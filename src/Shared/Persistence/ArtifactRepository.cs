using System.Text.Json;
using AgentHost.Shared.Contracts;
using Dapper;
using Npgsql;

namespace AgentHost.Shared.Persistence;

public class ArtifactRepository : IArtifactRepository
{
    private readonly string _connectionString;

    public ArtifactRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Artifact?> GetAsync(Guid id)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, source, type, uri, hash, labels, created_at 
            FROM artifacts 
            WHERE id = @id
            """;

        var row = await connection.QueryFirstOrDefaultAsync(sql, new { id });
        return row == null ? null : MapToArtifact(row);
    }

    public async Task<IEnumerable<Artifact>> ListAsync(int limit = 100, int offset = 0)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            SELECT id, source, type, uri, hash, labels, created_at 
            FROM artifacts 
            ORDER BY created_at DESC 
            LIMIT @limit OFFSET @offset
            """;

        var rows = await connection.QueryAsync(sql, new { limit, offset });
        return rows.Select(MapToArtifact);
    }

    public async Task CreateAsync(Artifact artifact)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            INSERT INTO artifacts (id, source, type, uri, hash, labels, created_at)
            VALUES (@Id, @Source, @Type, @Uri, @Hash, @Labels::jsonb, @CreatedAt)
            """;

        await connection.ExecuteAsync(sql, new
        {
            artifact.Id,
            artifact.Source,
            artifact.Type,
            artifact.Uri,
            artifact.Hash,
            Labels = JsonSerializer.Serialize(artifact.Labels),
            artifact.CreatedAt
        });
    }

    public async Task UpdateAsync(Artifact artifact)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = """
            UPDATE artifacts 
            SET source = @Source, type = @Type, uri = @Uri, hash = @Hash, labels = @Labels::jsonb
            WHERE id = @Id
            """;

        await connection.ExecuteAsync(sql, new
        {
            artifact.Id,
            artifact.Source,
            artifact.Type,
            artifact.Uri,
            artifact.Hash,
            Labels = JsonSerializer.Serialize(artifact.Labels)
        });
    }

    private static Artifact MapToArtifact(dynamic row)
    {
        var labels = JsonSerializer.Deserialize<Dictionary<string, object>>((string)row.labels) 
                    ?? new Dictionary<string, object>();

        return new Artifact(
            (Guid)row.id,
            (string)row.source,
            (string)row.type,
            (string)row.uri,
            (string?)row.hash,
            labels,
            (DateTimeOffset)row.created_at
        );
    }
}
