-- Add schema_hash and is_deleted columns to mcp_tools
ALTER TABLE mcp_tools ADD COLUMN IF NOT EXISTS schema_hash text;
ALTER TABLE mcp_tools ADD COLUMN IF NOT EXISTS is_deleted boolean NOT NULL DEFAULT false;
CREATE INDEX IF NOT EXISTS idx_mcp_tools_server_id_name ON mcp_tools(server_id, name);
