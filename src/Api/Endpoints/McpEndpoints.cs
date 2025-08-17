using AgentHost.Api.Infrastructure;
using AgentHost.Shared.Contracts;
using AgentHost.Shared.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace AgentHost.Api.Endpoints;

public static class McpEndpoints
{
    public static void MapMcpEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/mcp").WithTags("MCP");

        // Create server
        group.MapPost("/servers", async (
            [FromBody] McpServerCreateRequest request,
            [FromServices] IMcpServerRepository serverRepo,
            [FromServices] ILogger<Program> logger) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Type))
                {
                    return BufferedResults.BadRequest(new ErrorResponse("name and type are required"));
                }

                // Prevent duplicate name
                var existing = await serverRepo.GetByNameAsync(request.Name);
                if (existing != null)
                {
                    return BufferedResults.Conflict(new ErrorResponse($"Server name '{request.Name}' already exists"));
                }

                var now = DateTimeOffset.UtcNow;
                var server = new McpServer(
                    Id: Guid.NewGuid(),
                    Name: request.Name,
                    Type: request.Type,
                    DisplayName: request.DisplayName,
                    Command: request.Command,
                    Args: request.Args ?? Array.Empty<string>(),
                    Endpoint: request.Endpoint,
                    Env: request.Env ?? new Dictionary<string, string>(),
                    IsEnabled: request.IsEnabled,
                    Status: request.IsEnabled ? "created" : "disabled",
                    LastHeartbeatAt: null,
                    Error: null,
                    CreatedAt: now,
                    UpdatedAt: now);

                await serverRepo.CreateAsync(server);
                logger.LogInformation("Created MCP server {ServerId} ({Name})", server.Id, server.Name);
                return BufferedResults.Created($"/mcp/servers/{server.Id}", new McpServerResponse(server.Id));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating MCP server");
                return BufferedResults.Problem("An error occurred while creating the MCP server");
            }
        }).WithName("CreateMcpServer").WithSummary("Create an MCP server");

        // List servers
        group.MapGet("/servers", async (
            [FromServices] IMcpServerRepository serverRepo,
            [FromServices] ILogger<Program> logger,
            [FromQuery] bool? enabled,
            [FromQuery] int limit = 100,
            [FromQuery] int offset = 0) =>
        {
            try
            {
                var servers = await serverRepo.ListAsync(enabled, limit, offset);
                return BufferedResults.Ok(servers);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error listing MCP servers");
                return BufferedResults.Problem("An error occurred while listing MCP servers");
            }
        }).WithName("ListMcpServers").WithSummary("List MCP servers");

        // Get server
        group.MapGet("/servers/{id:guid}", async (
            Guid id,
            [FromServices] IMcpServerRepository serverRepo,
            [FromServices] ILogger<Program> logger) =>
        {
            try
            {
                var server = await serverRepo.GetAsync(id);
                if (server == null) return BufferedResults.NotFound(new ErrorResponse($"Server {id} not found"));
                return BufferedResults.Ok(server);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting MCP server {ServerId}", id);
                return BufferedResults.Problem("An error occurred while retrieving the MCP server");
            }
        }).WithName("GetMcpServer").WithSummary("Get MCP server details");

        // Update status
        group.MapPatch("/servers/{id:guid}/status", async (
            Guid id,
            [FromBody] McpServerStatusUpdateRequest request,
            [FromServices] IMcpServerRepository serverRepo,
            [FromServices] ILogger<Program> logger) =>
        {
            try
            {
                var server = await serverRepo.GetAsync(id);
                if (server == null) return BufferedResults.NotFound(new ErrorResponse($"Server {id} not found"));
                await serverRepo.UpdateStatusAsync(id, request.Status, request.Error, DateTimeOffset.UtcNow);
                return BufferedResults.Ok(new { Id = id, Status = request.Status });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating MCP server status {ServerId}", id);
                return BufferedResults.Problem("An error occurred while updating status");
            }
        }).WithName("UpdateMcpServerStatus").WithSummary("Update MCP server status");

        // Enable/disable
        group.MapPatch("/servers/{id:guid}/enable", async (
            Guid id,
            [FromBody] McpServerEnableRequest request,
            [FromServices] IMcpServerRepository serverRepo,
            [FromServices] ILogger<Program> logger) =>
        {
            try
            {
                var server = await serverRepo.GetAsync(id);
                if (server == null) return BufferedResults.NotFound(new ErrorResponse($"Server {id} not found"));
                await serverRepo.EnableAsync(id, request.IsEnabled);
                return BufferedResults.Ok(new { Id = id, IsEnabled = request.IsEnabled });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error enabling/disabling MCP server {ServerId}", id);
                return BufferedResults.Problem("An error occurred while toggling server");
            }
        }).WithName("ToggleMcpServer").WithSummary("Enable or disable MCP server");

        // List tools for server
        group.MapGet("/servers/{id:guid}/tools", async (
            Guid id,
            [FromServices] IMcpServerRepository serverRepo,
            [FromServices] IMcpToolRepository toolRepo,
            [FromServices] ILogger<Program> logger,
            [FromQuery] bool includeDeleted = false,
            [FromQuery] bool deletedOnly = false,
            [FromQuery] string? namePrefix = null,
            [FromQuery] string? version = null,
            [FromQuery] int limit = 100,
            [FromQuery] int offset = 0) =>
        {
            try
            {
                var server = await serverRepo.GetAsync(id);
                if (server == null) return BufferedResults.NotFound(new ErrorResponse($"Server {id} not found"));
                var tools = await toolRepo.ListByServerAsync(id, includeDeleted, namePrefix, version, deletedOnly, limit, offset);
                return BufferedResults.Ok(tools);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error listing tools for MCP server {ServerId}", id);
                return BufferedResults.Problem("An error occurred while listing tools");
            }
        }).WithName("ListMcpTools").WithSummary("List tools for a server");

        // Upsert many tools
        group.MapPut("/servers/{id:guid}/tools", async (
            Guid id,
            [FromBody] McpToolUpsertManyRequest request,
            [FromServices] IMcpServerRepository serverRepo,
            [FromServices] IMcpToolRepository toolRepo,
            [FromServices] ILogger<Program> logger) =>
        {
            try
            {
                var server = await serverRepo.GetAsync(id);
                if (server == null) return BufferedResults.NotFound(new ErrorResponse($"Server {id} not found"));

                var now = DateTimeOffset.UtcNow;
                    var records = request.Tools.Select(t => {
                        var schemaObj = t.Schema ?? new { };
                        var hash = AgentHost.Shared.Util.SchemaHashUtil.Compute(schemaObj);
                        return new McpToolRecord(
                            Id: Guid.NewGuid(),
                            ServerId: id,
                            Name: t.Name,
                            Description: t.Description,
                            Schema: schemaObj,
                            RequiredScopes: t.RequiredScopes ?? Array.Empty<string>(),
                            Version: t.Version,
                            SchemaHash: hash,
                            IsDeleted: false,
                            CreatedAt: now,
                            UpdatedAt: now);
                    });

                await toolRepo.UpsertManyAsync(records);
                logger.LogInformation("Upserted {Count} tools for server {ServerId}", request.Tools.Count(), id);
                var tools = await toolRepo.ListByServerAsync(id);
                return BufferedResults.Ok(tools);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error upserting tools for MCP server {ServerId}", id);
                return BufferedResults.Problem("An error occurred while upserting tools");
            }
        }).WithName("UpsertMcpTools").WithSummary("Upsert tools for a server");

        // Refresh tools (discover + sync)
        group.MapPost("/servers/{id:guid}/refresh-tools", async (
            Guid id,
            [FromServices] IMcpServerRepository serverRepo,
            [FromServices] IMcpToolRepository toolRepo,
            [FromServices] IMcpToolDiscovery discovery,
            [FromServices] IMcpToolAuditLogger audit,
            [FromServices] ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                var server = await serverRepo.GetAsync(id);
                if (server == null) return BufferedResults.NotFound(new ErrorResponse($"Server {id} not found"));
                var discovered = (await discovery.DiscoverAsync(server, ct)).ToList();
                var existing = (await toolRepo.ListByServerAsync(id, includeDeleted: true)).ToList();
                var now = DateTimeOffset.UtcNow;
                var toUpsert = new List<McpToolRecord>();
                foreach (var d in discovered)
                {
                    var schemaHash = AgentHost.Shared.Util.SchemaHashUtil.Compute(d.Schema);
                    var match = existing.FirstOrDefault(x => x.Name == d.Name);
                    if (match == null || match.SchemaHash != schemaHash || match.Version != d.Version || match.Description != d.Description)
                    {
                        var action = match == null ? "Added" : "Updated";
                        toUpsert.Add(new McpToolRecord(
                            Id: match?.Id ?? Guid.NewGuid(),
                            ServerId: id,
                            Name: d.Name,
                            Description: d.Description,
                            Schema: d.Schema,
                            RequiredScopes: d.RequiredScopes,
                            Version: d.Version,
                            SchemaHash: schemaHash,
                            IsDeleted: false,
                            CreatedAt: match?.CreatedAt ?? now,
                            UpdatedAt: now));
                        audit.Log(new McpToolAuditEntry(id, d.Name, action, match?.Version, d.Version, now));
                    }
                }
                var deletedNames = existing.Where(e => discovered.All(d => d.Name != e.Name)).Select(e => e.Name).ToList();
                if (toUpsert.Count > 0) await toolRepo.UpsertManyAsync(toUpsert);
                if (deletedNames.Count > 0) await toolRepo.MarkDeletedAsync(id, deletedNames);
                foreach (var dn in deletedNames)
                {
                    var match = existing.First(e => e.Name == dn);
                    audit.Log(new McpToolAuditEntry(id, dn, "Deleted", match.Version, null, now));
                }
                logger.LogInformation("Refreshed tools for server {ServerId}: {AddedOrUpdated} updated, {Deleted} marked deleted", id, toUpsert.Count, deletedNames.Count);
                var finalList = await toolRepo.ListByServerAsync(id);
                return BufferedResults.Ok(finalList);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error refreshing tools for MCP server {ServerId}", id);
                return BufferedResults.Problem("An error occurred while refreshing tools");
            }
        }).WithName("RefreshMcpTools").WithSummary("Discover and synchronize tools for a server");

        group.MapGet("/servers/{id:guid}/tool-audit", async (
            Guid id,
            [FromServices] IMcpServerRepository serverRepo,
            [FromServices] IMcpToolAuditLogger audit,
            [FromQuery] string? action,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] int limit = 100,
            [FromQuery] int offset = 0) =>
        {
            var server = await serverRepo.GetAsync(id);
            if (server == null) return BufferedResults.NotFound(new ErrorResponse($"Server {id} not found"));
            var entries = audit.List(id, action, from, to, limit, offset);
            return BufferedResults.Ok(entries);
        }).WithName("ListMcpToolAudit").WithSummary("List tool audit entries for a server");
    }
}
