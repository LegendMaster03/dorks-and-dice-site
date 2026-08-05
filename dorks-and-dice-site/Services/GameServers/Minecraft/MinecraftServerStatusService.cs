using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using dorks_and_dice_site.Models.GameServers.Minecraft;
using Microsoft.Extensions.Options;

namespace dorks_and_dice_site.Services.GameServers.Minecraft;

public sealed class MinecraftServerStatusService : IMinecraftServerStatusService
{
    private readonly MinecraftServerOptions _options;
    private readonly ILogger<MinecraftServerStatusService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private MinecraftServerStatus? _cachedStatus;
    private DateTimeOffset _cacheExpiresAt;

    public MinecraftServerStatusService(
        IOptions<MinecraftServerOptions> options,
        ILogger<MinecraftServerStatusService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MinecraftServerStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (_cachedStatus is not null && now < _cacheExpiresAt)
        {
            return _cachedStatus;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (_cachedStatus is not null && now < _cacheExpiresAt)
            {
                return _cachedStatus;
            }

            try
            {
                _cachedStatus = await QueryAsync(cancellationToken);
                _cacheExpiresAt = now.AddSeconds(Math.Max(1, _options.CacheSeconds));
            }
            catch (Exception ex) when (ex is SocketException or IOException or TimeoutException or JsonException)
            {
                _logger.LogWarning(ex, "Minecraft status query failed for {Host}:{Port}", _options.Host, _options.Port);
                _cachedStatus = MinecraftServerStatus.Unavailable(ex.Message);
                _cacheExpiresAt = now.AddSeconds(Math.Max(1, _options.FailureCacheSeconds));
            }

            return _cachedStatus;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<MinecraftServerStatus> QueryAsync(CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(250, _options.TimeoutMilliseconds)));
        var timeoutToken = timeoutSource.Token;

        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(_options.Host, _options.Port, timeoutToken);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Minecraft status connection timed out.", ex);
        }

        await using var stream = client.GetStream();

        using var handshakePayload = new MemoryStream();
        WriteVarInt(handshakePayload, 0);
        WriteVarInt(handshakePayload, _options.ProtocolVersion);
        WriteString(handshakePayload, _options.Host);
        handshakePayload.WriteByte((byte)(_options.Port >> 8));
        handshakePayload.WriteByte((byte)_options.Port);
        WriteVarInt(handshakePayload, 1);

        await WritePacketAsync(stream, handshakePayload.ToArray(), timeoutToken);
        await WritePacketAsync(stream, [0], timeoutToken);

        _ = await ReadVarIntAsync(stream, timeoutToken);
        var packetId = await ReadVarIntAsync(stream, timeoutToken);
        if (packetId != 0)
        {
            throw new IOException($"Unexpected Minecraft status packet ID {packetId}.");
        }

        var jsonLength = await ReadVarIntAsync(stream, timeoutToken);
        if (jsonLength <= 0 || jsonLength > 1_048_576)
        {
            throw new IOException("Minecraft status response length was invalid.");
        }

        var payload = new byte[jsonLength];
        await stream.ReadExactlyAsync(payload, timeoutToken);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        var version = root.TryGetProperty("version", out var versionElement)
            && versionElement.TryGetProperty("name", out var versionName)
            ? versionName.GetString()
            : null;

        int? onlinePlayers = null;
        int? maximumPlayers = null;
        if (root.TryGetProperty("players", out var playersElement))
        {
            if (playersElement.TryGetProperty("online", out var onlineElement) && onlineElement.TryGetInt32(out var online))
            {
                onlinePlayers = online;
            }

            if (playersElement.TryGetProperty("max", out var maxElement) && maxElement.TryGetInt32(out var maximum))
            {
                maximumPlayers = maximum;
            }
        }

        var motd = root.TryGetProperty("description", out var descriptionElement)
            ? ExtractText(descriptionElement)
            : null;

        return new MinecraftServerStatus(
            true,
            motd,
            version,
            onlinePlayers,
            maximumPlayers,
            DateTimeOffset.UtcNow);
    }

    private static async Task WritePacketAsync(NetworkStream stream, byte[] payload, CancellationToken cancellationToken)
    {
        using var packet = new MemoryStream();
        WriteVarInt(packet, payload.Length);
        packet.Write(payload);
        await stream.WriteAsync(packet.ToArray(), cancellationToken);
    }

    private static void WriteString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarInt(stream, bytes.Length);
        stream.Write(bytes);
    }

    private static void WriteVarInt(Stream stream, int value)
    {
        uint remaining = unchecked((uint)value);
        do
        {
            var current = (byte)(remaining & 0x7F);
            remaining >>= 7;
            if (remaining != 0)
            {
                current |= 0x80;
            }

            stream.WriteByte(current);
        }
        while (remaining != 0);
    }

    private static async Task<int> ReadVarIntAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var value = 0;
        var position = 0;
        var buffer = new byte[1];

        while (position < 35)
        {
            await stream.ReadExactlyAsync(buffer, cancellationToken);
            var current = buffer[0];
            value |= (current & 0x7F) << position;

            if ((current & 0x80) == 0)
            {
                return value;
            }

            position += 7;
        }

        throw new IOException("Minecraft VarInt exceeded the supported length.");
    }

    private static string? ExtractText(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Object => ExtractObjectText(element),
            JsonValueKind.Array => string.Concat(element.EnumerateArray().Select(ExtractText)),
            _ => null
        };
    }

    private static string? ExtractObjectText(JsonElement element)
    {
        var builder = new StringBuilder();

        if (element.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
        {
            builder.Append(textElement.GetString());
        }

        if (element.TryGetProperty("extra", out var extraElement) && extraElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in extraElement.EnumerateArray())
            {
                builder.Append(ExtractText(child));
            }
        }

        var result = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }
}
