using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace dorks_and_dice_site.Plugins.DiscordBot;

public sealed record DiscordGuildRole(string Id, string Name);

public interface IDiscordBotClient
{
    Task<IReadOnlyList<DiscordGuildRole>> GetGuildRolesAsync(
        string guildId,
        CancellationToken cancellationToken = default);

    Task<DiscordGuildRole> CreateGuildRoleAsync(
        string guildId,
        string name,
        CancellationToken cancellationToken = default);

    Task UpdateGuildRoleAsync(
        string guildId,
        string roleId,
        string name,
        CancellationToken cancellationToken = default);

    Task DeleteGuildRoleAsync(
        string guildId,
        string roleId,
        CancellationToken cancellationToken = default);

    Task<bool> AddMemberRoleAsync(
        string guildId,
        string userId,
        string roleId,
        CancellationToken cancellationToken = default);

    Task RemoveMemberRoleAsync(
        string guildId,
        string userId,
        string roleId,
        CancellationToken cancellationToken = default);
}

public sealed class DiscordBotClient(HttpClient httpClient) : IDiscordBotClient
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<IReadOnlyList<DiscordGuildRole>> GetGuildRolesAsync(
        string guildId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"guilds/{guildId}/roles"),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement
            .EnumerateArray()
            .Select(role => new DiscordGuildRole(
                role.GetProperty("id").GetString() ?? string.Empty,
                role.GetProperty("name").GetString() ?? string.Empty))
            .Where(role => role.Id.Length > 0)
            .ToArray();
    }

    public async Task<DiscordGuildRole> CreateGuildRoleAsync(
        string guildId,
        string name,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            () => JsonRequest(HttpMethod.Post, $"guilds/{guildId}/roles", new { name }),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return new DiscordGuildRole(
            document.RootElement.GetProperty("id").GetString() ?? string.Empty,
            document.RootElement.GetProperty("name").GetString() ?? name);
    }

    public async Task UpdateGuildRoleAsync(
        string guildId,
        string roleId,
        string name,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            () => JsonRequest(
                HttpMethod.Patch,
                $"guilds/{guildId}/roles/{roleId}",
                new { name }),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DeleteGuildRoleAsync(
        string guildId,
        string roleId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(
                HttpMethod.Delete,
                $"guilds/{guildId}/roles/{roleId}"),
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<bool> AddMemberRoleAsync(
        string guildId,
        string userId,
        string roleId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(
                HttpMethod.Put,
                $"guilds/{guildId}/members/{userId}/roles/{roleId}"),
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return true;
    }

    public async Task RemoveMemberRoleAsync(
        string guildId,
        string userId,
        string roleId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(
                HttpMethod.Delete,
                $"guilds/{guildId}/members/{userId}/roles/{roleId}"),
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = requestFactory();
            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt >= 2)
            {
                return response;
            }

            var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(1);
            response.Dispose();
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static HttpRequestMessage JsonRequest(
        HttpMethod method,
        string requestUri,
        object body)
    {
        var request = new HttpRequestMessage(method, requestUri);
        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");
        return request;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Discord API request failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}",
            null,
            response.StatusCode);
    }
}

public interface IDiscordBotInstallLinkProvider
{
    string? CreateInstallUrl(string? guildId = null);
}

public sealed class DiscordBotInstallLinkProvider(
    string? clientId,
    bool enabled) : IDiscordBotInstallLinkProvider
{
    private const ulong ManageRolesPermission = 1UL << 28;

    public string? CreateInstallUrl(string? guildId = null)
    {
        if (!enabled || string.IsNullOrWhiteSpace(clientId))
        {
            return null;
        }

        var query = $"client_id={Uri.EscapeDataString(clientId)}"
            + $"&scope=bot"
            + $"&permissions={ManageRolesPermission}";

        if (!string.IsNullOrWhiteSpace(guildId))
        {
            query += $"&guild_id={Uri.EscapeDataString(guildId)}";
        }

        return $"https://discord.com/oauth2/authorize?{query}";
    }
}
