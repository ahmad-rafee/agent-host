using System.Net.Http.Json;
using AgentHost.Shared.Persistence;
using FluentAssertions;
using Xunit;

namespace AgentHost.Api.Tests;

public class McpToolsPaginationFilteringTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public McpToolsPaginationFilteringTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task List_Should_Filter_And_Paginate()
    {
        var create = new { name = $"srv_tools_{Guid.NewGuid():N}", type = "stdio", displayName = "ToolsSrv", command = "echo", args = Array.Empty<string>(), env = new Dictionary<string,string>()};
        var resp = await _client.PostAsJsonAsync("/mcp/servers", create);
        resp.EnsureSuccessStatusCode();
        var server = await resp.Content.ReadFromJsonAsync<McpServer>();
        server.Should().NotBeNull();
        var serverId = server!.Id;

        var toolsPayload = new {
            tools = Enumerable.Range(0, 10).Select(i => new {
                name = $"tool{i}", description = $"Tool {i}", schema = new { type = "object" }, requiredScopes = Array.Empty<string>(), version = (string?)null
            }).ToArray()
        };
        var put = await _client.PutAsJsonAsync($"/mcp/servers/{serverId}/tools", toolsPayload);
        put.EnsureSuccessStatusCode();

        var page1 = await _client.GetFromJsonAsync<List<McpToolRecord>>($"/mcp/servers/{serverId}/tools?limit=3&offset=0");
        page1!.Count.Should().Be(3);
        var page2 = await _client.GetFromJsonAsync<List<McpToolRecord>>($"/mcp/servers/{serverId}/tools?limit=3&offset=3");
        page2!.Count.Should().Be(3);
        var filtered = await _client.GetFromJsonAsync<List<McpToolRecord>>($"/mcp/servers/{serverId}/tools?namePrefix=tool1");
        filtered!.Select(t => t.Name).Should().BeEquivalentTo(new[]{"tool1"});
    }
}
