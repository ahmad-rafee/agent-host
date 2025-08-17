using AgentHost.Shared.Clients.McpClient;
using AgentHost.Shared.Contracts;
using AgentHost.Worker.Jobs;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace AgentHost.Worker.Tests;

public class McpStepExecutorTests
{
    private readonly Mock<IMcpClient> _mcpClientMock;
    private readonly Mock<ILogger<McpStepExecutor>> _loggerMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly McpStepExecutor _executor;

    public McpStepExecutorTests()
    {
        _mcpClientMock = new Mock<IMcpClient>();
        _loggerMock = new Mock<ILogger<McpStepExecutor>>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _executor = new McpStepExecutor(_mcpClientMock.Object, _loggerMock.Object);
    }

    [Theory]
    [InlineData(StepKind.McpPull, true)]
    [InlineData(StepKind.McpCall, true)]
    [InlineData(StepKind.LlmPrompt, false)]
    [InlineData(StepKind.Transform, false)]
    public void CanExecute_ShouldReturnCorrectResult(string stepKind, bool expected)
    {
        // Act
        var result = _executor.CanExecute(stepKind);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingTool_ShouldReturnError()
    {
        // Arrange
        var stepSpec = new PipelineStep("test", StepKind.McpCall, Tool: null);
        var context = new StepExecutionContext(
            Guid.NewGuid(),
            stepSpec,
            new Dictionary<string, object>(),
            _serviceProviderMock.Object
        );

        // Act
        var result = await _executor.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Tool is required for MCP steps");
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulMcpCall_ShouldReturnSuccess()
    {
        // Arrange
        var stepSpec = new PipelineStep("test", StepKind.McpCall, Tool: "mcp.gmail.listMessages");
        var context = new StepExecutionContext(
            Guid.NewGuid(),
            stepSpec,
            new Dictionary<string, object>
            {
                ["params"] = new Dictionary<string, object>
                {
                    ["query"] = "from:test@example.com"
                }
            },
            _serviceProviderMock.Object
        );

        var mockResponse = new McpResponse
        {
            Success = true,
            Result = new
            {
                messages = new[]
                {
                    new { id = "1", subject = "Test Email", from = "test@example.com" }
                }
            },
            Metadata = new Dictionary<string, object> { ["source"] = "gmail" }
        };

        _mcpClientMock
            .Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mcpClientMock
            .Setup(x => x.CallToolAsync(McpTools.Gmail.ListMessages, It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _executor.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().NotBeNull();
        result.Metadata.Should().ContainKey("tool");
        result.Metadata.Should().ContainKey("originalTool");
        result.Metadata.Should().ContainKey("mcpMetadata");

        // Verify MCP client was called correctly
        _mcpClientMock.Verify(x => x.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mcpClientMock.Verify(
            x => x.CallToolAsync(McpTools.Gmail.ListMessages, It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithFailedMcpCall_ShouldReturnError()
    {
        // Arrange
        var stepSpec = new PipelineStep("test", StepKind.McpCall, Tool: "mcp.jira.createIssue");
        var context = new StepExecutionContext(
            Guid.NewGuid(),
            stepSpec,
            new Dictionary<string, object>
            {
                ["params"] = new Dictionary<string, object>
                {
                    ["projectKey"] = "PROJ",
                    ["summary"] = "Test Issue"
                }
            },
            _serviceProviderMock.Object
        );

        var mockResponse = new McpResponse
        {
            Success = false,
            Error = "Invalid project key"
        };

        _mcpClientMock
            .Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mcpClientMock
            .Setup(x => x.CallToolAsync(McpTools.Jira.CreateIssue, It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _executor.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Invalid project key");
    }

    [Fact]
    public async Task ExecuteAsync_WithMcpPullKind_ShouldProcessPullResponse()
    {
        // Arrange
        var stepSpec = new PipelineStep("test", StepKind.McpPull, Tool: "mcp.gmail.listMessages");
        var context = new StepExecutionContext(
            Guid.NewGuid(),
            stepSpec,
            new Dictionary<string, object>(),
            _serviceProviderMock.Object
        );

        var mockMessages = new[]
        {
            new { id = "1", subject = "Email 1" },
            new { id = "2", subject = "Email 2" }
        };

        var mockResponse = new McpResponse
        {
            Success = true,
            Result = new { messages = mockMessages },
            Metadata = new Dictionary<string, object>()
        };

        _mcpClientMock
            .Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mcpClientMock
            .Setup(x => x.CallToolAsync(McpTools.Gmail.ListMessages, It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _executor.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().NotBeNull();
        
        // Verify pull response processing
        var outputJson = JsonSerializer.Serialize(result.Output);
        outputJson.Should().Contain("items");
        outputJson.Should().Contain("count");
    }

    [Fact]
    public async Task ExecuteAsync_WithException_ShouldReturnError()
    {
        // Arrange
        var stepSpec = new PipelineStep("test", StepKind.McpCall, Tool: "mcp.fs.readFile");
        var context = new StepExecutionContext(
            Guid.NewGuid(),
            stepSpec,
            new Dictionary<string, object>(),
            _serviceProviderMock.Object
        );

        _mcpClientMock
            .Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("MCP server not available"));

        // Act
        var result = await _executor.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("MCP server not available");
    }

    [Theory]
    [InlineData("mcp.gmail.listMessages", McpTools.Gmail.ListMessages)]
    [InlineData("mcp.jira.createIssue", McpTools.Jira.CreateIssue)]
    [InlineData("mcp.fs.readFile", McpTools.Filesystem.ReadFile)]
    [InlineData("unknown.tool", "unknown.tool")]
    public async Task ExecuteAsync_ShouldConvertToolNamesCorrectly(string originalTool, string expectedMcpTool)
    {
        // Arrange
        var stepSpec = new PipelineStep("test", StepKind.McpCall, Tool: originalTool);
        var context = new StepExecutionContext(
            Guid.NewGuid(),
            stepSpec,
            new Dictionary<string, object>(),
            _serviceProviderMock.Object
        );

        var mockResponse = new McpResponse
        {
            Success = true,
            Result = new { result = "test" },
            Metadata = new Dictionary<string, object>()
        };

        _mcpClientMock
            .Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mcpClientMock
            .Setup(x => x.CallToolAsync(expectedMcpTool, It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _executor.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        
        // Verify the correct MCP tool name was used
        _mcpClientMock.Verify(
            x => x.CallToolAsync(expectedMcpTool, It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
