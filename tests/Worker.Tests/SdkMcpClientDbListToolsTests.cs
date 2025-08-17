using AgentHost.Shared.Clients.McpClient;
using AgentHost.Shared.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace AgentHost.Worker.Tests;

public class SdkMcpClientDbListToolsTests
{
    private readonly Mock<ILogger<SdkMcpClient>> _logger = new();
    private readonly Mock<IConfiguration> _config = new();
    private readonly Mock<IMcpServerRepository> _serverRepo = new();
    private readonly Mock<IMcpToolRepository> _toolRepo = new();

    public SdkMcpClientDbListToolsTests()
    {
        var mcpSection = new Mock<IConfigurationSection>();
        _config.Setup(c => c.GetSection("Mcp")).Returns(mcpSection.Object);
    }

    [Fact]
    public async Task ListToolsAsync_ReturnsDatabaseTools_WhenRepositoriesProvided()
    {
        // Arrange: one enabled server and two tools
        var serverId = Guid.NewGuid();
        _serverRepo.Setup(r => r.ListAsync(true, It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(new[]
        {
            new McpServer(serverId, "srv", "stdio", null, null, Array.Empty<string>(), null, new Dictionary<string,string>(), true, "ready", null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        });

        var toolRecords = new[]
        {
            new McpToolRecord(Guid.NewGuid(), serverId, "db.tool.one", "Desc 1", new { type="object" }, new[]{"scope.a"}, "1.0", null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new McpToolRecord(Guid.NewGuid(), serverId, "db.tool.two", "Desc 2", new { type="object" }, Array.Empty<string>(), null, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        };
        _toolRepo.Setup(r => r.ListByServerAsync(serverId, false, null, null, false, It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(toolRecords);

        var client = new SdkMcpClient(_config.Object, _logger.Object, _serverRepo.Object, _toolRepo.Object);

        // Act
        var tools = (await client.ListToolsAsync()).ToList();

        // Assert
        tools.Should().HaveCount(2);
        tools.Select(t => t.Name).Should().BeEquivalentTo("db.tool.one", "db.tool.two");
        tools.All(t => t.Schema.ValueKind == JsonValueKind.Object).Should().BeTrue();
    }
}
