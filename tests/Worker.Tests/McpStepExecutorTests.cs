using AgentHost.Shared.Clients.McpClient;
using AgentHost.Shared.Contracts;
using AgentHost.Worker.Jobs;
using AgentHost.Shared.Persistence;
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
    private readonly Mock<IMcpToolRepository> _toolRepoMock = new();
    private readonly Mock<IMcpServerRepository> _serverRepoMock = new();

    public McpStepExecutorTests()
    {
        _mcpClientMock = new Mock<IMcpClient>();
        _loggerMock = new Mock<ILogger<McpStepExecutor>>();
        _serviceProviderMock = new Mock<IServiceProvider>();
    _executor = new McpStepExecutor(_mcpClientMock.Object, _loggerMock.Object, _toolRepoMock.Object, _serverRepoMock.Object);
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
    public async Task ExecuteAsync_WithServerNotFound_ShouldFailEarly()
    {
        var stepSpec = new PipelineStep("test", StepKind.McpCall, Tool: "mcp.gmail.listMessages", Server: "missing-server");
        var context = new StepExecutionContext(Guid.NewGuid(), stepSpec, new Dictionary<string, object>(), _serviceProviderMock.Object);

        _serverRepoMock.Setup(r => r.GetByNameAsync("missing-server")).ReturnsAsync((McpServer?)null);

        var result = await _executor.ExecuteAsync(context);
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("server 'missing-server' not found");
    }

    [Fact]
    public async Task ExecuteAsync_WithToolMissingOnServer_ShouldFailEarly()
    {
        var stepSpec = new PipelineStep("test", StepKind.McpCall, Tool: "mcp.gmail.listMessages", Server: "gmail");
        var context = new StepExecutionContext(Guid.NewGuid(), stepSpec, new Dictionary<string, object>(), _serviceProviderMock.Object);

        var server = new McpServer(Guid.NewGuid(), "gmail", "stdio", null, null, Array.Empty<string>(), null, new Dictionary<string, string>(), true, "ready", null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _serverRepoMock.Setup(r => r.GetByNameAsync("gmail")).ReturnsAsync(server);
        _toolRepoMock.Setup(r => r.GetByServerAndNameAsync(server.Id, It.IsAny<string>(), true)).ReturnsAsync((McpToolRecord?)null);

        var result = await _executor.ExecuteAsync(context);
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found on server 'gmail'");
    }

    [Fact]
    public async Task ExecuteAsync_WithDeletedTool_ShouldFailEarly()
    {
        var stepSpec = new PipelineStep("test", StepKind.McpCall, Tool: "mcp.gmail.listMessages", Server: "gmail");
        var context = new StepExecutionContext(Guid.NewGuid(), stepSpec, new Dictionary<string, object>(), _serviceProviderMock.Object);

        var server = new McpServer(Guid.NewGuid(), "gmail", "stdio", null, null, Array.Empty<string>(), null, new Dictionary<string, string>(), true, "ready", null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var deletedTool = new McpToolRecord(Guid.NewGuid(), server.Id, "gmail.list_messages", "desc", new { }, Array.Empty<string>(), null, null, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _serverRepoMock.Setup(r => r.GetByNameAsync("gmail")).ReturnsAsync(server);
        _toolRepoMock.Setup(r => r.GetByServerAndNameAsync(server.Id, "gmail.list_messages", true)).ReturnsAsync(deletedTool);

        var result = await _executor.ExecuteAsync(context);
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("is deleted");
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
    var stepSpec = new PipelineStep("test", StepKind.McpCall, Tool: "gmail.list_messages");
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
            .Setup(x => x.CallToolAsync("gmail.list_messages", It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
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
            x => x.CallToolAsync("gmail.list_messages", It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithFailedMcpCall_ShouldReturnError()
    {
        // Arrange
    var stepSpec = new PipelineStep("test", StepKind.McpCall, Tool: "jira.create_issue");
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
            .Setup(x => x.CallToolAsync("jira.create_issue", It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
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
    var stepSpec = new PipelineStep("test", StepKind.McpPull, Tool: "gmail.list_messages");
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
            .Setup(x => x.CallToolAsync("gmail.list_messages", It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
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

    // Legacy normalization path still covered indirectly via early validation tests when Server not provided.
}
