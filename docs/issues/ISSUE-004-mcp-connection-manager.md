# ISSUE-004: MCP Connection Manager (Feature Flagged)

Status: Done (2025-08-16) ✅ (Simulated client, feature-flagged)

Goal: Implement background service that loads enabled servers, attempts connection (placeholder until real SDK), and updates status.

Deliverables:
- `IMcpConnectionManager` + hosted service.
- Feature flag (config: `Mcp:EnableRealConnections`).
- Status transitions: unknown->connecting->ready|error.

Acceptance Criteria:
- On startup enabled servers move to ready (simulated) or error on failure. (Met via mock SdkMcpClient initialization path.)
- Status persisted. (Repository update works.)

Additional Notes:
- Real reconnect & heartbeat logic deferred to ISSUE-011.
Next Steps:
- Implement real SDK connect path + transient failure handling (ISSUE-007 / ISSUE-011).

Out of Scope:
- Real SDK invocation (will come later issue).
