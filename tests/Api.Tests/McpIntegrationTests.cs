using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using AgentHost.Shared.Contracts;
using Xunit;

namespace AgentHost.Api.Tests;

public class McpIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public McpIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_And_List_Server_Flow()
    {
        var create = new McpServerCreateRequest(
            Name: $"srv_{Guid.NewGuid():N}".Substring(0, 12),
            Type: "stdio",
            DisplayName: "Test Server",
            Command: "echo",
            Args: new []{"--flag"},
            Endpoint: null,
            Env: new Dictionary<string,string>{{"K","V"}},
            IsEnabled: true);

        var resp = await _client.PostAsJsonAsync("/mcp/servers", create);
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdJson = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
        var id = createdJson.GetProperty("id").GetGuid();

        var listResp = await _client.GetAsync("/mcp/servers");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var servers = JsonDocument.Parse(await listResp.Content.ReadAsStringAsync()).RootElement;
        servers.EnumerateArray().Any(e => e.GetProperty("id").GetGuid() == id).Should().BeTrue();

        var getResp = await _client.GetAsync($"/mcp/servers/{id}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var server = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync()).RootElement;
        server.GetProperty("name").GetString().Should().StartWith("srv_");
    }

    [Fact]
    public async Task Update_Status_And_Tools_Flow()
    {
        // create server
        var create = new McpServerCreateRequest(
            Name: $"srv_{Guid.NewGuid():N}".Substring(0, 12),
            Type: "http",
            DisplayName: null,
            Command: null,
            Args: Array.Empty<string>(),
            Endpoint: "http://localhost:1234",
            Env: null,
            IsEnabled: true);
        var resp = await _client.PostAsJsonAsync("/mcp/servers", create);
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        // update status
        var statusPayload = new McpServerStatusUpdateRequest("ready", null);
        var statusResp = await _client.PatchAsJsonAsync($"/mcp/servers/{id}/status", statusPayload);
        statusResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // upsert tools
        var toolsPayload = new McpToolUpsertManyRequest(new []{
            new McpToolUpsertRequest("alpha.tool", "Alpha", new { a = 1 }, new[]{"scope.a"}, "v1"),
            new McpToolUpsertRequest("beta.tool", "Beta", new { b = 2 }, Array.Empty<string>(), null)
        });
        var upsertResp = await _client.PutAsJsonAsync($"/mcp/servers/{id}/tools", toolsPayload);
        upsertResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var tools = JsonDocument.Parse(await upsertResp.Content.ReadAsStringAsync()).RootElement;
        tools.EnumerateArray().Count().Should().Be(2);

        // list tools
        var listTools = await _client.GetAsync($"/mcp/servers/{id}/tools");
        listTools.StatusCode.Should().Be(HttpStatusCode.OK);
        var listed = JsonDocument.Parse(await listTools.Content.ReadAsStringAsync()).RootElement;
        listed.EnumerateArray().Any(e => e.GetProperty("name").GetString() == "alpha.tool").Should().BeTrue();
    }

    [Fact]
    public async Task Create_Server_Duplicate_Name_Should_Conflict()
    {
        var name = $"srv_{Guid.NewGuid():N}".Substring(0, 12);
        var req = new McpServerCreateRequest(name, "stdio");
        var first = await _client.PostAsJsonAsync("/mcp/servers", req);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var second = await _client.PostAsJsonAsync("/mcp/servers", req);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task List_Servers_With_Enabled_Filter_Should_Exclude_Disabled()
    {
        // create one enabled and one disabled
        var enabledReq = new McpServerCreateRequest($"srv_{Guid.NewGuid():N}".Substring(0, 12), "stdio", IsEnabled: true);
        var disabledReq = new McpServerCreateRequest($"srv_{Guid.NewGuid():N}".Substring(0, 12), "stdio", IsEnabled: false);
        (await _client.PostAsJsonAsync("/mcp/servers", enabledReq)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await _client.PostAsJsonAsync("/mcp/servers", disabledReq)).StatusCode.Should().Be(HttpStatusCode.Created);

        var resp = await _client.GetAsync("/mcp/servers?enabled=true");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var servers = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
        servers.EnumerateArray().Should().OnlyContain(e => e.GetProperty("isEnabled").GetBoolean());
    }
}
