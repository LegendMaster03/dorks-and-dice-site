using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.Data.Sqlite;
using Npgsql;
using NpgsqlTypes;

namespace dorks_and_dice_site.Services.Tools;

public sealed class DatabaseToolRegistry : IToolRegistry
{
    private const string SelectColumns = """
        SELECT
            tool_id,
            tool_slug,
            tool_display_name,
            tool_description,
            tool_integration_type,
            tool_upstream_base_url,
            tool_frontend_entry_point,
            tool_health_path,
            tool_modes,
            tool_allow_anonymous,
            tool_enabled,
            tool_created_at,
            tool_updated_at
        FROM tool_registration
        """;

    private readonly IContentSourceRegistry _sourceRegistry;

    public DatabaseToolRegistry(IContentSourceRegistry sourceRegistry)
    {
        _sourceRegistry = sourceRegistry;
    }

    public async Task<IReadOnlyList<ToolRegistration>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var (connection, provider) = CreateConnection();
        await using (connection)
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = SelectColumns;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var tools = new List<ToolRegistration>();
            while (await reader.ReadAsync(cancellationToken))
            {
                tools.Add(ReadRegistration(reader, provider));
            }

            return tools
                .OrderBy(tool => tool.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public Task<ToolRegistration?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetSingleAsync(
            "tool_id = @tool_id",
            (command, provider) => AddIdParameter(command, "@tool_id", id, provider),
            cancellationToken);

    public Task<ToolRegistration?> GetBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default) =>
        GetSingleAsync(
            "lower(tool_slug) = lower(@tool_slug)",
            (command, _) => AddParameter(command, "@tool_slug", slug),
            cancellationToken);

    public async Task SaveAsync(
        ToolRegistration registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);

        var duplicate = await GetBySlugAsync(registration.Slug, cancellationToken);
        if (duplicate is not null && duplicate.Id != registration.Id)
        {
            throw DuplicateSlug(registration.Slug);
        }

