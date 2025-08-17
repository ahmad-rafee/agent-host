using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentHost.Shared.Clients.LlmClient;

public class LiteLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LiteLlmClient> _logger;
    private readonly LlmClientOptions _options;

    public LiteLlmClient(HttpClient httpClient, IConfiguration configuration, ILogger<LiteLlmClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = configuration.GetSection("Llm").Get<LlmClientOptions>() ?? new LlmClientOptions();
        
        if (!string.IsNullOrEmpty(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        }
    }

    public async Task<LlmChatResponse> ChatAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Sending chat request to model {Model}", request.Model);

        var messages = request.Messages.ToList();
        
        // Add system message if provided
        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            messages.Insert(0, new LlmMessage(LlmRoles.System, request.SystemPrompt));
        }

        var requestBody = new
        {
            model = ResolveModelAlias(request.Model),
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            temperature = request.Temperature,
            max_tokens = request.MaxTokens
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync("/v1/chat/completions", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

            var choice = responseData.GetProperty("choices")[0];
            var message = choice.GetProperty("message");
            var usage = responseData.GetProperty("usage");

            var result = new LlmChatResponse(
                message.GetProperty("content").GetString() ?? "",
                responseData.GetProperty("model").GetString() ?? request.Model,
                new LlmUsage(
                    usage.GetProperty("prompt_tokens").GetInt32(),
                    usage.GetProperty("completion_tokens").GetInt32(),
                    usage.GetProperty("total_tokens").GetInt32()
                )
            );

            _logger.LogDebug("Chat request completed. Tokens: {TotalTokens}", result.Usage.TotalTokens);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling LLM chat API");
            throw;
        }
    }

    public async Task<LlmEmbeddingResponse> EmbedAsync(LlmEmbeddingRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Sending embedding request to model {Model} for {InputCount} inputs", request.Model, request.Inputs.Count());

        var requestBody = new
        {
            model = ResolveModelAlias(request.Model),
            input = request.Inputs
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync("/v1/embeddings", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

            var dataArray = responseData.GetProperty("data");
            var embeddings = new List<float[]>();

            foreach (var item in dataArray.EnumerateArray())
            {
                var embedding = item.GetProperty("embedding")
                    .EnumerateArray()
                    .Select(x => (float)x.GetDouble())
                    .ToArray();
                embeddings.Add(embedding);
            }

            var usage = responseData.GetProperty("usage");
            var result = new LlmEmbeddingResponse(
                embeddings,
                responseData.GetProperty("model").GetString() ?? request.Model,
                new LlmUsage(
                    usage.GetProperty("prompt_tokens").GetInt32(),
                    0, // embeddings don't have completion tokens
                    usage.GetProperty("total_tokens").GetInt32()
                )
            );

            _logger.LogDebug("Embedding request completed. Tokens: {TotalTokens}", result.Usage.TotalTokens);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling LLM embedding API");
            throw;
        }
    }

    private string ResolveModelAlias(string modelAlias)
    {
        return _options.ModelAliases.TryGetValue(modelAlias, out var actualModel) 
            ? actualModel 
            : modelAlias;
    }
}

public class LlmClientOptions
{
    public string BaseUrl { get; set; } = "http://localhost:4000";
    public Dictionary<string, string> ModelAliases { get; set; } = new()
    {
        { "chat-default", "gpt-3.5-turbo" },
        { "embed-default", "text-embedding-ada-002" }
    };
}
