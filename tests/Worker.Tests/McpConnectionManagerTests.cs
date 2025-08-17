using AgentHost.Shared.Persistence;
using AgentHost.Worker.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgentHost.Worker.Tests;

public class McpConnectionManagerTests
{
    private class InMemoryMcpServerRepository : IMcpServerRepository
    {
        private readonly Dictionary<Guid, McpServer> _servers = new();
        private readonly object _lock = new();

        public Task CreateAsync(McpServer server)
        {
            lock (_lock) _servers[server.Id] = server;
            return Task.CompletedTask;
        }

        public Task EnableAsync(Guid id, bool isEnabled)
        {
            lock (_lock)
            {
                if (_servers.TryGetValue(id, out var s))
                {
                    _servers[id] = s with { IsEnabled = isEnabled, Status = isEnabled ? s.Status : "disabled", UpdatedAt = DateTimeOffset.UtcNow };
                }
            }
            return Task.CompletedTask;
        }

        public Task<McpServer?> GetAsync(Guid id)
        {
            lock (_lock) return Task.FromResult(_servers.TryGetValue(id, out var s) ? s : null);
        }

        public Task<McpServer?> GetByNameAsync(string name)
        {
            lock (_lock) return Task.FromResult(_servers.Values.FirstOrDefault(s => s.Name == name));
        }

        public Task<IEnumerable<McpServer>> ListAsync(bool? enabled = null, int limit = 100, int offset = 0)
        {
            lock (_lock)
            {
                var q = _servers.Values.AsEnumerable();
                if (enabled != null) q = q.Where(s => s.IsEnabled == enabled);
                q = q.Skip(offset).Take(limit);
                return Task.FromResult(q);
            }
        }

        public Task UpdateAsync(McpServer server)
        {
            lock (_lock) _servers[server.Id] = server with { UpdatedAt = DateTimeOffset.UtcNow };
            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(Guid id, string status, string? error = null, DateTimeOffset? heartbeat = null)
        {
            lock (_lock)
            {
                if (_servers.TryGetValue(id, out var s))
                {
                    _servers[id] = s with
                    {
                        Status = status,
                        Error = error,
                        LastHeartbeatAt = heartbeat,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                }
            }
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ConnectionManager_Should_Transition_Unknown_To_Ready_When_Flag_Enabled()
    {
        // Arrange
        var repo = new InMemoryMcpServerRepository();
        var server = new McpServer(
            Id: Guid.NewGuid(),
            Name: "cm-test",
            Type: "stdio",
            DisplayName: null,
            Command: "echo",
            Args: Array.Empty<string>(),
            Endpoint: null,
            Env: new Dictionary<string, string>(),
            IsEnabled: true,
            Status: "unknown",
            LastHeartbeatAt: null,
            Error: null,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);
        await repo.CreateAsync(server);

        var settings = new Dictionary<string, string?>
        {
            ["Mcp:EnableRealConnections"] = "true"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var logger = NullLogger<McpConnectionManagerHostedService>.Instance;
        var svc = new McpConnectionManagerHostedService(repo, logger, config);

        // Act
        await svc.StartAsync(CancellationToken.None);
        await Task.Delay(200); // allow background sweep
        var updated = await repo.GetAsync(server.Id);
        await svc.StopAsync(CancellationToken.None);

        // Assert
        updated!.Status.Should().Be("ready");
        updated.LastHeartbeatAt.Should().NotBeNull();
    }
}
