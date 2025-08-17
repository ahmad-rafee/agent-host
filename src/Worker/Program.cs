using AgentHost.Shared.Clients.LlmClient;
using AgentHost.Shared.Clients.McpClient;
using AgentHost.Shared.Observability;
using AgentHost.Shared.Persistence;
using AgentHost.Worker.Jobs;
using AgentHost.Shared.Policy;
using AgentHost.Worker.Services;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Add services
builder.Services.AddPersistence(builder.Configuration);

// Add clients
builder.Services.AddHttpClient<ILlmClient, LiteLlmClient>();
builder.Services.AddSingleton<IMcpClient>(sp => new SdkMcpClient(
	sp.GetRequiredService<IConfiguration>(),
	sp.GetRequiredService<ILogger<SdkMcpClient>>(),
	sp.GetRequiredService<IServiceScopeFactory>()));

// Add step executors
builder.Services.AddScoped<IStepExecutor, McpStepExecutor>();
builder.Services.AddScoped<IStepExecutor, LlmStepExecutor>();
builder.Services.AddScoped<IStepExecutor, TransformStepExecutor>();

// Policy validator
builder.Services.AddScoped<IPolicyValidator, PolicyValidator>();

// Add pipeline executor
builder.Services.AddScoped<IPipelineExecutor, PipelineExecutor>();

// Add worker service
builder.Services.AddHostedService<PipelineWorkerService>();

// MCP connection manager (feature flagged)
if (builder.Configuration.GetValue<bool>("Mcp:EnableRealConnections"))
{
	builder.Services.AddHostedService<McpConnectionManagerHostedService>();
}

// Add observability
builder.Services.AddObservability("AgentHost.Worker");

// Configure logging
builder.Logging.AddStructuredLogging();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("AgentHost Worker starting");

await host.RunAsync();
