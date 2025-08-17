# ISSUE-002: MCP Repositories (CRUD Layer)

Status: Done (2025-08-16) ✅ (No changes since initial completion)

Goal: Implement repository interfaces & concrete Dapper implementations for `mcp_servers` and `mcp_tools`.

Deliverables:
- Interfaces: `IMcpServerRepository`, `IMcpToolRepository` (create, update status, list enabled, upsert tools, get by name/id, disable).
- Implementations with parameterized SQL.
- Unit tests (create/get, status update, tool upsert-many with update path).

Acceptance Criteria:
- Repositories covered by tests (happy path + round trips) – Achieved. Added `McpRepositoryTests` (3 facts) passing in suite (total tests: 33, all succeeded).
- No SQL injection risk (all parameters used).

Out of Scope:
- Background discovery logic.

Notes:
- Extended later to support: includeDeleted, deletedOnly, version filter, name prefix, pagination.
- Soft delete implemented (is_deleted) + MarkDeletedAsync added.
Next Steps: None.
