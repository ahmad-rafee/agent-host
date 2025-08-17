using AgentHost.Shared.Clients.LlmClient;
using AgentHost.Shared.Clients.McpClient;
using AgentHost.Shared.Observability;
using AgentHost.Shared.Persistence;
using AgentHost.Worker.Jobs;
using AgentHost.Worker.Services;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Add services
builder.Services.AddPersistence(builder.Configuration);

// Add clients
builder.Services.AddHttpClient<ILlmClient, LiteLlmClient>();
builder.Services.AddSingleton<IMcpClient, SdkMcpClient>();

// Add step executors
builder.Services.AddScoped<IStepExecutor, McpStepExecutor>();
builder.Services.AddScoped<IStepExecutor, LlmStepExecutor>();
builder.Services.AddScoped<IStepExecutor, TransformStepExecutor>();

// Add pipeline executor
builder.Services.AddScoped<IPipelineExecutor, PipelineExecutor>();

// Add worker service
builder.Services.AddHostedService<PipelineWorkerService>();

// Add observability
builder.Services.AddObservability("AgentHost.Worker");

// Configure logging
builder.Logging.AddStructuredLogging();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("AgentHost Worker starting");

await host.RunAsync();
