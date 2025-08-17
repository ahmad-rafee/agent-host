using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace AgentHost.Api.Tests;

public class PolicyScopeIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PolicyScopeIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AddPolicyScope_ShouldReflectInRunEffectivePolicy()
    {
        var createReq = new { pipelineName = "test-pipeline", pipelineSpec = new { } };
        var resp = await _client.PostAsync("/runs", new StringContent(JsonSerializer.Serialize(createReq), Encoding.UTF8, "application/json"));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var runJson = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        var runId = runJson.GetProperty("runId").GetGuid();

        var scopeReq = new { model = "custom-model-x" };
        var scopeResp = await _client.PostAsync($"/runs/{runId}/policy-scopes", new StringContent(JsonSerializer.Serialize(scopeReq), Encoding.UTF8, "application/json"));
        scopeResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var detailsResp = await _client.GetAsync($"/runs/{runId}");
        detailsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailsJson = JsonSerializer.Deserialize<JsonElement>(await detailsResp.Content.ReadAsStringAsync());

        detailsJson.TryGetProperty("effectiveModels", out var effModels).Should().BeTrue();
        effModels.EnumerateArray().Any(e => e.GetString() == "custom-model-x").Should().BeTrue();
    }

    [Fact]
    public async Task ListAndDeletePolicyScopes_ShouldWork()
    {
        var createReq = new { pipelineName = "test-pipeline", pipelineSpec = new { } };
        var resp = await _client.PostAsync("/runs", new StringContent(JsonSerializer.Serialize(createReq), Encoding.UTF8, "application/json"));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var runJson = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
        var runId = runJson.GetProperty("runId").GetGuid();

        // Add two scopes
        foreach (var model in new[] { "m-a", "m-b" })
        {
            var scopeReq = new { model };
            var scopeResp = await _client.PostAsync($"/runs/{runId}/policy-scopes", new StringContent(JsonSerializer.Serialize(scopeReq), Encoding.UTF8, "application/json"));
            scopeResp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var listResp = await _client.GetAsync($"/runs/{runId}/policy-scopes");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var listJson = JsonSerializer.Deserialize<JsonElement>(await listResp.Content.ReadAsStringAsync());
        listJson.GetArrayLength().Should().BeGreaterOrEqualTo(2);
        var firstScopeId = listJson[0].GetProperty("id").GetGuid();

        var deleteResp = await _client.DeleteAsync($"/runs/{runId}/policy-scopes/{firstScopeId}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResp2 = await _client.GetAsync($"/runs/{runId}/policy-scopes");
        listResp2.StatusCode.Should().Be(HttpStatusCode.OK);
        var listJson2 = JsonSerializer.Deserialize<JsonElement>(await listResp2.Content.ReadAsStringAsync());
        listJson2.EnumerateArray().Any(e => e.GetProperty("id").GetGuid() == firstScopeId).Should().BeFalse();
    }
}
