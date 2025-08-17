using AgentHost.Shared.Clients.LlmClient;
using AgentHost.Shared.Clients.McpClient;
using AgentHost.Shared.Persistence;
using AgentHost.Shared.Policy;
using AgentHost.Shared.Storage;
using AgentHost.Api.Pipelines;
using AgentHost.Shared.Discovery;

namespace AgentHost.Api.Composition;

public static class ServiceRegistration
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add persistence
        services.AddPersistence(configuration);

        // Add clients
        services.AddHttpClient<ILlmClient, LiteLlmClient>();
        // Register MCP client singleton using scope factory to avoid capturing scoped repositories directly
        services.AddSingleton<IMcpClient>(sp => new SdkMcpClient(
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<ILogger<SdkMcpClient>>(),
            sp.GetRequiredService<IServiceScopeFactory>()));

        // Add storage
        services.AddScoped<IBlobStorage, MinIOBlobStorage>();

        // Add policy validation
        services.AddScoped<IPolicyValidator, PolicyValidator>();

        // Add pipeline registry
        services.AddSingleton<IPipelineRegistry, InMemoryPipelineRegistry>();

    // Add MCP tool discovery (client). Audit logger provided by persistence layer (DB-backed)
    services.AddSingleton<IMcpToolDiscovery, McpClientToolDiscovery>();

        return services;
    }

    public static WebApplication ConfigureApiPipeline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        return app;
    }
}
