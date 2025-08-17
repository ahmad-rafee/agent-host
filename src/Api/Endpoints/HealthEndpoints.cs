using AgentHost.Shared.Contracts;
using AgentHost.Shared.Persistence;
using Microsoft.AspNetCore.Mvc;
using AgentHost.Api.Infrastructure;

namespace AgentHost.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
    app.MapGet("/healthz", () => BufferedResults.Ok(new HealthCheckResponse(true, DateTimeOffset.UtcNow)))
        .WithName("HealthCheck")
        .WithSummary("Health check endpoint")
        .WithTags("Health");
    }
}
