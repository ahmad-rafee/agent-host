using System.Text.Json;

namespace AgentHost.Shared.Clients.LlmClient;

public interface ILlmClient
{
    Task<LlmChatResponse> ChatAsync(LlmChatRequest request, CancellationToken cancellationToken = default);
    Task<LlmEmbeddingResponse> EmbedAsync(LlmEmbeddingRequest request, CancellationToken cancellationToken = default);
}

public record LlmChatRequest(
    string Model,
    IEnumerable<LlmMessage> Messages,
    double Temperature = 0.7,
    int MaxTokens = 1000,
    string? SystemPrompt = null);

public record LlmMessage(string Role, string Content);

public record LlmChatResponse(
    string Content,
    string Model,
    LlmUsage Usage);

public record LlmEmbeddingRequest(
    string Model,
    IEnumerable<string> Inputs);

public record LlmEmbeddingResponse(
    IEnumerable<float[]> Embeddings,
    string Model,
    LlmUsage Usage);

public record LlmUsage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    decimal? Cost = null);

public static class LlmRoles
{
    public const string System = "system";
    public const string User = "user";
    public const string Assistant = "assistant";
}
