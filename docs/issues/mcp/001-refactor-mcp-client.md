# 001: Adopt Official MCP & Chat SDK (Minimal Integration)

Status: Proposed
Created: 2025-08-17
Owner: TBD
Labels: mcp, client, refactor, backend

## Summary
Replace bespoke `SdkMcpClient` logic with direct usage of the official `ModelContextProtocol` C# SDK plus `Microsoft.Extensions.AI` chat abstractions (per quickstart). Keep DB-backed server + tool persistence and auditing. Avoid custom wrappers unless a hard requirement emerges (multi-server orchestration kept as a thin coordinator only).

## Motivation
- Align with upstream MCP patterns and reduce maintenance.
- Improve reliability & lifecycle management (process disposal, multi-server handling).
- Enable future transports (TCP/WebSocket) with minimal change.
- Simplify testability via standard SDK abstractions.

## Out of Scope
- DB schema changes (unless gaps emerge).
- Adding new server configuration types beyond current launch command pattern.
- Reworking policy enforcement or pipeline executors.

## High-Level Design
1. Add NuGet packages (exact as quickstart):
   - `ModelContextProtocol` (prerelease pin)
   - `Azure.AI.OpenAI` (prerelease if required for target model) / `Azure.Identity`
   - `Microsoft.Extensions.AI` & `Microsoft.Extensions.AI.OpenAI`
2. Replace current custom initialization with direct per-server creation via `McpClientFactory.CreateAsync` + `StdioClientTransport` (command + args from DB config).
3. Maintain a lightweight dictionary: `serverId -> IMcpClient` (SDK). No additional wrapper types unless a test seam is needed (then use factory interface only).
4. Tool listing stays DB-driven (`ListToolsAsync` unchanged) to satisfy persistence + policy rules.
5. Tool invocation path (later issue) will construct a transient chat session using `IChatClient` with `Tools = [.. tools]` constrained to the target tool.
6. Refresh: dispose and recreate only the affected `IMcpClient` instance.
7. Disposal: central coordinator implements `IAsyncDisposable` enumerating and disposing active SDK clients.

## Interfaces / Contracts
Decision point: either
 A) Remove custom `IMcpClient` interface and use SDK `IMcpClient` everywhere, OR
 B) Retain thin interface mirroring SDK signatures for isolation.
Initial step: keep existing interface but mark for potential removal after adoption (add TODO comment). Any change requires synchronized Api + Worker + tests update per repo rules.

## Error Handling
- Per-server startup failures logged & skipped; aggregate warning reported.
- Tool invocation when server runtime missing triggers lazy init; if still failing returns `McpResponse(Success: false, Error: ...)`.

## Observability
- Retain existing log messages: initialization counts, tool listing lines ("Found X MCP tools (DB)").
- Add structured log for tool invocation (later issue): {toolName, serverId, durationMs, success}.
- Add log on each SDK client spawn & disposal.

## Testing Strategy
- Unit tests: multi-server initialization (no wrappers), lazy creation, refresh disposal.
- Acceptance unchanged (MCP lifecycle feature must stay green).
- Introduce injectable factory delegate to allow test fakes without wrapper proliferation.

## Risks
| Risk | Mitigation |
|------|------------|
| Process leak on refresh | Dispose SDK client before recreation |
| Startup latency | Lazy per-server creation |
| Interface churn | Delay removal of custom interface until stable |
| Invocation complexity | Separate issue handles chat/tool invocation mapping |

## Definition of Done
- Packages added & restore succeeds.
- All tests pass (unit + acceptance: 68+ baseline still green).
- Active servers instantiate SDK clients; disposal on refresh validated.
- Tool listing behavior unchanged (same DB code path).
- Logging includes spawn/dispose events.

## Follow-Ups
See companion issues 002–006 (updated to reflect minimal integration path).
