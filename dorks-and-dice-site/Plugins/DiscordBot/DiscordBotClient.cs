using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace dorks_and_dice_site.Plugins.DiscordBot;

public sealed record DiscordGuild(string Id, string Name, string OwnerId);
public sealed record DiscordGuildRole(string Id, string Name);
public sealed record DiscordGuildChannel(
    string Id,
    string Name,
    DiscordManagedChannelKind Kind,
    string? ParentId);

public interface IDiscordBotClient
{
    Task<DiscordGuild?> GetGuildAsync(
        string guildId,
        CancellationToken cancellationToken = default);

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

    Task<IReadOnlyList<DiscordGuildChannel>> GetGuildChannelsAsync(
        string guildId,
        CancellationToken cancellationToken = default);

    Task<DiscordGuildChannel> CreateGuildChannelAsync(
        string guildId,
        string name,
        DiscordManagedChannelKind kind,
        string? parentId,
        CancellationToken cancellationToken = default);

    Task UpdateGuildChannelAsync(
        string channelId,
        string name,
        string? parentId,
        CancellationToken cancellationToken = default);

    Task DeleteGuildChannelAsync(
        string channelId,
        CancellationToken cancellationToken = default);
}

public sealed class DiscordBotClient(HttpClient httpClient) : IDiscordBotClient
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<DiscordGuild?> GetGuildAsync(
        string guildId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"guilds/{guildId}"),
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return new DiscordGuild(
            document.RootElement.GetProperty("id").GetString() ?? guildId,
            document.RootElement.GetProperty("name").GetString() ?? string.Empty,
            document.RootElement.GetProperty("owner_id").GetString() ?? string.Empty);
    }

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

    public async Task<IReadOnlyList<DiscordGuildChannel>> GetGuildChannelsAsync(
        string guildId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"guilds/{guildId}/channels"),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement
            .EnumerateArray()
            .Where(channel =>
                channel.TryGetProperty("type", out var type)
                && Enum.IsDefined(typeof(DiscordManagedChannelKind), type.GetInt32()))
            .Select(channel => new DiscordGuildChannel(
                channel.GetProperty("id").GetString() ?? string.Empty,
                channel.GetProperty("name").GetString() ?? string.Empty,
                (DiscordManagedChannelKind)channel.GetProperty("type").GetInt32(),
                channel.TryGetProperty("parent_id", out var parent)
                    && parent.ValueKind == JsonValueKind.String
                        ? parent.GetString()
                        : null))
            .Where(channel => channel.Id.Length > 0)
            .ToArray();
    }

    public async Task<DiscordGuildChannel> CreateGuildChannelAsync(
        string guildId,
        string name,
        DiscordManagedChannelKind kind,
        string? parentId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            () => JsonRequest(
                HttpMethod.Post,
                $"guilds/{guildId}/channels",
                new
                {
                    name,
                    type = (int)kind,
                    parent_id = parentId
                }),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return new DiscordGuildChannel(
            document.RootElement.GetProperty("id").GetString() ?? string.Empty,
            document.RootElement.GetProperty("name").GetString() ?? name,
            (DiscordManagedChannelKind)document.RootElement.GetProperty("type").GetInt32(),
            document.RootElement.TryGetProperty("parent_id", out var parent)
                && parent.ValueKind == JsonValueKind.String
                    ? parent.GetString()
                    : null);
    }

    public async Task UpdateGuildChannelAsync(
        string channelId,
        string name,
        string? parentId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            () => JsonRequest(
                HttpMethod.Patch,
                $"channels/{channelId}",
                new
                {
                    name,
                    parent_id = parentId
                }),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DeleteGuildChannelAsync(
        string channelId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Delete, $"channels/{channelId}"),
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
    private const ulong ManageChannelsPermission = 1UL << 4;
    private const ulong ManageRolesPermission = 1UL << 28;
    private const ulong RequiredPermissions =
        ManageChannelsPermission | ManageRolesPermission;

    public string? CreateInstallUrl(string? guildId = null)
    {
        if (!enabled || string.IsNullOrWhiteSpace(clientId))
        {
            return null;
        }

        var query = $"client_id={Uri.EscapeDataString(clientId)}"
            + $"&scope=bot"
            + $"&permissions={RequiredPermissions}";

        if (!string.IsNullOrWhiteSpace(guildId))
        {
            query += $"&guild_id={Uri.EscapeDataString(guildId)}";
        }

        return $"https://discord.com/oauth2/authorize?{query}";
    }
}
