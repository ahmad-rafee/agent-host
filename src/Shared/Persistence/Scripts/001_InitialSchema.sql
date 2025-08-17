-- Create runs table
CREATE TABLE IF NOT EXISTS runs (
    id UUID PRIMARY KEY,
    pipeline_name TEXT NOT NULL,
    pipeline_spec JSONB NOT NULL,
    status TEXT NOT NULL,               -- pending|running|completed|failed
    costs JSONB DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ DEFAULT now(),
    updated_at TIMESTAMPTZ DEFAULT now()
);

-- Create steps table
CREATE TABLE IF NOT EXISTS steps (
    id UUID PRIMARY KEY,
    run_id UUID REFERENCES runs(id) ON DELETE CASCADE,
    kind TEXT NOT NULL,                 -- mcp.pull|transform|llm.prompt|mcp.call|vector.embed|rag
    status TEXT NOT NULL,
    input JSONB,
    output JSONB,
    attempt INT DEFAULT 0,
    timings JSONB DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ DEFAULT now(),
    updated_at TIMESTAMPTZ DEFAULT now()
);

-- Create artifacts table
CREATE TABLE IF NOT EXISTS artifacts (
    id UUID PRIMARY KEY,
    source TEXT NOT NULL,
    type TEXT NOT NULL,                 -- email|note|doc
    uri TEXT NOT NULL,                  -- s3://...
    hash TEXT,
    labels JSONB DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ DEFAULT now()
);

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS idx_runs_status ON runs(status);
CREATE INDEX IF NOT EXISTS idx_runs_created_at ON runs(created_at);
CREATE INDEX IF NOT EXISTS idx_steps_run_id ON steps(run_id);
CREATE INDEX IF NOT EXISTS idx_steps_status ON steps(status);
CREATE INDEX IF NOT EXISTS idx_artifacts_type ON artifacts(type);
CREATE INDEX IF NOT EXISTS idx_artifacts_source ON artifacts(source);
