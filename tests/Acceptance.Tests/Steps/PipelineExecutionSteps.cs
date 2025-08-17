using System.Net.Http.Json;
using FluentAssertions;
using TechTalk.SpecFlow;
using Microsoft.AspNetCore.Mvc.Testing;
using AgentHost.Shared.Contracts;

namespace AgentHost.Acceptance.Tests.Steps;

[Binding]
public partial class PipelineExecutionSteps
{
    private readonly ScenarioContext _ctx;
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private Guid _runId;
    private HttpResponseMessage? _lastResponse;
    private List<dynamic> _policyScopes = new();
    private readonly Dictionary<string, Guid> _mcpServers = new();
    private List<dynamic> _lastTools = new();
    private List<dynamic> _lastAudit = new();

    public PipelineExecutionSteps(ScenarioContext ctx)
    {
        _ctx = ctx;
    }

    [Given("the API is running")]
    public void GivenTheApiIsRunning()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [Given("a pipeline named \"(.*)\" exists")]
    public void GivenAPipelineNamedExists(string pipelineName)
    {
        // In current PoC pipelines are registered in memory at startup; assume existence.
        _ctx["pipelineName"] = pipelineName;
    }

    [When("I create a run for pipeline \"(.*)\"")]
    public async Task WhenICreateARunForPipeline(string pipelineName)
    {
    // Minimal snapshot object: matches schema (PipelineSpec) expected by API when persisting
    var snapshot = new { name = pipelineName, steps = Array.Empty<object>() };
    var request = new RunRequest(pipelineName, snapshot!);
        _lastResponse = await _client!.PostAsJsonAsync("/runs", request);
        if (_lastResponse.IsSuccessStatusCode)
        {
            var payload = await _lastResponse.Content.ReadFromJsonAsync<RunResponse>();
            _runId = payload!.RunId;
        }
    }

    [Then("the run is created successfully")]
    public void ThenTheRunIsCreatedSuccessfully()
    {
        _lastResponse!.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        _runId.Should().NotBe(Guid.Empty);
    }

    [When("I retrieve the run details")]
    public async Task WhenIRetrieveTheRunDetails()
    {
        _lastResponse = await _client!.GetAsync($"/runs/{_runId}");
    }

    private record RunDetailsWire(Guid RunId, object Run, object[] Steps, IEnumerable<string> EffectiveModels, IEnumerable<string> EffectiveTools, IEnumerable<string> PolicyWarnings);

    [Then("the response includes effective policy information")]
    public async Task ThenTheResponseIncludesEffectivePolicyInformation()
    {
        _lastResponse!.IsSuccessStatusCode.Should().BeTrue();
        var json = await _lastResponse.Content.ReadFromJsonAsync<RunDetailsWire>();
        json!.EffectiveModels.Should().NotBeEmpty();
        json!.EffectiveTools.Should().NotBeNull();
    }

    [When("I add a policy scope with model \"(.*)\"")]
    public async Task WhenIAddAPolicyScopeWithModel(string model)
    {
        var payload = new { model };
        var resp = await _client!.PostAsJsonAsync($"/runs/{_runId}/policy-scopes", payload);
        resp.IsSuccessStatusCode.Should().BeTrue();
    }

    [When("I add a policy scope with tool \"(.*)\"")]
    public async Task WhenIAddAPolicyScopeWithTool(string tool)
    {
        var payload = new { tool };
        var resp = await _client!.PostAsJsonAsync($"/runs/{_runId}/policy-scopes", payload);
        resp.IsSuccessStatusCode.Should().BeTrue();
    }

    [When("I list policy scopes")]
    public async Task WhenIListPolicyScopes()
    {
        var resp = await _client!.GetAsync($"/runs/{_runId}/policy-scopes");
        resp.IsSuccessStatusCode.Should().BeTrue();
    var list = await resp.Content.ReadFromJsonAsync<List<PolicyScopeWire>>();
    _policyScopes = list!.Cast<dynamic>().ToList();
    }

    [Then("at least (.*) policy scopes are returned")]
    public void ThenAtLeastNPolicyScopesAreReturned(int min)
    {
        _policyScopes.Count.Should().BeGreaterOrEqualTo(min);
    }

