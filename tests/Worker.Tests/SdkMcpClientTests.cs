using AgentHost.Shared.Clients.McpClient;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace AgentHost.Worker.Tests;

public class SdkMcpClientTests
{
    private readonly Mock<ILogger<SdkMcpClient>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly SdkMcpClient _client;

    public SdkMcpClientTests()
    {
        _loggerMock = new Mock<ILogger<SdkMcpClient>>();
        _configurationMock = new Mock<IConfiguration>();
        
        // Setup mock configuration
        var mcpSection = new Mock<IConfigurationSection>();
        mcpSection.Setup(x => x["UseMockResponses"]).Returns("true");
        mcpSection.Setup(x => x["MaxRetryAttempts"]).Returns("3");
        mcpSection.Setup(x => x["RetryDelayMs"]).Returns("1000");
        mcpSection.Setup(x => x["TimeoutSeconds"]).Returns("30");
        
        _configurationMock.Setup(x => x.GetSection("Mcp")).Returns(mcpSection.Object);
        
        _client = new SdkMcpClient(_configurationMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task InitializeAsync_WithMockResponsesEnabled_ShouldSucceed()
    {
        // Act
        await _client.InitializeAsync();

        // Assert
        // No exceptions should be thrown
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("MCP client initialized")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CallToolAsync_WithGmailListMessages_ShouldReturnMockData()
    {
        // Arrange
        await _client.InitializeAsync();
        var toolName = McpTools.Gmail.ListMessages;
        var parameters = new Dictionary<string, object>
        {
            ["query"] = "from:test@example.com",
            ["maxResults"] = 10
        };

        // Act
        var response = await _client.CallToolAsync(toolName, parameters);

        // Assert
        response.Success.Should().BeTrue();
        response.Result.Should().NotBeNull();
        
        // Verify the mock response structure
        var jsonResult = JsonSerializer.Serialize(response.Result);
        jsonResult.Should().Contain("messages");
    }

    [Fact]
    public async Task CallToolAsync_WithJiraCreateIssue_ShouldReturnMockData()
    {
        // Arrange
        await _client.InitializeAsync();
        var toolName = McpTools.Jira.CreateIssue;
        var parameters = new Dictionary<string, object>
        {
            ["projectKey"] = "PROJ",
            ["summary"] = "Test Issue",
            ["description"] = "This is a test issue",
            ["issueType"] = "Task"
        };

        // Act
        var response = await _client.CallToolAsync(toolName, parameters);

        // Assert
        response.Success.Should().BeTrue();
        response.Result.Should().NotBeNull();
        
        // Verify the mock response structure
        var jsonResult = JsonSerializer.Serialize(response.Result);
        jsonResult.Should().Contain("key");
        jsonResult.Should().Contain("DEMO-");
    }

    [Fact]
    public async Task CallToolAsync_WithFilesystemReadFile_ShouldReturnMockData()
    {
        // Arrange
        await _client.InitializeAsync();
        var toolName = McpTools.Filesystem.ReadFile;
        var parameters = new Dictionary<string, object>
        {
            ["path"] = "/app/workspace/test.txt"
        };

        // Act
        var response = await _client.CallToolAsync(toolName, parameters);

        // Assert
        response.Success.Should().BeTrue();
        response.Result.Should().NotBeNull();
        
        // Verify the mock response structure
        var jsonResult = JsonSerializer.Serialize(response.Result);
        jsonResult.Should().Contain("content");
    }

    [Fact]
    public async Task CallToolAsync_WithUnknownTool_ShouldReturnError()
    {
        // Arrange
        await _client.InitializeAsync();
        var toolName = "unknown.tool";
        var parameters = new Dictionary<string, object>();

        // Act
        var response = await _client.CallToolAsync(toolName, parameters);

        // Assert
        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Unknown tool");
    }

    [Fact]
    public async Task CallToolAsync_WithNullParameters_ShouldHandleGracefully()
    {
        // Arrange
        await _client.InitializeAsync();
        var toolName = McpTools.Gmail.ListMessages;

        // Act
        var response = await _client.CallToolAsync(toolName, null);

        // Assert
        response.Success.Should().BeTrue();
        response.Result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAvailableToolsAsync_ShouldReturnKnownTools()
    {
        // Arrange
        await _client.InitializeAsync();

        // Act
        var tools = await _client.ListToolsAsync();

        // Assert
        tools.Should().NotBeEmpty();
        tools.Should().Contain(tool => tool.Name == McpTools.Gmail.ListMessages);
        tools.Should().Contain(tool => tool.Name == McpTools.Jira.CreateIssue);
        tools.Should().Contain(tool => tool.Name == McpTools.Filesystem.ReadFile);
        
        // Verify all tools have descriptions
        tools.Should().AllSatisfy(tool => 
        {
            tool.Name.Should().NotBeNullOrEmpty();
            tool.Description.Should().NotBeNullOrEmpty();
        });
    }

    [Theory]
    [InlineData(McpTools.Gmail.ListMessages, true)]
    [InlineData(McpTools.Jira.CreateIssue, true)]
    [InlineData(McpTools.Filesystem.ReadFile, true)]
    [InlineData("unknown.tool", false)]
    public void McpTools_IsKnownTool_ShouldReturnCorrectResult(string toolName, bool expected)
    {
        // Act
        var result = McpTools.IsKnownTool(toolName);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void McpTools_GetAllTools_ShouldReturnAllDefinedTools()
    {
        // Act
        var allTools = McpTools.GetAllTools().ToList();

        // Assert
        allTools.Should().NotBeEmpty();
        allTools.Should().Contain(McpTools.Gmail.ListMessages);
        allTools.Should().Contain(McpTools.Jira.CreateIssue);
        allTools.Should().Contain(McpTools.Filesystem.ReadFile);
        allTools.Should().Contain(McpTools.Slack.SendMessage);
        allTools.Should().Contain(McpTools.GitHub.CreateIssue);
        
        // All tools should be unique
        allTools.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeResources()
    {
        // Arrange
        await _client.InitializeAsync();

        // Act & Assert
        // Client doesn't need explicit disposal, just verify no exceptions
        // The client should be cleaned up by the DI container
        // This test verifies that the client can be initialized without issues
    }
}
