using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using MonitorService.Configuration;

namespace MonitorService.Persistence;

public sealed class SqliteSnapshotRepository : ISnapshotRepository
{
    private readonly string _connectionString;

    public SqliteSnapshotRepository(IOptions<MonitorOptions> options)
    {
        var path = Path.GetFullPath(options.Value.Database.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS snapshots (
                source_name  TEXT NOT NULL,
                item_key     TEXT NOT NULL,
                fields_json  TEXT NOT NULL,
                hash         TEXT NOT NULL,
                captured_at  TEXT NOT NULL,
                url          TEXT,
                title        TEXT,
                PRIMARY KEY (source_name, item_key, captured_at)
            );

            CREATE INDEX IF NOT EXISTS idx_snapshots_latest
                ON snapshots (source_name, item_key, captured_at DESC);
        """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> HasAnyAsync(string sourceName, CancellationToken ct)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM snapshots WHERE source_name = $source LIMIT 1;";
        cmd.Parameters.AddWithValue("$source", sourceName);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is not null;
    }

    public async Task<Snapshot?> GetLatestAsync(string sourceName, string itemKey, CancellationToken ct)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT fields_json, hash, captured_at, url, title
            FROM snapshots
            WHERE source_name = $source AND item_key = $key
            ORDER BY captured_at DESC
            LIMIT 1;
        """;
        cmd.Parameters.AddWithValue("$source", sourceName);
        cmd.Parameters.AddWithValue("$key", itemKey);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new Snapshot(
            sourceName,
            itemKey,
            reader.GetString(0),
            reader.GetString(1),
            DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4)
        );
    }

    public async Task SaveAsync(Snapshot snapshot, CancellationToken ct)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO snapshots (source_name, item_key, fields_json, hash, captured_at, url, title)
            VALUES ($source, $key, $fields, $hash, $captured, $url, $title);
        """;
        cmd.Parameters.AddWithValue("$source",   snapshot.SourceName);
        cmd.Parameters.AddWithValue("$key",      snapshot.ItemKey);
        cmd.Parameters.AddWithValue("$fields",   snapshot.FieldsJson);
        cmd.Parameters.AddWithValue("$hash",     snapshot.Hash);
        cmd.Parameters.AddWithValue("$captured", snapshot.CapturedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$url",      (object?)snapshot.Url   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$title",    (object?)snapshot.Title ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
