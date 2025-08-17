using Dapper;
using Npgsql;

namespace AgentHost.Shared.Persistence;

public class PolicyScopeRepository : IPolicyScopeRepository
{
    private readonly string _connectionString;

    public PolicyScopeRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IEnumerable<PolicyScope>> ListByRunAsync(Guid runId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, run_id, allowed_model, allowed_tool, scope, created_at
            FROM policy_scopes
            WHERE run_id = @runId
        """;
        var rows = await connection.QueryAsync(sql, new { runId });
        return rows.Select(r => new PolicyScope(
            (Guid)r.id,
            (Guid)r.run_id,
            (string?)r.allowed_model,
            (string?)r.allowed_tool,
            (string?)r.scope,
            (DateTimeOffset)r.created_at
        ));
    }

    public async Task AddAsync(PolicyScope scope)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            INSERT INTO policy_scopes (id, run_id, allowed_model, allowed_tool, scope, created_at)
            VALUES (@Id, @RunId, @AllowedModel, @AllowedTool, @Scope, @CreatedAt)
        """;
        await connection.ExecuteAsync(sql, new
        {
            scope.Id,
            scope.RunId,
            scope.AllowedModel,
            scope.AllowedTool,
            scope.Scope,
            scope.CreatedAt
        });
    }

    public async Task DeleteAsync(Guid runId, Guid scopeId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            DELETE FROM policy_scopes WHERE run_id = @runId AND id = @scopeId
        """;
        await connection.ExecuteAsync(sql, new { runId, scopeId });
    }
}
