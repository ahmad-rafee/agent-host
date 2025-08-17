using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace AgentHost.Api.Tests;

public class ArtifactIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ArtifactIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateArtifact_ShouldReturnCreated()
    {
        var req = new { source = "unit-test", type = "note", uri = "memory://note/1", labels = new { a = 1 } };
        var json = JsonSerializer.Serialize(req);
        var response = await _client.PostAsync("/artifacts", new StringContent(json, Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        doc.GetProperty("id").GetGuid().Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GetArtifact_InvalidId_ShouldReturnNotFound()
    {
        var response = await _client.GetAsync($"/artifacts/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListArtifacts_ShouldIncludeCreated()
    {
        var req = new { source = "list-test", type = "note", uri = "memory://note/2" };
        await _client.PostAsync("/artifacts", new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json"));

        var response = await _client.GetAsync("/artifacts");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var arr = JsonSerializer.Deserialize<JsonElement[]>(await response.Content.ReadAsStringAsync());
        arr!.Any().Should().BeTrue();
    }
}
