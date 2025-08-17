# ISSUE-001: MCP Server & Tool Persistence (DB Migrations)

Status: Done (2025-08-16) ✅

Goal: Introduce database schema to persist MCP servers and their discovered tools (foundation for dynamic, non hard-coded MCP integration).

Deliverables:
- New migrations: `mcp_servers`, `mcp_tools` tables.
- Embedded SQL scripts (DbUp) with idempotent creation.
- Updated migration documentation.

Schema (initial draft):
```sql
CREATE TABLE mcp_servers (
  id UUID PRIMARY KEY,
  name TEXT NOT NULL UNIQUE,
  display_name TEXT NULL,
  type TEXT NOT NULL,                -- stdio|http|ws
  command TEXT NULL,                 -- for stdio
  args TEXT[] NULL,
  endpoint TEXT NULL,                -- for http/ws
  env JSONB NOT NULL DEFAULT '{}'::jsonb,
  is_enabled BOOLEAN NOT NULL DEFAULT true,
  status TEXT NOT NULL DEFAULT 'unknown', -- unknown|connecting|ready|error|disabled
  last_heartbeat_at TIMESTAMPTZ NULL,
  error TEXT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE mcp_tools (
  id UUID PRIMARY KEY,
  server_id UUID NOT NULL REFERENCES mcp_servers(id) ON DELETE CASCADE,
  name TEXT NOT NULL,
  description TEXT NULL,
  schema JSONB NOT NULL,
  required_scopes TEXT[] NOT NULL DEFAULT '{}',
  version TEXT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE(server_id, name)
);
```

Acceptance Criteria (Original):
- Migrations apply cleanly on fresh DB and no-op on existing.
- Tables visible via psql after app start.
- Existing tests still pass.

Extended Implementation (Delivered):
- Additional columns added later (schema_hash, is_deleted) via script 005.
- Audit table (mcp_tool_audit) added by ISSUE-016 groundwork (script 006) – not in original scope but compatible.

Verification:
- DbUp run shows scripts applied once; re-runs are no-op.
- Integration tests (33 total) pass post-migration.

Out of Scope (Still):
- Runtime connection logic (covered by ISSUE-004).
- Tool discovery population (ISSUE-005).

No further action required.
