# 002: Direct SDK Client Creation (Per Server)

Status: Proposed
Parent: 001
Labels: mcp, runtime, sdk

## Goal
Create per-server MCP clients directly via `McpClientFactory.CreateAsync` without an intermediate wrapper class.

## Tasks
- [ ] Introduce configuration -> transport mapper (launch command -> `StdioClientTransport`).
- [ ] Add factory delegate: `Func<ServerConfig, CancellationToken, Task<ModelContextProtocol.Client.IMcpClient>>` injected for testability.
- [ ] Implement creation in existing coordinator (current `SdkMcpClient` / future composite) storing `IMcpClient` in dictionary.
- [ ] Ensure disposal path enumerates dictionary and disposes each.
- [ ] Logging: spawn & dispose events include serverId, name, command.

## Acceptance Criteria
- Unit test: server creation success (factory invoked once) & disposal calls dispose.
- Unit test: creation failure logged and does not crash initialization (other servers still create).
- No custom wrapper type added.
