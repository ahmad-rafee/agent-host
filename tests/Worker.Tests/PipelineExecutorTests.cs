using AgentHost.Shared.Contracts;
using AgentHost.Worker.Jobs;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AgentHost.Worker.Tests;

public class PipelineExecutorTests
{
    private readonly Mock<ILogger<PipelineExecutor>> _loggerMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;

    public PipelineExecutorTests()
    {
        _loggerMock = new Mock<ILogger<PipelineExecutor>>();
        _serviceProviderMock = new Mock<IServiceProvider>();
    }

    [Fact]
    public void TransformStepExecutor_CanExecute_ShouldReturnTrueForTransformKind()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<TransformStepExecutor>>();
        var executor = new TransformStepExecutor(loggerMock.Object);

        // Act
        var canExecute = executor.CanExecute(StepKind.Transform);

        // Assert
        canExecute.Should().BeTrue();
    }

    [Fact]
    public void TransformStepExecutor_CanExecute_ShouldReturnFalseForOtherKinds()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<TransformStepExecutor>>();
        var executor = new TransformStepExecutor(loggerMock.Object);

        // Act
        var canExecute = executor.CanExecute(StepKind.LlmPrompt);

        // Assert
        canExecute.Should().BeFalse();
    }

    [Fact]
    public async Task TransformStepExecutor_ExecuteAsync_WithEmailToDocTransform_ShouldTransformCorrectly()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<TransformStepExecutor>>();
        var executor = new TransformStepExecutor(loggerMock.Object);

        var stepSpec = new PipelineStep("test", StepKind.Transform, Fn: "emailToDocV1");
        var context = new StepExecutionContext(
            Guid.NewGuid(),
            stepSpec,
            new Dictionary<string, object>
            {
                ["input"] = new
                {
                    items = new[]
                    {
                        new { id = "1", subject = "Test Email", from = "test@example.com" }
                    }
                }
            },
            _serviceProviderMock.Object
        );

        // Act
        var result = await executor.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().NotBeNull();
    }
}
