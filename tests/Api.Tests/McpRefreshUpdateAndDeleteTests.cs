using System.Net.Http.Json;
using AgentHost.Shared.Persistence;
using FluentAssertions;
using Xunit;
using Npgsql;

namespace AgentHost.Api.Tests;

public class McpRefreshUpdateAndDeleteTests : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _client;

    public McpRefreshUpdateAndDeleteTests(ApiTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Refresh_Should_Update_And_SoftDelete()
    {
        var create = new { name = $"srv_refresh_{Guid.NewGuid():N}", type = "stdio", displayName = "CHANGING", command = "echo", args = new[] {"--x"}, env = new Dictionary<string,string>()};
        var resp = await _client.PostAsJsonAsync("/mcp/servers", create);
        resp.EnsureSuccessStatusCode();
        var serverCreated = await resp.Content.ReadFromJsonAsync<McpServer>();
        serverCreated.Should().NotBeNull();
        var serverId = serverCreated!.Id;

        await using (var conn = new NpgsqlConnection("Host=db;Database=postgres;Username=postgres;Password=postgres"))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM mcp_tools WHERE server_id = @sid", conn);
            cmd.Parameters.AddWithValue("sid", serverId);
            await cmd.ExecuteNonQueryAsync();
        }

        // 1st refresh - two tools
        var r1 = await _client.PostAsync($"/mcp/servers/{serverId}/refresh-tools", null);
        r1.EnsureSuccessStatusCode();
    var tools1 = await r1.Content.ReadFromJsonAsync<List<McpToolRecord>>();
    tools1.Should().NotBeNull();
    tools1!.Any(t => t.Name == "echo").Should().BeTrue();
    tools1!.Any(t => t.Name == "time").Should().BeTrue();
    var echoV1 = tools1!.Single(t => t.Name == "echo");
        echoV1.Version.Should().Be("1.0");

        // 2nd refresh - echo updated to v2
        var r2 = await _client.PostAsync($"/mcp/servers/{serverId}/refresh-tools", null);
        r2.EnsureSuccessStatusCode();
    var tools2 = await r2.Content.ReadFromJsonAsync<List<McpToolRecord>>();
    tools2!.Any(t => t.Name == "echo").Should().BeTrue();
    tools2!.Any(t => t.Name == "time").Should().BeTrue();
    var echoV2 = tools2!.Single(t => t.Name == "echo");
        echoV2.Version.Should().Be("2.0");
        echoV2.UpdatedAt.Should().BeAfter(echoV1.UpdatedAt);

        // 3rd refresh - time removed (soft deleted)
        var r3 = await _client.PostAsync($"/mcp/servers/{serverId}/refresh-tools", null);
        r3.EnsureSuccessStatusCode();
    var tools3 = await r3.Content.ReadFromJsonAsync<List<McpToolRecord>>();
    tools3!.Any(t => t.Name == "echo").Should().BeTrue();
    tools3!.Any(t => t.Name == "time").Should().BeFalse();

        // List including deleted
        var listAll = await _client.GetAsync($"/mcp/servers/{serverId}/tools?includeDeleted=true");
        listAll.EnsureSuccessStatusCode();
        var allTools = await listAll.Content.ReadFromJsonAsync<List<McpToolRecord>>();
    var deletedTool = allTools!.Single(t => t.Name == "time");
        deletedTool.IsDeleted.Should().BeTrue();
    }
}
