using AgentHost.Shared.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace AgentHost.Worker.Services;

public interface IMcpConnectionManager { }

public class McpConnectionManagerHostedService : IHostedService, IMcpConnectionManager
{
    private readonly IMcpServerRepository _serverRepo;
    private readonly ILogger<McpConnectionManagerHostedService> _logger;
    private readonly IConfiguration _config;
    private CancellationTokenSource? _cts;

    public McpConnectionManagerHostedService(IMcpServerRepository serverRepo, ILogger<McpConnectionManagerHostedService> logger, IConfiguration config)
    {
        _serverRepo = serverRepo;
        _logger = logger;
        _config = config;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var enabled = _config.GetValue<bool>("Mcp:EnableRealConnections");
        if (!enabled)
        {
            _logger.LogInformation("MCP Connection Manager disabled by config");
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Task.Run(() => RunAsync(_cts.Token));
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await SweepAsync(ct);
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(60), ct);
                await SweepAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP Connection Manager loop failed");
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        var servers = await _serverRepo.ListAsync(enabled: true, limit: 500, offset: 0);
        foreach (var s in servers)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (s.Status is "ready" or "disabled") continue;
                await _serverRepo.UpdateStatusAsync(s.Id, "connecting", null, DateTimeOffset.UtcNow);
                await Task.Delay(50, ct); // simulate connection
                await _serverRepo.UpdateStatusAsync(s.Id, "ready", null, DateTimeOffset.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to simulate connection for server {ServerId}", s.Id);
                await _serverRepo.UpdateStatusAsync(s.Id, "error", ex.Message, DateTimeOffset.UtcNow);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }
}
