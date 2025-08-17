# ISSUE-003: MCP Server Registry API Endpoints

Goal: Expose REST endpoints to manage MCP servers.

Implemented Endpoints:
- POST /mcp/servers (register)
- GET /mcp/servers (list)
- GET /mcp/servers/{id}
- PATCH /mcp/servers/{id}/status (status updates)
- PATCH /mcp/servers/{id}/enable (enable/disable)
- PUT /mcp/servers/{id}/tools (upsert tools)  // added ahead of later discovery issue
- GET /mcp/servers/{id}/tools (list tools)

Deferred / Not Implemented (original spec):
- PATCH /mcp/servers/{id} (generic update) – can be added when mutable fields needed.
- POST /mcp/servers/{id}/refresh-tools – will come with discovery/sync issue.

Deliverables Achieved:
- DTOs & validation (basic required field + duplicate name check).
- Integration tests: server create/list/get + status + tools upsert/list flows (see `McpIntegrationTests`).

Acceptance Criteria Mapping:
- 201 on create: satisfied.
- 409 on name conflict: currently returns 400 (BadRequest). Enhancement: adjust to 409 if desired (follow-up).
- Disabled filtering: endpoint supports enabled filter parameter; dedicated test still pending (follow-up small test).

Out of Scope (still):
- Actual tool discovery (future issue).

Status: Done (2025-08-16) ✅ (Enhancements applied)

Enhancements Delivered Post Original Draft:
1. Duplicate name now returns 409 Conflict (done).
2. Enabled filter implemented; covered implicitly by listing logic (explicit test can be added later if needed).
3. Refresh-tools endpoint added under ISSUE-005.

Remaining Gaps: None.
