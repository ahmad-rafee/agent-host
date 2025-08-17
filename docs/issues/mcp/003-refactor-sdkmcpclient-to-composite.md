# 003: Simplify SdkMcpClient into Thin Multi-Server Coordinator

Status: Proposed
Parent: 001
Labels: mcp, refactor, composite

## Goal
Retain class name (`SdkMcpClient`) but slim it to a thin coordinator managing multiple SDK `IMcpClient` instances (one per server) with lazy creation.

## Tasks
- [ ] Keep class name stable to avoid churn; remove any internal wrapper usages.
- [ ] Inject: repositories + IServiceScopeFactory + logger + factory delegate for creating SDK clients.
- [ ] `InitializeAsync`: cache enabled server metadata (no process start yet).
- [ ] Maintain `ConcurrentDictionary<Guid, IMcpClient>` for active connections.
- [ ] `GetOrCreateClientAsync(serverId)` with double-checked locking & per-server semaphore.
- [ ] `CallToolAsync`: (placeholder until Issue 004) throw NotImplemented if invoked before routing implemented; will be updated there.
- [ ] `ListToolsAsync`: unchanged DB path.
- [ ] Refresh: dispose existing client (if any) then remove entry; tool sync remains external endpoint responsibility.
- [ ] Implement `DisposeAsync` to dispose all clients.

## Acceptance Criteria
- Acceptance tests remain green.
- Lazy load test passes (client created only on first need).
- Refresh disposes and removes client.
- Logging includes spawn/dispose events.
