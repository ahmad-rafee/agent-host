using FluentAssertions;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace AgentHost.Api.Tests;

public class ApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/healthz");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var healthResponse = JsonSerializer.Deserialize<JsonElement>(content);
        
        healthResponse.GetProperty("ok").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task CreateRun_WithValidPipeline_ShouldReturnCreated()
    {
        // Arrange
        var request = new
        {
            pipelineName = "test-pipeline",
            pipelineSpec = new { }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/runs", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var runResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
        
        runResponse.GetProperty("runId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetRun_WithInvalidId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/runs/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListRuns_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/runs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var runs = JsonSerializer.Deserialize<JsonElement[]>(content);
        
        runs.Should().NotBeNull();
    }
}
