# ISSUE-013: Integration Tests – Dynamic MCP

Status: Deferred (Invocation portion) ⏸️ (2025-08-17)

Goal: Add tests that register an MCP server, trigger discovery, list tools, and invoke a tool (mock or real depending on flag).

Acceptance Criteria (Pending Partially Met):
- Register -> refresh-tools -> list: Covered by existing integration tests. ✅
- Invoke phase: NOT implemented (no invocation endpoint test). ❌

Next Steps (Post-PoC):
1. Expose tool invocation endpoint (if within scope) or worker step test harness.
2. Add integration test invoking a tool and asserting mock response schema.
