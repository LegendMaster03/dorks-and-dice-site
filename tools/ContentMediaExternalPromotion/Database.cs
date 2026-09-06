using System.Data;
using System.Data.Common;
using System.Globalization;
using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace ContentMediaExternalPromotion;

public sealed class Database(string provider, string connectionString)
{
    public string Provider { get; } = provider;
    internal string ConnectionString { get; } = connectionString;
    public bool IsSqlite => Provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase);
    public string Identity => IsSqlite
        ? StateStore.Hash(new { Provider = "Sqlite", File = Path.GetFullPath(new SqliteConnectionStringBuilder(ConnectionString).DataSource) })
        : StateStore.Hash(new { Provider = "PostgreSQL", new NpgsqlConnectionStringBuilder(ConnectionString).Host, new NpgsqlConnectionStringBuilder(ConnectionString).Port, new NpgsqlConnectionStringBuilder(ConnectionString).Database });
    public string ReadOnlyConnectionString => IsSqlite
        ? new SqliteConnectionStringBuilder(ConnectionString) { Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ConnectionString
        : new NpgsqlConnectionStringBuilder(ConnectionString) { Options = "-c default_transaction_read_only=on", Timeout = 15, CommandTimeout = 60 }.ConnectionString;
    public DbConnection Connection(bool readOnly) => IsSqlite
        ? new SqliteConnection(readOnly ? ReadOnlyConnectionString : ConnectionString)
        : new NpgsqlConnection(readOnly ? ReadOnlyConnectionString : ConnectionString);
    public async Task Integrity()
    {
        if (!IsSqlite) return;
        await using var c = Connection(true); await c.OpenAsync();
        if (Convert.ToString(await Scalar(c, null, "PRAGMA integrity_check")) != "ok") throw new PromotionException("SQLite integrity check failed.");
        await using var cmd = c.CreateCommand(); cmd.CommandText = "PRAGMA foreign_key_check";
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync()) throw new PromotionException("SQLite foreign key check failed.");
    }
    public async Task<Snapshot> Read()
    {
        await using var c = Connection(true); await c.OpenAsync();
        await using var transaction = await c.BeginTransactionAsync(IsSqlite ? IsolationLevel.Serializable : IsolationLevel.RepeatableRead);
        if (!IsSqlite) await Execute(c, transaction, "SET TRANSACTION READ ONLY");
        return await Read(c, transaction);
    }
    public static async Task<Snapshot> Read(DbConnection c, DbTransaction? transaction)
    {
        // Reuse the application's model for the schema check; never call its write-capable initializer in a read mode.
        using var model = new ContentDbContext(new DbContextOptionsBuilder<ContentDbContext>().UseSqlite("Data Source=:memory:").Options);
        var result = new Snapshot();
        foreach (var entity in model.Model.GetEntityTypes().OrderBy(e => e.GetTableName(), StringComparer.Ordinal))
        {
            var table = entity.GetTableName()!;
            var columns = entity.GetProperties().Select(p => p.GetColumnName(StoreObjectIdentifier.Table(table, null))!).Order(StringComparer.Ordinal).ToArray();
            await using (var schema = c.CreateCommand())
            {
                schema.Transaction = transaction; schema.CommandText = $"SELECT {string.Join(',', columns.Select(Quote))} FROM {Quote(table)} WHERE 1=0";
                await using var check = await schema.ExecuteReaderAsync();
            }
            columns = columns.Where(name => name != "asset_data").ToArray();
            await using var command = c.CreateCommand(); command.Transaction = transaction;
            command.CommandText = $"SELECT {string.Join(',', columns.Select(Quote))} FROM {Quote(table)}";
            await using var reader = await command.ExecuteReaderAsync(); var rows = new List<Row>();
            while (await reader.ReadAsync())
            {
                var row = new Row();
                for (int i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.GetValue(i) switch
                { DBNull => null, DateTime dt => dt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), var value => Convert.ToString(value, CultureInfo.InvariantCulture) };
                rows.Add(row);
            }
            result.Tables[table] = rows.OrderBy(StateStore.Hash, StringComparer.Ordinal).ToList();
        }
        return result;
    }
    public async Task<byte[]> Bytes(string key)
    {
        await using var c = Connection(true); await c.OpenAsync();
        return (byte[]?)await Scalar(c, null, "SELECT asset_data FROM content_asset WHERE asset_key=@key", ("key", key))
            ?? throw new PromotionException("Expected media asset is missing.");
    }
    public static string Quote(string identifier) => '"' + identifier.Replace("\"", "\"\"") + '"';
    public static DbCommand Command(DbConnection c, DbTransaction? tx, string sql, params (string, object?)[] parameters)
    {
        var cmd = c.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = sql;
        foreach (var (name, value) in parameters) { var p = cmd.CreateParameter(); p.ParameterName = "@" + name; p.Value = value ?? DBNull.Value; cmd.Parameters.Add(p); }
        return cmd;
    }
    public static async Task<object?> Scalar(DbConnection c, DbTransaction? tx, string sql, params (string, object?)[] parameters)
    { await using var cmd = Command(c, tx, sql, parameters); return await cmd.ExecuteScalarAsync(); }
    public static async Task<int> Execute(DbConnection c, DbTransaction? tx, string sql, params (string, object?)[] parameters)
    { await using var cmd = Command(c, tx, sql, parameters); return await cmd.ExecuteNonQueryAsync(); }
    public async Task<IAsyncDisposable> WriterLock()
    {
        DbConnection c = IsSqlite ? Connection(false) : new NpgsqlConnection(new NpgsqlConnectionStringBuilder(ConnectionString) { Pooling = false }.ConnectionString); await c.OpenAsync();
        if (!IsSqlite && !Equals(await Scalar(c, null, "SELECT pg_try_advisory_lock(7421963812058941)"), true))
        { await c.DisposeAsync(); throw new PromotionException("Another promotion tool is writing this External database."); }
        return c; // A session-scoped lock is released by closing its non-pooled connection.
    }
}
