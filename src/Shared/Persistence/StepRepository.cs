using System.Text.Json;
using AgentHost.Shared.Contracts;
using Dapper;
using Npgsql;

namespace AgentHost.Shared.Persistence;

public class StepRepository : IStepRepository
{
    private readonly string _connectionString;

    public StepRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Step?> GetAsync(Guid id)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, run_id, kind, status, input, output, attempt, timings, created_at, updated_at 
            FROM steps 
            WHERE id = @id
            """;

        var row = await connection.QueryFirstOrDefaultAsync(sql, new { id });
        return row == null ? null : MapToStep(row);
    }

    public async Task<IEnumerable<Step>> ListByRunAsync(Guid runId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, run_id, kind, status, input, output, attempt, timings, created_at, updated_at 
            FROM steps 
            WHERE run_id = @runId 
            ORDER BY created_at ASC
            """;

        var rows = await connection.QueryAsync(sql, new { runId });
        return rows.Select(MapToStep);
    }

    public async Task CreateAsync(Step step)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            INSERT INTO steps (id, run_id, kind, status, input, output, attempt, timings, created_at, updated_at)
            VALUES (@Id, @RunId, @Kind, @Status, @Input::jsonb, @Output::jsonb, @Attempt, @Timings::jsonb, @CreatedAt, @UpdatedAt)
            """;

        await connection.ExecuteAsync(sql, new
        {
            step.Id,
            step.RunId,
            step.Kind,
            step.Status,
            Input = step.Input != null ? JsonSerializer.Serialize(step.Input) : null,
            Output = step.Output != null ? JsonSerializer.Serialize(step.Output) : null,
            step.Attempt,
            Timings = JsonSerializer.Serialize(step.Timings),
            step.CreatedAt,
            step.UpdatedAt
        });
    }

    public async Task UpdateAsync(Step step)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            UPDATE steps 
            SET status = @Status, input = @Input::jsonb, output = @Output::jsonb, attempt = @Attempt, 
                timings = @Timings::jsonb, updated_at = @UpdatedAt
            WHERE id = @Id
            """;

        await connection.ExecuteAsync(sql, new
        {
            step.Id,
            step.Status,
            Input = step.Input != null ? JsonSerializer.Serialize(step.Input) : null,
            Output = step.Output != null ? JsonSerializer.Serialize(step.Output) : null,
            step.Attempt,
            Timings = JsonSerializer.Serialize(step.Timings),
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    public async Task UpdateStatusAsync(Guid id, string status)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            UPDATE steps 
            SET status = @status, updated_at = @updatedAt
            WHERE id = @id
            """;

        await connection.ExecuteAsync(sql, new { id, status, updatedAt = DateTimeOffset.UtcNow });
    }

    private static Step MapToStep(dynamic row)
    {
        object? input = null;
        object? output = null;
        
        if (row.input is string inputStr && !string.IsNullOrEmpty(inputStr))
        {
            input = JsonSerializer.Deserialize<object>(inputStr);
        }
        
        if (row.output is string outputStr && !string.IsNullOrEmpty(outputStr))
        {
            output = JsonSerializer.Deserialize<object>(outputStr);
        }

        var timings = JsonSerializer.Deserialize<Dictionary<string, object>>((string)row.timings) 
                     ?? new Dictionary<string, object>();

        return new Step(
            (Guid)row.id,
            (Guid)row.run_id,
            (string)row.kind,
            (string)row.status,
            input,
            output,
            (int)row.attempt,
            timings,
            (DateTimeOffset)row.created_at,
            (DateTimeOffset)row.updated_at
        );
    }
}
