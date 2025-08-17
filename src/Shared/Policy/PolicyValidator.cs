using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentHost.Shared.Policy;

public class PolicyValidator : IPolicyValidator
{
    private readonly PolicyOptions _options;
    private readonly ILogger<PolicyValidator> _logger;

    public PolicyValidator(IConfiguration configuration, ILogger<PolicyValidator> logger)
    {
        _options = configuration.GetSection("Policy").Get<PolicyOptions>() ?? new PolicyOptions();
        _logger = logger;
    }

    public async Task<PolicyValidationResult> ValidateAsync(PolicyValidationRequest request)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate model allowlist
        if (!_options.ModelAllowlist.Contains(request.Model))
        {
            errors.Add($"Model '{request.Model}' is not in the allowlist");
        }

        // Validate tool scopes
        foreach (var tool in request.Tools)
        {
            var requiredScope = GetRequiredScopeForTool(tool);
            if (requiredScope != null && !_options.ToolScopes.Contains(requiredScope))
            {
                errors.Add($"Tool '{tool}' requires scope '{requiredScope}' which is not authorized");
            }
        }

        // Check budget (simplified - would need actual usage tracking in production)
        var estimatedCost = EstimateCost(request.Model);
        if (estimatedCost > _options.MonthlyBudgetUsd * 0.1m) // Warning at 10% of budget per request
        {
            warnings.Add($"Request may consume significant budget: ${estimatedCost:F4}");
        }

        // Validate scope if provided
        if (!string.IsNullOrEmpty(request.Scope) && !_options.ToolScopes.Contains(request.Scope))
        {
            errors.Add($"Scope '{request.Scope}' is not authorized");
        }

        var result = new PolicyValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors,
            Warnings: warnings
        );

        if (!result.IsValid)
        {
            _logger.LogWarning("Policy validation failed for user {UserId}: {Errors}", 
                request.UserId, string.Join(", ", errors));
        }
        else if (warnings.Any())
        {
            _logger.LogInformation("Policy validation passed with warnings for user {UserId}: {Warnings}", 
                request.UserId, string.Join(", ", warnings));
        }

        return await Task.FromResult(result);
    }

    private string? GetRequiredScopeForTool(string tool)
    {
        return tool switch
        {
            var t when t.Contains("gmail") => "email.read",
            var t when t.Contains("jira") => "jira.write",
            var t when t.Contains("fs") => "filesystem.read",
            _ => null
        };
    }

    private decimal EstimateCost(string model)
    {
        // Very rough estimation - in production this would be more sophisticated
        var costPerToken = _options.ModelCosts.GetValueOrDefault(model, 0.002m);
        var estimatedTokens = 1000; // Rough estimate
        return costPerToken * estimatedTokens / 1000m; // Cost is usually per 1k tokens
    }
}
