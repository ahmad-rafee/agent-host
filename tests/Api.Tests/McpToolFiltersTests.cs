using System.Net.Http.Json;
using AgentHost.Shared.Persistence;
using FluentAssertions;
using Xunit;
using Npgsql;

namespace AgentHost.Api.Tests;

public class McpToolFiltersTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    public McpToolFiltersTests(CustomWebApplicationFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task DeletedOnly_Should_Return_Only_Deleted()
    {
        var create = new { name = $"srv_filter_{Guid.NewGuid():N}", type = "stdio", displayName = "CHANGING", command = "echo", args = Array.Empty<string>(), env = new Dictionary<string,string>()};
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

        await _client.PostAsync($"/mcp/servers/{id}/refresh-tools", null);
        await _client.PostAsync($"/mcp/servers/{id}/refresh-tools", null);
        await _client.PostAsync($"/mcp/servers/{id}/refresh-tools", null);

    var deleted = await _client.GetFromJsonAsync<List<McpToolRecord>>($"/mcp/servers/{id}/tools?deletedOnly=true");
    deleted.Should().NotBeNull();
    deleted!.Any(t => t.IsDeleted && t.Name == "time").Should().BeTrue();
    }

    [Fact]
    public async Task Version_Filter_Should_Work()
    {
        var create = new { name = $"srv_version_{Guid.NewGuid():N}", type = "stdio", displayName = "CHANGING", command = "echo", args = Array.Empty<string>(), env = new Dictionary<string,string>()};
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

        await _client.PostAsync($"/mcp/servers/{id}/refresh-tools", null); // v1
        await _client.PostAsync($"/mcp/servers/{id}/refresh-tools", null); // v2 update

    var v2 = await _client.GetFromJsonAsync<List<McpToolRecord>>($"/mcp/servers/{id}/tools?version=2.0");
    v2.Should().NotBeNull();
    v2!.Any(t => t.Name == "echo" && t.Version == "2.0").Should().BeTrue();
    }
}
