# ISSUE-005: Dynamic Tool Discovery & Sync

Status: Done (2025-08-16) ✅ (Extended beyond original spec)

Goal: After (simulated) connection, fetch tool list from server (mock provider now) and upsert into `mcp_tools`.

Deliverables (Implemented):
- Discovery (client-backed stub) with upsert logic & schema hash comparison.
- Refresh endpoint POST /mcp/servers/{id}/refresh-tools.
- Soft delete of missing tools (is_deleted=true) instead of hard delete.
- Audit logging (add/update/delete) recorded in mcp_tool_audit.
- Filtering: includeDeleted, deletedOnly, namePrefix, version, pagination.

Acceptance Criteria (Revised Achieved):
- Tools inserted or updated when schema hash, version, or description change.
- Removed tools soft deleted (is_deleted flag) and auditable.
- Rediscovery of previously deleted tool reactivates it (clears is_deleted).

Out of Scope (Still):
- Real remote server enumeration (pending ISSUE-007/011).

No open work remaining under this issue.
