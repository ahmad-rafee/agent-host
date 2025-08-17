-- Enable pgvector extension (optional)
-- This script is optional and only needed if vector functionality is required
-- Uncomment the following lines to enable vector support

-- CREATE EXTENSION IF NOT EXISTS vector;

-- CREATE TABLE IF NOT EXISTS vectors (
--     artifact_id UUID REFERENCES artifacts(id) ON DELETE CASCADE,
--     chunk_id TEXT NOT NULL,
--     embedding VECTOR(1536),
--     metadata JSONB DEFAULT '{}'::jsonb,
--     created_at TIMESTAMPTZ DEFAULT now(),
--     PRIMARY KEY (artifact_id, chunk_id)
-- );

-- CREATE INDEX IF NOT EXISTS idx_vectors_embedding ON vectors USING ivfflat (embedding vector_l2_ops);
-- CREATE INDEX IF NOT EXISTS idx_vectors_artifact_id ON vectors(artifact_id);
