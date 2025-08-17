-- Policy scopes table for DB-backed policy enforcement
CREATE TABLE IF NOT EXISTS policy_scopes (
    id UUID PRIMARY KEY,
    run_id UUID NOT NULL REFERENCES runs(id) ON DELETE CASCADE,
    allowed_model TEXT NULL,
    allowed_tool TEXT NULL,
    scope TEXT NULL,
    created_at TIMESTAMPTZ DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_policy_scopes_run_id ON policy_scopes(run_id);
CREATE INDEX IF NOT EXISTS idx_policy_scopes_model ON policy_scopes(allowed_model);
CREATE INDEX IF NOT EXISTS idx_policy_scopes_tool ON policy_scopes(allowed_tool);
