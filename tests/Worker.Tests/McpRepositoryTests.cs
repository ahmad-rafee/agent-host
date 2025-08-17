using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AgentHost.Shared.Persistence;
using FluentAssertions;
using Xunit;

namespace AgentHost.Worker.Tests;

public class McpRepositoryTests
{
    private const string Conn = "Host=db;Database=postgres;Username=postgres;Password=postgres";

    private static void EnsureMigrations()
    {
        DatabaseMigrator.MigrateDatabase(Conn);
    }

    [Fact]
    public async Task McpServerRepository_Create_And_Get_Should_RoundTrip()
    {
        EnsureMigrations();
        var repo = new McpServerRepository(Conn);
        var id = Guid.NewGuid();
        var server = new McpServer(
            id,
            Name: $"srv_{id.ToString()[..8]}",
            Type: "stdio",
            DisplayName: "Test Server",
            Command: "echo",
            Args: new[] { "arg1", "arg2" },
            Endpoint: null,
            Env: new Dictionary<string, string> { ["A"] = "B" },
            IsEnabled: true,
            Status: "unknown",
            LastHeartbeatAt: null,
            Error: null,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow
        );

        await repo.CreateAsync(server);

        var fetched = await repo.GetAsync(id);
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be(server.Name);
        fetched.Args.Should().BeEquivalentTo(server.Args);
        fetched.Env.Should().ContainKey("A");
    }

    [Fact]
    public async Task McpServerRepository_UpdateStatus_Should_Persist()
    {
        EnsureMigrations();
        var repo = new McpServerRepository(Conn);
        var id = Guid.NewGuid();
        var server = new McpServer(
            id,
            Name: $"srv_{id.ToString()[..8]}",
            Type: "http",
            DisplayName: null,
            Command: null,
            Args: Array.Empty<string>(),
            Endpoint: "http://localhost:1234",
            Env: new Dictionary<string, string>(),
            IsEnabled: true,
            Status: "unknown",
            LastHeartbeatAt: null,
            Error: null,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow
        );
        await repo.CreateAsync(server);

        await repo.UpdateStatusAsync(id, "ready", null, DateTimeOffset.UtcNow);
        var updated = await repo.GetAsync(id);
        updated!.Status.Should().Be("ready");
        updated.LastHeartbeatAt.Should().NotBeNull();
    }

    [Fact]
    public async Task McpToolRepository_UpsertMany_Should_Insert_And_Update()
    {
        EnsureMigrations();
        var serverRepo = new McpServerRepository(Conn);
        var toolRepo = new McpToolRepository(Conn);
        var sid = Guid.NewGuid();
        var server = new McpServer(
            sid,
            Name: $"srv_{sid.ToString()[..8]}",
            Type: "stdio",
            DisplayName: "Tools Server",
            Command: "echo",
            Args: new[] { "--flag" },
            Endpoint: null,
            Env: new Dictionary<string, string>(),
            IsEnabled: true,
            Status: "ready",
            LastHeartbeatAt: DateTimeOffset.UtcNow,
            Error: null,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow
        );
        await serverRepo.CreateAsync(server);

    var t1 = new McpToolRecord(Guid.NewGuid(), sid, "alpha.tool", "Alpha desc", new { a = 1 }, new[] { "scope.a" }, null, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    var t2 = new McpToolRecord(Guid.NewGuid(), sid, "beta.tool", "Beta desc", new { b = 2 }, Array.Empty<string>(), "v1", null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await toolRepo.UpsertManyAsync(new[] { t1, t2 });

        var list = (await toolRepo.ListByServerAsync(sid)).ToList();
        list.Should().HaveCount(2);
        list.Select(x => x.Name).Should().Contain(new[] { "alpha.tool", "beta.tool" });

    var updatedT2 = new McpToolRecord(t2.Id, sid, "beta.tool", "Beta desc v2", new { b = 3 }, new[] { "scope.b" }, "v2", null, false, t2.CreatedAt, DateTimeOffset.UtcNow);
        await toolRepo.UpsertAsync(updatedT2);

        var after = (await toolRepo.ListByServerAsync(sid)).ToList();
        after.Should().HaveCount(2);
        after.Single(x => x.Name == "beta.tool").Description.Should().Contain("v2");
        after.Single(x => x.Name == "beta.tool").RequiredScopes.Should().Contain("scope.b");
    }
}
