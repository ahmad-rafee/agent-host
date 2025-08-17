namespace AgentHost.Shared.Policy;

public interface IPolicyValidator
{
    Task<PolicyValidationResult> ValidateAsync(PolicyValidationRequest request);
}

public record PolicyValidationRequest(
    string UserId,
    string Model,
    IEnumerable<string> Tools,
    string? Scope = null);

public record PolicyValidationResult(
    bool IsValid,
    IEnumerable<string> Errors,
    IEnumerable<string> Warnings = null!)
{
    public PolicyValidationResult() : this(false, Array.Empty<string>())
    {
        Warnings ??= Array.Empty<string>();
    }
}

public class PolicyOptions
{
    public IEnumerable<string> ModelAllowlist { get; set; } = new[] { "chat-default", "embed-default" };
    public decimal MonthlyBudgetUsd { get; set; } = 100m;
    public IEnumerable<string> ToolScopes { get; set; } = new[] { "email.read", "jira.write" };
    public Dictionary<string, decimal> ModelCosts { get; set; } = new()
    {
        { "gpt-3.5-turbo", 0.002m },
        { "gpt-4", 0.03m },
        { "text-embedding-ada-002", 0.0004m }
    };
}
