using System.Net;
using System.Text;
using System.Text.Json;
using AgentHost.Shared.Contracts;
using AgentHost.Shared.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentHost.Api.Tests;

public class RunLifecycleTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly IServiceProvider _services;

    public RunLifecycleTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _services = factory.Services;
    }

    [Fact]
    public async Task Run_WithStep_ShouldReturnStepInDetails()
    {
    var createReq = new { pipelineName = "test-pipeline", pipelineSpec = new { steps = Array.Empty<object>() } };
        var createResp = await _client.PostAsync("/runs", new StringContent(JsonSerializer.Serialize(createReq), Encoding.UTF8, "application/json"));
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var runJson = JsonSerializer.Deserialize<JsonElement>(await createResp.Content.ReadAsStringAsync());
        var runId = runJson.GetProperty("runId").GetGuid();

        using var scope = _services.CreateScope();
        var stepRepo = scope.ServiceProvider.GetRequiredService<IStepRepository>();
        var step = new Step(
            Id: Guid.NewGuid(),
            RunId: runId,
            Kind: StepKind.Transform,
            Status: StepStatus.Completed,
            Input: new { value = 1 },
            Output: new { value = 2 },
            Attempt: 1,
            Timings: new Dictionary<string, object>{{"duration_ms", 5}},
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);
        await stepRepo.CreateAsync(step);

        var detailsResp = await _client.GetAsync($"/runs/{runId}");
        detailsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailsJson = JsonSerializer.Deserialize<JsonElement>(await detailsResp.Content.ReadAsStringAsync());
        var steps = detailsJson.GetProperty("steps").EnumerateArray().ToList();
        steps.Should().HaveCountGreaterThanOrEqualTo(1);
        steps.Any(s => s.GetProperty("id").GetGuid() == step.Id).Should().BeTrue();
    }
}
