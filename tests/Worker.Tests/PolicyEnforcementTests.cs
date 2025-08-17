using AgentHost.Shared.Clients.McpClient;
using AgentHost.Shared.Clients.LlmClient;
using AgentHost.Shared.Contracts;
using AgentHost.Shared.Policy;
using AgentHost.Worker.Jobs;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AgentHost.Worker.Tests;

public class PolicyEnforcementTests
{
    [Fact]
    public async Task McpExecutor_ShouldNotInvoke_WhenPolicyInvalid()
    {
        var mcp = new Mock<IMcpClient>();
        var logger = new Mock<ILogger<McpStepExecutor>>();
        var policy = new Mock<IPolicyValidator>();
        policy.Setup(p => p.ValidateAsync(It.IsAny<PolicyValidationRequest>()))
            .ReturnsAsync(new PolicyValidationResult(false, new[]{"Model 'chat-default' is not in the allowlist"}, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()));
        var exec = new McpStepExecutor(mcp.Object, logger.Object, policyValidator: policy.Object);
        var spec = new PipelineStep("mcp", StepKind.McpCall, Tool: "gmail.list_messages");
        var ctx = new StepExecutionContext(Guid.NewGuid(), spec, new Dictionary<string, object>(), new Mock<IServiceProvider>().Object);
        var result = await exec.ExecuteAsync(ctx);
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Policy validation failed");
        mcp.Verify(x => x.CallToolAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LlmExecutor_ShouldNotInvoke_WhenPolicyInvalid()
    {
        var llm = new Mock<ILlmClient>();
        var logger = new Mock<ILogger<LlmStepExecutor>>();
        var policy = new Mock<IPolicyValidator>();
        policy.Setup(p => p.ValidateAsync(It.IsAny<PolicyValidationRequest>()))
            .ReturnsAsync(new PolicyValidationResult(false, new[]{"Model 'bad-model' is not in the allowlist"}, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()));
    var exec = new LlmStepExecutor(llm.Object, logger.Object, policy.Object);
        var spec = new PipelineStep("llm", StepKind.LlmPrompt, Model: "bad-model", Input: "Hello" );
        var ctx = new StepExecutionContext(Guid.NewGuid(), spec, new Dictionary<string, object>(), new Mock<IServiceProvider>().Object);
        var result = await exec.ExecuteAsync(ctx);
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Policy validation failed");
        llm.Verify(x => x.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LlmExecutor_ShouldSucceed_WhenModelAddedByScope()
    {
        var llm = new Mock<ILlmClient>();
        llm.Setup(c => c.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmChatResponse("custom output", "custom-model-x", new LlmUsage(10,5,15)));
        var logger = new Mock<ILogger<LlmStepExecutor>>();
        var policy = new Mock<IPolicyValidator>();
        policy.SetupSequence(p => p.ValidateAsync(It.IsAny<PolicyValidationRequest>()))
            .ReturnsAsync(new PolicyValidationResult(false, new[]{"Model 'custom-model-x' is not in the allowlist"}, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()))
            .ReturnsAsync(new PolicyValidationResult(true, Array.Empty<string>(), Array.Empty<string>(), new[]{"custom-model-x"}, Array.Empty<string>()));

        var exec = new LlmStepExecutor(llm.Object, logger.Object, policy.Object);
        var spec = new PipelineStep("llm", StepKind.LlmPrompt, Model: "custom-model-x", Input: "Hi" );
        var ctx = new StepExecutionContext(Guid.NewGuid(), spec, new Dictionary<string, object>(), new Mock<IServiceProvider>().Object);

        // First attempt fails (policy denied)
        var first = await exec.ExecuteAsync(ctx);
        first.Success.Should().BeFalse();

        // Simulate scope added (second validation returns allowed)
        var second = await exec.ExecuteAsync(ctx);
        second.Success.Should().BeTrue();
        llm.Verify(x => x.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task McpExecutor_ShouldSucceed_WhenToolAddedByScope()
    {
        var mcp = new Mock<IMcpClient>();
        mcp.Setup(c => c.InitializeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mcp.Setup(c => c.CallToolAsync("gmail.list_messages", It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new McpResponse{ Success = true, Result = new { messages = Array.Empty<object>() }, Metadata = new Dictionary<string, object>()});
        var logger = new Mock<ILogger<McpStepExecutor>>();
        var policy = new Mock<IPolicyValidator>();
        policy.SetupSequence(p => p.ValidateAsync(It.IsAny<PolicyValidationRequest>()))
            .ReturnsAsync(new PolicyValidationResult(false, new[]{"Tool 'gmail.list_messages' is not authorized"}, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()))
            .ReturnsAsync(new PolicyValidationResult(true, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), new[]{"gmail.list_messages"}));
        var exec = new McpStepExecutor(mcp.Object, logger.Object, policyValidator: policy.Object);
        var spec = new PipelineStep("mcp", StepKind.McpPull, Tool: "gmail.list_messages");
        var ctx = new StepExecutionContext(Guid.NewGuid(), spec, new Dictionary<string, object>(), new Mock<IServiceProvider>().Object);

        var first = await exec.ExecuteAsync(ctx);
        first.Success.Should().BeFalse();

        var second = await exec.ExecuteAsync(ctx);
        second.Success.Should().BeTrue();
        mcp.Verify(x => x.CallToolAsync("gmail.list_messages", It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
