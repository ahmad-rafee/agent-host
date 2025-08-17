using System.Text.Json;
using Dapper;
using Npgsql;

namespace AgentHost.Shared.Persistence;

public class McpToolRepository : IMcpToolRepository
{
    private readonly string _connectionString;
    public McpToolRepository(string connectionString) => _connectionString = connectionString;

    public async Task<IEnumerable<McpToolRecord>> ListByServerAsync(Guid serverId, bool includeDeleted = false, string? namePrefix = null, string? version = null, bool deletedOnly = false, int limit = 100, int offset = 0)
    {
        using var c = new NpgsqlConnection(_connectionString);
        var conditions = new List<string> { "server_id = @serverId" };
        if (deletedOnly)
        {
            conditions.Add("is_deleted = true");
        }
        else if (!includeDeleted)
        {
            conditions.Add("is_deleted = false");
        }
        if (!string.IsNullOrWhiteSpace(namePrefix)) conditions.Add("name ILIKE @nameLike");
        if (!string.IsNullOrWhiteSpace(version)) conditions.Add("version = @version");
        var where = string.Join(" AND ", conditions);
        var sql = $"""
            SELECT id, server_id, name, description, schema, required_scopes, version, schema_hash, is_deleted, created_at, updated_at
            FROM mcp_tools WHERE {where} ORDER BY name LIMIT @limit OFFSET @offset
            """;
        var rows = await c.QueryAsync(sql, new { serverId, nameLike = namePrefix + "%", version, limit, offset });
        return rows.Select(Map);
    }

    public async Task<McpToolRecord?> GetAsync(Guid id)
    {
        using var c = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, server_id, name, description, schema, required_scopes, version, schema_hash, is_deleted, created_at, updated_at
            FROM mcp_tools WHERE id = @id
            """;
        var row = await c.QueryFirstOrDefaultAsync(sql, new { id });
        return row == null ? null : Map(row);
    }

    public async Task<McpToolRecord?> GetByServerAndNameAsync(Guid serverId, string name, bool includeDeleted = false)
    {
        using var c = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, server_id, name, description, schema, required_scopes, version, schema_hash, is_deleted, created_at, updated_at
            FROM mcp_tools WHERE server_id = @serverId AND name = @name AND (@includeDeleted OR is_deleted = false)
            """;
        var row = await c.QueryFirstOrDefaultAsync(sql, new { serverId, name, includeDeleted });
        return row == null ? null : Map(row);
    }

    public async Task UpsertAsync(McpToolRecord tool) => await UpsertManyAsync(new[] { tool });

    public async Task UpsertManyAsync(IEnumerable<McpToolRecord> tools)
    {
    using var c = new NpgsqlConnection(_connectionString);
    await c.OpenAsync();
    using var tx = await c.BeginTransactionAsync();
        var sql = """
            INSERT INTO mcp_tools (id, server_id, name, description, schema, required_scopes, version, schema_hash, is_deleted, created_at, updated_at)
            VALUES (@Id, @ServerId, @Name, @Description, @Schema::jsonb, @RequiredScopes, @Version, @SchemaHash, false, @CreatedAt, @UpdatedAt)
            ON CONFLICT (server_id, name) DO UPDATE SET
                description = EXCLUDED.description,
                schema = EXCLUDED.schema,
                required_scopes = EXCLUDED.required_scopes,
                version = EXCLUDED.version,
                schema_hash = EXCLUDED.schema_hash,
                is_deleted = false,
                updated_at = EXCLUDED.updated_at
            """;
        foreach (var t in tools)
        {
            await c.ExecuteAsync(sql, new
            {
                t.Id,
                t.ServerId,
                t.Name,
                t.Description,
                Schema = JsonSerializer.Serialize(t.Schema),
                RequiredScopes = t.RequiredScopes,
                t.Version,
                t.SchemaHash,
                t.CreatedAt,
                UpdatedAt = DateTimeOffset.UtcNow
            }, tx);
        }
        await tx.CommitAsync();
    }

    public async Task MarkDeletedAsync(Guid serverId, IEnumerable<string> toolNamesToDelete)
    {
        var names = toolNamesToDelete.Distinct().ToArray();
        if (names.Length == 0) return;
        using var c = new NpgsqlConnection(_connectionString);
        var sql = "UPDATE mcp_tools SET is_deleted = true, updated_at = @Now WHERE server_id = @ServerId AND name = ANY(@Names)";
        await c.ExecuteAsync(sql, new { ServerId = serverId, Names = names, Now = DateTimeOffset.UtcNow });
    }

    private static McpToolRecord Map(dynamic row)
    {
        object schema = new { };
        if (row.schema is string schemaStr && !string.IsNullOrEmpty(schemaStr))
        {
            try { schema = JsonSerializer.Deserialize<object>(schemaStr) ?? new { }; } catch { }
        }
        string[] scopes = row.required_scopes is string[] s ? s : Array.Empty<string>();
        return new McpToolRecord(
            (Guid)row.id,
            (Guid)row.server_id,
            (string)row.name,
            (string?)row.description,
            schema,
            scopes,
            (string?)row.version,
            (string?)row.schema_hash,
            (bool)(row.is_deleted ?? false),
            (DateTimeOffset)row.created_at,
            (DateTimeOffset)row.updated_at
        );
    }
}
