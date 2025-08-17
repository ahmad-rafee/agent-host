-- MCP Tool Audit table
CREATE TABLE IF NOT EXISTS mcp_tool_audit (
    id UUID PRIMARY KEY,
    server_id UUID NOT NULL REFERENCES mcp_servers(id) ON DELETE CASCADE,
    tool_name TEXT NOT NULL,
    action TEXT NOT NULL, -- Added | Updated | Deleted
    old_version TEXT,
    new_version TEXT,
    timestamp TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_mcp_tool_audit_server_time ON mcp_tool_audit(server_id, timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_mcp_tool_audit_server_action ON mcp_tool_audit(server_id, action);