        var (connection, provider) = CreateConnection();
        await using (connection)
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO tool_registration
                (
                    tool_id,
                    tool_slug,
                    tool_display_name,
                    tool_description,
                    tool_integration_type,
                    tool_upstream_base_url,
                    tool_frontend_entry_point,
                    tool_health_path,
                    tool_modes,
                    tool_allow_anonymous,
                    tool_enabled,
                    tool_created_at,
                    tool_updated_at
                )
                VALUES
                (
                    @tool_id,
                    @tool_slug,
                    @tool_display_name,
                    @tool_description,
                    @tool_integration_type,
                    @tool_upstream_base_url,
                    @tool_frontend_entry_point,
                    @tool_health_path,
                    @tool_modes,
                    @tool_allow_anonymous,
                    @tool_enabled,
                    @tool_created_at,
                    @tool_updated_at
                )
                ON CONFLICT(tool_id) DO UPDATE SET
                    tool_slug = excluded.tool_slug,
                    tool_display_name = excluded.tool_display_name,
                    tool_description = excluded.tool_description,
                    tool_integration_type = excluded.tool_integration_type,
                    tool_upstream_base_url = excluded.tool_upstream_base_url,
                    tool_frontend_entry_point = excluded.tool_frontend_entry_point,
                    tool_health_path = excluded.tool_health_path,
                    tool_modes = excluded.tool_modes,
                    tool_allow_anonymous = excluded.tool_allow_anonymous,
                    tool_enabled = excluded.tool_enabled,
                    tool_created_at = excluded.tool_created_at,
                    tool_updated_at = excluded.tool_updated_at
                """;

            AddIdParameter(command, "@tool_id", registration.Id, provider);
            AddParameter(command, "@tool_slug", registration.Slug);
            AddParameter(command, "@tool_display_name", registration.DisplayName);
            AddParameter(command, "@tool_description", registration.Description);
            AddParameter(command, "@tool_integration_type", (short)registration.IntegrationType);
            AddParameter(command, "@tool_upstream_base_url", registration.UpstreamBaseUrl);
            AddParameter(command, "@tool_frontend_entry_point", registration.FrontendEntryPoint);
            AddParameter(command, "@tool_health_path", registration.HealthPath);
            AddModesParameter(command, "@tool_modes", registration.Modes, provider);
            AddParameter(command, "@tool_allow_anonymous", registration.AllowAnonymous);
            AddParameter(command, "@tool_enabled", registration.Enabled);
            AddTimestampParameter(command, "@tool_created_at", registration.CreatedAt, provider);
            AddTimestampParameter(command, "@tool_updated_at", registration.UpdatedAt, provider);

            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (PostgresException ex) when (
                ex.SqlState == PostgresErrorCodes.UniqueViolation
                && string.Equals(ex.ConstraintName, "ux_tool_registration_slug_ci", StringComparison.Ordinal))
            {
                throw DuplicateSlug(registration.Slug, ex);
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 2067)
            {
                throw DuplicateSlug(registration.Slug, ex);
            }
        }
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var (connection, provider) = CreateConnection();
        await using (connection)
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM tool_registration WHERE tool_id = @tool_id";
            AddIdParameter(command, "@tool_id", id, provider);
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }
    }

    private async Task<ToolRegistration?> GetSingleAsync(
        string predicate,
        Action<DbCommand, RegistryProvider> addParameter,
        CancellationToken cancellationToken)
    {
        var (connection, provider) = CreateConnection();
        await using (connection)
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"{SelectColumns} WHERE {predicate}";
            addParameter(command, provider);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return ReadRegistration(reader, provider);
        }
    }

    private (DbConnection Connection, RegistryProvider Provider) CreateConnection()
    {
        var source = _sourceRegistry.GetSource(_sourceRegistry.AuthoringSourceKey);
        return source.Provider.ToLowerInvariant() switch
        {
            "sqlite" => (new SqliteConnection(source.ConnectionString), RegistryProvider.Sqlite),
            "postgres" or "postgresql" => (new NpgsqlConnection(source.ConnectionString), RegistryProvider.PostgreSql),
            _ => throw new NotSupportedException(
                $"Tool registry content database provider '{source.Provider}' is not supported.")
        };
    }

    private static ToolRegistration ReadRegistration(DbDataReader reader, RegistryProvider provider) => new()
    {
        Id = ReadGuid(reader.GetValue(0)),
        Slug = reader.GetString(1),
        DisplayName = reader.GetString(2),
        Description = ReadNullableString(reader, 3),
        IntegrationType = (ToolIntegrationType)Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
        UpstreamBaseUrl = ReadNullableString(reader, 5),
        FrontendEntryPoint = ReadNullableString(reader, 6),
        HealthPath = ReadNullableString(reader, 7),
        Modes = ReadModes(reader.GetValue(8), provider),
        AllowAnonymous = Convert.ToBoolean(reader.GetValue(9), CultureInfo.InvariantCulture),
        Enabled = Convert.ToBoolean(reader.GetValue(10), CultureInfo.InvariantCulture),
        CreatedAt = ReadTimestamp(reader.GetValue(11)),
        UpdatedAt = ReadTimestamp(reader.GetValue(12))
    };

    private static Guid ReadGuid(object value) => value switch
    {
        Guid guid => guid,
        string text => Guid.Parse(text),
        _ => Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException("Tool registration ID is null."))
    };

    private static string? ReadNullableString(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static List<string> ReadModes(object value, RegistryProvider provider)
    {
        if (provider == RegistryProvider.PostgreSql)
        {
            return value switch
            {
                string[] values => values.ToList(),
                IEnumerable<string> values => values.ToList(),
                _ => throw new InvalidOperationException("Tool registration modes are not a PostgreSQL text array.")
            };
        }

        var json = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "[]";
        return JsonSerializer.Deserialize<List<string>>(json) ?? [];
    }

    private static DateTimeOffset ReadTimestamp(object value) => value switch
    {
        DateTimeOffset timestamp => timestamp,
        DateTime timestamp when timestamp.Kind == DateTimeKind.Utc => new DateTimeOffset(timestamp),
        DateTime timestamp => new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)),
        string text => DateTimeOffset.Parse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        _ => throw new InvalidOperationException("Tool registration timestamp has an unsupported database type.")
    };

    private static void AddIdParameter(
        DbCommand command,
        string name,
        Guid value,
        RegistryProvider provider) =>
        AddParameter(command, name, provider == RegistryProvider.Sqlite ? value.ToString("D") : value);

    private static void AddModesParameter(
        DbCommand command,
        string name,
        IReadOnlyCollection<string> modes,
        RegistryProvider provider)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        if (provider == RegistryProvider.PostgreSql)
        {
            parameter.Value = modes.ToArray();
            if (parameter is NpgsqlParameter npgsqlParameter)
            {
                npgsqlParameter.NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text;
            }
        }
        else
        {
            parameter.Value = JsonSerializer.Serialize(modes);
        }

        command.Parameters.Add(parameter);
    }

    private static void AddTimestampParameter(
        DbCommand command,
        string name,
        DateTimeOffset value,
        RegistryProvider provider) =>
        AddParameter(
            command,
            name,
            provider == RegistryProvider.Sqlite
                ? value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                : value.ToUniversalTime().UtcDateTime);

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static InvalidOperationException DuplicateSlug(string slug, Exception? innerException = null) =>
        new($"A tool with slug '{slug}' already exists.", innerException);

    private enum RegistryProvider
    {
        Sqlite,
        PostgreSql
    }
}
