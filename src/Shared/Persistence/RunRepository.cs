using System.Data;
using System.Text.Json;
using AgentHost.Shared.Contracts;
using Dapper;
using Npgsql;

namespace AgentHost.Shared.Persistence;

public class RunRepository : IRunRepository
{
    private readonly string _connectionString;

    public RunRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Run?> GetAsync(Guid id)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, pipeline_name, pipeline_spec, status, costs, created_at, updated_at 
            FROM runs 
            WHERE id = @id
            """;

        var row = await connection.QueryFirstOrDefaultAsync(sql, new { id });
        return row == null ? null : MapToRun(row);
    }

    public async Task<IEnumerable<Run>> ListAsync(int limit = 100, int offset = 0)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, pipeline_name, pipeline_spec, status, costs, created_at, updated_at 
            FROM runs 
            ORDER BY created_at DESC 
            LIMIT @limit OFFSET @offset
            """;

        var rows = await connection.QueryAsync(sql, new { limit, offset });
        return rows.Select(MapToRun);
    }

    public async Task CreateAsync(Run run)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            INSERT INTO runs (id, pipeline_name, pipeline_spec, status, costs, created_at, updated_at)
            VALUES (@Id, @PipelineName, @PipelineSpec::jsonb, @Status, @Costs::jsonb, @CreatedAt, @UpdatedAt)
            """;

        await connection.ExecuteAsync(sql, new
        {
            run.Id,
            run.PipelineName,
            PipelineSpec = JsonSerializer.Serialize(run.PipelineSpec),
            run.Status,
            Costs = JsonSerializer.Serialize(run.Costs),
            run.CreatedAt,
            run.UpdatedAt
        });
    }

    public async Task UpdateAsync(Run run)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            UPDATE runs 
            SET pipeline_spec = @PipelineSpec, status = @Status, costs = @Costs, updated_at = @UpdatedAt
            WHERE id = @Id
            """;

        await connection.ExecuteAsync(sql, new
        {
            run.Id,
            PipelineSpec = JsonSerializer.Serialize(run.PipelineSpec),
            run.Status,
            Costs = JsonSerializer.Serialize(run.Costs),
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    public async Task UpdateStatusAsync(Guid id, string status)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            UPDATE runs 
            SET status = @status, updated_at = @updatedAt
            WHERE id = @id
            """;

        await connection.ExecuteAsync(sql, new { id, status, updatedAt = DateTimeOffset.UtcNow });
    }

    private static Run MapToRun(dynamic row)
    {
        var pipelineSpec = JsonSerializer.Deserialize<object>((string)row.pipeline_spec) ?? new { };
        var costs = JsonSerializer.Deserialize<Dictionary<string, object>>((string)row.costs) ?? new Dictionary<string, object>();

        return new Run(
            (Guid)row.id,
            (string)row.pipeline_name,
            pipelineSpec,
            (string)row.status,
            costs,
            (DateTimeOffset)row.created_at,
            (DateTimeOffset)row.updated_at
        );
    }
}