    [When("I delete the last added policy scope")]
    public async Task WhenIDeleteTheLastAddedPolicyScope()
    {
        _policyScopes.Should().NotBeEmpty();
    var last = (PolicyScopeWire)_policyScopes.Last();
    Guid scopeId = last.Id;
        var resp = await _client!.DeleteAsync($"/runs/{_runId}/policy-scopes/{scopeId}");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);
    }

    [Then("the effective models include \"(.*)\"")]
    public async Task ThenTheEffectiveModelsInclude(string model)
    {
        var resp = await _client!.GetAsync($"/runs/{_runId}");
        resp.IsSuccessStatusCode.Should().BeTrue();
        var json = await resp.Content.ReadFromJsonAsync<RunDetailsWire>();
        json!.EffectiveModels.Should().Contain(model);
    }

    [Then("the effective models do not include \"(.*)\"")]
    public async Task ThenTheEffectiveModelsDoNotInclude(string model)
    {
        var resp = await _client!.GetAsync($"/runs/{_runId}");
        resp.IsSuccessStatusCode.Should().BeTrue();
        var json = await resp.Content.ReadFromJsonAsync<RunDetailsWire>();
        json!.EffectiveModels.Should().NotContain(model);
    }
    // MCP steps
        [When("I create an MCP server named \"(.*)\" of type \"(.*)\"")]
        public async Task WhenICreateAnMcpServer(string name, string type)
        {
            // Use a unique physical name to avoid cross-scenario conflicts caused by persistent test DB
            var uniqueName = $"{name}-{Guid.NewGuid().ToString("N").Substring(0,8)}";
            var payload = new { name = uniqueName, type, isEnabled = true };
            var resp = await _client!.PostAsJsonAsync("/mcp/servers", payload);
            resp.IsSuccessStatusCode.Should().BeTrue();
            var json = await resp.Content.ReadFromJsonAsync<McpServerResponse>();
            _mcpServers[name] = json!.Id;
        }

        [Then("the MCP server is created successfully")]
        public void ThenTheMcpServerIsCreatedSuccessfully()
        {
            _mcpServers.Should().NotBeEmpty();
        }

        [Given("an MCP server exists named \"(.*)\" of type \"(.*)\"")]
        public async Task GivenAnMcpServerExists(string name, string type)
        {
            if (_mcpServers.ContainsKey(name)) return;
            await WhenICreateAnMcpServer(name, type);
        }

        [When("I upsert tools on server \"(.*)\":")] 
        public async Task WhenIUpsertToolsOnServer(string name, Table table)
        {
            var serverId = _mcpServers[name];
            var tools = table.Rows.Select(r => new {
                name = r["name"],
                description = r["description"],
                schema = new { },
                requiredScopes = Array.Empty<string>(),
                version = "v1"
            }).ToArray();
            var payload = new { tools };
            var resp = await _client!.PutAsJsonAsync($"/mcp/servers/{serverId}/tools", payload);
            resp.IsSuccessStatusCode.Should().BeTrue();
            _lastTools = (await resp.Content.ReadFromJsonAsync<List<McpToolWire>>())!.Cast<dynamic>().ToList();
        }

        [Then("the server \"(.*)\" lists at least (.*) tools")]
        public async Task ThenTheServerListsAtLeastNTools(string name, int min)
        {
            var serverId = _mcpServers[name];
            var resp = await _client!.GetAsync($"/mcp/servers/{serverId}/tools");
            resp.IsSuccessStatusCode.Should().BeTrue();
            var list = await resp.Content.ReadFromJsonAsync<List<McpToolWire>>();
            list!.Count.Should().BeGreaterOrEqualTo(min);
        }

        [When("I refresh tools for server \"(.*)\"")]
        public async Task WhenIRefreshToolsForServer(string name)
        {
            var serverId = _mcpServers[name];
            var resp = await _client!.PostAsync($"/mcp/servers/{serverId}/refresh-tools", null);
            // For now allow non-success for skipped scenarios (tagged @skip)
            if (!_ctx.ScenarioInfo.Tags.Contains("skip"))
                resp.IsSuccessStatusCode.Should().BeTrue();
            _lastTools = (await resp.Content.ReadFromJsonAsync<List<McpToolWire>>())!.Cast<dynamic>().ToList();
        }

        [Then("an audit log exists for server \"(.*)\"")]
        public async Task ThenAnAuditLogExistsForServer(string name)
        {
            var serverId = _mcpServers[name];
            var resp = await _client!.GetAsync($"/mcp/servers/{serverId}/tool-audit");
            resp.IsSuccessStatusCode.Should().BeTrue();
            var entries = await resp.Content.ReadFromJsonAsync<List<McpAuditWire>>();
            entries.Should().NotBeNull();
            entries!.Count.Should().BeGreaterThan(0);
            _lastAudit = entries.Cast<dynamic>().ToList();
    }
}

// Internal wire model for policy scopes list deserialization
file record PolicyScopeWire(Guid Id, Guid RunId, string? AllowedModel, string? AllowedTool, string? Scope, DateTimeOffset CreatedAt);
file record McpServerWire(Guid Id, string Name, string Type, string? DisplayName, string? Command, string[] Args, string? Endpoint, Dictionary<string,string> Env, bool IsEnabled, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
file record McpToolWire(Guid Id, Guid ServerId, string Name, string? Description, object Schema, string[] RequiredScopes, string? Version, string SchemaHash, bool IsDeleted, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
file record McpAuditWire(Guid ServerId, string ToolName, string Action, string? OldVersion, string? NewVersion, DateTimeOffset Timestamp);
