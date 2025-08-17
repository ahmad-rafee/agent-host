using System.Text.Json;
using Dapper;
using Npgsql;

namespace AgentHost.Shared.Persistence;

public class McpServerRepository : IMcpServerRepository
{
    private readonly string _connectionString;
    public McpServerRepository(string connectionString) => _connectionString = connectionString;

    public async Task<McpServer?> GetAsync(Guid id)
    {
        using var c = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, name, display_name, type, command, args, endpoint, env, is_enabled, status, last_heartbeat_at, error, created_at, updated_at
            FROM mcp_servers WHERE id = @id
            """;
        var row = await c.QueryFirstOrDefaultAsync(sql, new { id });
        return row == null ? null : Map(row);
    }

    public async Task<McpServer?> GetByNameAsync(string name)
    {
        using var c = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, name, display_name, type, command, args, endpoint, env, is_enabled, status, last_heartbeat_at, error, created_at, updated_at
            FROM mcp_servers WHERE name = @name
            """;
        var row = await c.QueryFirstOrDefaultAsync(sql, new { name });
        return row == null ? null : Map(row);
    }

    public async Task<IEnumerable<McpServer>> ListAsync(bool? enabled = null, int limit = 100, int offset = 0)
    {
        using var c = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, name, display_name, type, command, args, endpoint, env, is_enabled, status, last_heartbeat_at, error, created_at, updated_at
            FROM mcp_servers
            WHERE (@enabled IS NULL OR is_enabled = @enabled)
            ORDER BY created_at DESC
            LIMIT @limit OFFSET @offset
            """;
        var rows = await c.QueryAsync(sql, new { enabled, limit, offset });
        return rows.Select(Map);
    }

    public async Task CreateAsync(McpServer server)
    {
        using var c = new NpgsqlConnection(_connectionString);
        var sql = """
            INSERT INTO mcp_servers (id, name, display_name, type, command, args, endpoint, env, is_enabled, status, last_heartbeat_at, error, created_at, updated_at)
            VALUES (@Id, @Name, @DisplayName, @Type, @Command, @Args, @Endpoint, @Env::jsonb, @IsEnabled, @Status, @LastHeartbeatAt, @Error, @CreatedAt, @UpdatedAt)
            """;
        await c.ExecuteAsync(sql, new
        {
            server.Id,
            server.Name,
            server.DisplayName,
            server.Type,
            server.Command,
            Args = server.Args,
            server.Endpoint,
            Env = JsonSerializer.Serialize(server.Env),
            server.IsEnabled,
            server.Status,
            server.LastHeartbeatAt,
            server.Error,
            server.CreatedAt,
            server.UpdatedAt
        });
    }

    public async Task UpdateAsync(McpServer server)
    {
        using var c = new NpgsqlConnection(_connectionString);
        var sql = """
            UPDATE mcp_servers SET display_name=@DisplayName, type=@Type, command=@Command, args=@Args, endpoint=@Endpoint,
                env=@Env::jsonb, is_enabled=@IsEnabled, status=@Status, last_heartbeat_at=@LastHeartbeatAt, error=@Error, updated_at=@UpdatedAt
            WHERE id=@Id
            """;
        await c.ExecuteAsync(sql, new
        {
            server.Id,
            server.DisplayName,
            server.Type,
            server.Command,
            Args = server.Args,
            server.Endpoint,
            Env = JsonSerializer.Serialize(server.Env),
            server.IsEnabled,
            server.Status,
            server.LastHeartbeatAt,
            server.Error,
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    public async Task UpdateStatusAsync(Guid id, string status, string? error = null, DateTimeOffset? heartbeat = null)
    {
        using var c = new NpgsqlConnection(_connectionString);
        var sql = """
            UPDATE mcp_servers SET status=@status, error=@error, last_heartbeat_at=@heartbeat, updated_at=@updatedAt
            WHERE id=@id
            """;
        await c.ExecuteAsync(sql, new { id, status, error, heartbeat, updatedAt = DateTimeOffset.UtcNow });
    }

    public async Task EnableAsync(Guid id, bool isEnabled)
    {
        using var c = new NpgsqlConnection(_connectionString);
        var sql = """
            UPDATE mcp_servers SET is_enabled=@isEnabled, status=CASE WHEN @isEnabled THEN status ELSE 'disabled' END, updated_at=@updatedAt
            WHERE id=@id
            """;
        await c.ExecuteAsync(sql, new { id, isEnabled, updatedAt = DateTimeOffset.UtcNow });
    }

    private static McpServer Map(dynamic row)
    {
        var env = new Dictionary<string, string>();
        if (row.env is string envStr && !string.IsNullOrEmpty(envStr))
        {
            try { env = JsonSerializer.Deserialize<Dictionary<string, string>>(envStr) ?? new(); } catch { }
        }
        string[] args = row.args is string[] a ? a : Array.Empty<string>();
        return new McpServer(
            (Guid)row.id,
            (string)row.name,
            (string)row.type,
            (string?)row.display_name,
            (string?)row.command,
            args,
            (string?)row.endpoint,
            env,
            (bool)row.is_enabled,
            (string)row.status,
            (DateTimeOffset?)row.last_heartbeat_at,
            (string?)row.error,
            (DateTimeOffset)row.created_at,
            (DateTimeOffset)row.updated_at
        );
    }
}
