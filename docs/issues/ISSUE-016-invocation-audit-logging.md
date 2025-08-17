# ISSUE-016: Invocation Audit Logging (Deferred)

Status: Deferred (Reaffirmed 2025-08-17) 🚧 (Tool lifecycle audit delivered separately)

Goal: (Deferred) Persist each tool invocation for audit (later compliance needs).

Acceptance Criteria (future):
- New table mcp_invocations capturing server_id, tool, duration, success, error.

Note:
- Tool lifecycle audit (add/update/delete) implemented under ISSUE-005 extension (table: mcp_tool_audit) but invocation audit still not started.

Next Steps (When Activated Post-PoC):
1. Create mcp_invocations table (design as original).
2. Emit record on each tool invocation (success/failure, duration ms, error text).
3. Add endpoint /mcp/servers/{id}/invocations (filters: tool, from, to, success).
