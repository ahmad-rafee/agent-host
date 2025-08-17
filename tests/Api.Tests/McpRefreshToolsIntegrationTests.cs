using System.Net.Http.Json;
using AgentHost.Shared.Persistence;
using FluentAssertions;
using Xunit;

namespace AgentHost.Api.Tests;

public class McpRefreshToolsIntegrationTests : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _client;

    public McpRefreshToolsIntegrationTests(ApiTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Refresh_Should_Create_Tools()
    {
        var create = new { name = $"srv_refresh_{Guid.NewGuid():N}", type = "stdio", displayName = "RefreshSrv", command = "echo", args = new[] {"--x"}, env = new Dictionary<string,string>()};
        var resp = await _client.PostAsJsonAsync("/mcp/servers", create);
        resp.EnsureSuccessStatusCode();
        var server = await resp.Content.ReadFromJsonAsync<McpServer>();
        server.Should().NotBeNull();
        var refresh = await _client.PostAsync($"/mcp/servers/{server!.Id}/refresh-tools", null);
        refresh.EnsureSuccessStatusCode();
        var tools = await refresh.Content.ReadFromJsonAsync<List<McpToolRecord>>();
        tools.Should().NotBeNull();
        tools!.Count.Should().BeGreaterThan(0);
    }
}
