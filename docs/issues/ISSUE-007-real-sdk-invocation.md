# ISSUE-007: Real SDK Tool Invocation (Flagged)

Status: Deferred (PoC scope excludes real SDK) ⏸️ (2025-08-17)

Goal: Integrate official MCP .NET client to perform real tool calls when feature flag enabled; fallback to mock otherwise.

Acceptance Criteria (Pending):
- Invocation path chooses real vs mock depending on config flag.
- Errors surfaced with standardized structure.

Current State:
- Only mock SimulateToolCallAsync path exists.
- Decision: Real SDK integration deferred until after PoC stabilization (policy & dynamic scope features prioritized).

Next Steps (Post-PoC):
1. Add feature flag (e.g., Mcp:UseRealSdk).
2. Integrate real MCP client SDK call (tool execution) with cancellation & timeout.
3. Map SDK errors to uniform McpResponse (include error code).

Out of Scope:
- Server hosting (third-party only for now).
