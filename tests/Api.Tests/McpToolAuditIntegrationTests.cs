using System.Net.Http.Json;
using AgentHost.Shared.Persistence;
using FluentAssertions;
using Xunit;
using Npgsql;

namespace AgentHost.Api.Tests;

public class McpToolAuditIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public McpToolAuditIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Refresh_Should_Log_Audit_Entries()
    {
        var create = new { name = $"srv_audit_{Guid.NewGuid():N}", type = "stdio", displayName = "CHANGING", command = "echo", args = new[]{"--x"}, env = new Dictionary<string,string>()};
        var resp = await _client.PostAsJsonAsync("/mcp/servers", create);
        resp.EnsureSuccessStatusCode();
        var server = await resp.Content.ReadFromJsonAsync<McpServer>();
        server.Should().NotBeNull();
        var id = server!.Id;

        await using (var conn = new NpgsqlConnection("Host=db;Database=postgres;Username=postgres;Password=postgres"))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM mcp_tools WHERE server_id = @sid", conn);
            cmd.Parameters.AddWithValue("sid", id);
            await cmd.ExecuteNonQueryAsync();
        }

        await _client.PostAsync($"/mcp/servers/{id}/refresh-tools", null); // adds
        await _client.PostAsync($"/mcp/servers/{id}/refresh-tools", null); // update
        await _client.PostAsync($"/mcp/servers/{id}/refresh-tools", null); // delete

        var audit = await _client.GetFromJsonAsync<List<McpToolAuditEntry>>($"/mcp/servers/{id}/tool-audit");
    audit.Should().NotBeNull();
    audit!.Any(e => e.Action == "Added").Should().BeTrue();
    audit.Any(e => e.Action == "Updated").Should().BeTrue();
    // Deleted may be soft-deleted among many other tools; ensure at least one Deleted for time
    audit.Any(e => e.Action == "Deleted" && e.ToolName == "time").Should().BeTrue();
    }
}
