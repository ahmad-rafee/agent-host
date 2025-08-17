## Progress Update (2025-08-17)

Current PoC Status:
- Core pipeline loop (ingest → transform → LLM → act) operational via API + Worker.
- Policy system: model/tool allowlist enforcement plus dynamic, run-scoped extensions (policy_scopes table) exposed through `/runs/{id}` and dedicated scope CRUD endpoints.
- Effective policy transparency (effectiveModels/effectiveTools + warnings) included in run details.
- Timing instrumentation captured in step `timings` JSON (further tracing deferred).
- Tests: 60 passing (API + Worker) with added positive & negative policy coverage; no skipped tests.
- Cost tracking explicitly de-scoped for PoC.

Issue Summary:
- Completed: ISSUE-001 … 006 (MCP persistence, repos, registry API, connection manager stub, discovery & sync, DB list tools), first slice of ISSUE-009 (executor-level policy enforcement & dynamic scopes).
- Partial/Deferred Post-PoC: Remaining slice of ISSUE-009 (DB required_scopes enforcement), and ISSUE-007, 008, 010–016 marked Deferred (focus kept on governance & transparency for demo).

Deferred Focus Areas (post-PoC rationale): Real MCP SDK invocation, pipeline-time dynamic tool resolution, advanced observability (custom spans/metrics), health & reconnect loop, removal of static tool fallback, invocation integration tests, documentation expansion, internal server scaffold, invocation audit logging, DB required scope validation.

Next Recommended Milestones After Demo:
1. Enforce DB `required_scopes` in PolicyValidator (complete ISSUE-009 slice 2).
2. Add MCP observability spans & metrics (ISSUE-010) once invocation path stabilizes.
3. Remove static tool fallback & harden dynamic resolution (ISSUE-012 / ISSUE-008).
4. Introduce server health heartbeat & reconnect (ISSUE-011) for resilience.
5. Real SDK invocation behind feature flag (ISSUE-007).

Confidence: PoC achieves governance + dynamic extension goals with minimal surface area; remaining items are iterative hardening / expansion and intentionally deferred.

---

Here’s a lean PoC structure for C#/.NET that you can run quickly, demo confidently, and grow into the full architecture we outlined. It keeps boundaries clean so you won’t refactor everything later.

PoC Goals (non-negotiables)

Single-tenant, minimal services.

One REST API + one Worker (background) + shared library.

OpenAI-compatible LLM gateway (LiteLLM).

MCP client wrapper (call external tools) with easy swap to real servers.

Postgres (+pgvector optional), MinIO for blobs.

No orchestrator service yet — pipeline loop kept inside Worker with a simple state table.

Ready to split into full services later without breaking APIs/types.

Repository Layout (PoC)
agent-host-poc/
├─ src/
│  ├─ Api/                            # ASP.NET Core Minimal API
│  │  ├─ Endpoints/                   # HTTP endpoints: /healthz, /runs, /artifacts
│  │  ├─ Pipelines/                   # Pipeline definitions + DTOs (PoC: in-memory registry)
│  │  ├─ Composition/                 # DI wiring, options
│  │  ├─ Program.cs
│  │  └─ appsettings.json
│  ├─ Worker/                         # Single BackgroundService to execute pipelines
│  │  ├─ Jobs/                        # Ingest → Enrich → Decide → Act steps
│  │  ├─ Services/                    # ArtifactWriter, PipelineExecutor
│  │  └─ Program.cs
│  └─ Shared/                         # Can be split into packages later
│     ├─ Contracts/                   # Records (Artifact, Run, Step), enums
│     ├─ Clients/
│     │  ├─ LlmClient/                # LiteLLM client (OpenAI-compatible)
│     │  └─ McpClient/                # MCP client wrapper (retry, scopes)
│     ├─ Persistence/                 # Simple Repos (Dapper) + migrations (DbUp)
│     ├─ Vector/                      # Optional pgvector repo (no ANN index in PoC)
│     ├─ Storage/                     # MinIO blob store
│     ├─ Policy/                      # Allowlist, token caps, tool scopes (single org)
│     └─ Observability/               # OpenTelemetry bootstrap + Serilog JSON
├─ infra/
│  ├─ docker-compose.yml              # Postgres(+pgvector), MinIO, LiteLLM, vLLM, Api, Worker
│  ├─ db-init.sql                     # Enable pgvector (optional)
│  └─ litellm/
│     └─ config.yaml                  # Model aliases, budgets (PoC)
├─ tests/
│  ├─ Api.Tests/
│  └─ Worker.Tests/
├─ build/
│  ├─ Directory.Build.props
│  └─ Directory.Build.targets
├─ AgentHost.PoC.sln
└─ README.md

Responsibilities (PoC)
src/Api

Endpoints:

GET /healthz

POST /runs → create run for a named pipeline (stores spec snapshot)

GET /runs/{id} → status + steps

GET /artifacts/{id} → metadata

Policy guard (minimal): model allowlist, token caps, tool-scope check before issuing work.

Pipelines: a simple in-memory registry (or JSON file) of pipeline specs; persisted snapshot per run.

src/Worker

Background loop reads runs table for status=pending, moves to running.

Executes fixed step types (subset of final system):

mcp.pull (e.g., fetch emails/notes via MCP)

transform (normalize → simple mapping)

llm.prompt (analyze → produce JSON plan; validate with FluentValidation)

mcp.call (e.g., create Jira issue)

(optional) vector.embed + rag if you enable pgvector

Writes step rows with input/output JSON, timings, attempts, cost.

Emits minimal structured logs with org_id|run_id|step_id.

src/Shared

Contracts: records for Run, Step, Artifact, Plan.

Clients:

LlmClient: typed wrapper to LiteLLM (chat, embed) with model alias map from config.

McpClient: thin wrapper (HTTP/stdio) with retry (Polly) + scope assertion.

Persistence:

Dapper repos for runs, steps, artifacts, sources.

DbUp migrations (simple SQL) including pgvector enable if opted.

Vector (optional in PoC):

Upsert(artifactId, chunks, embedding[])

Search(query|embedding, k) (plain L2 distance; ANN later).

Storage:

MinIO client; PutObject/GetObject; signed URLs if you need UI previews.

Policy:

Config object with ModelAllowlist, MonthlyBudgetUsd, ToolScopes[].

Observability:

AddOpenTelemetry() for API + Worker; Serilog JSON sink to console.

Minimal DB (PoC)
-- runs
CREATE TABLE runs (
  id UUID PRIMARY KEY,
  pipeline_name TEXT NOT NULL,
  pipeline_spec JSONB NOT NULL,
  status TEXT NOT NULL,               -- pending|running|completed|failed
  costs JSONB DEFAULT '{}'::jsonb,
  created_at timestamptz DEFAULT now(),
  updated_at timestamptz DEFAULT now()
);

-- steps
CREATE TABLE steps (
  id UUID PRIMARY KEY,
  run_id UUID REFERENCES runs(id) ON DELETE CASCADE,
  kind TEXT NOT NULL,                 -- mcp.pull|transform|llm.prompt|mcp.call|vector.embed|rag
  status TEXT NOT NULL,
  input JSONB,
  output JSONB,
  attempt INT DEFAULT 0,
  timings JSONB DEFAULT '{}'::jsonb,
  created_at timestamptz DEFAULT now(),
  updated_at timestamptz DEFAULT now()
);

-- artifacts
CREATE TABLE artifacts (
  id UUID PRIMARY KEY,
  source TEXT NOT NULL,
  type TEXT NOT NULL,                 -- email|note|doc
  uri TEXT NOT NULL,                  -- s3://...
  hash TEXT,
  labels JSONB DEFAULT '{}'::jsonb,
  created_at timestamptz DEFAULT now()
);

-- optional vector table
-- CREATE EXTENSION IF NOT EXISTS vector;
-- CREATE TABLE vectors (artifact_id UUID, chunk_id TEXT, embedding VECTOR(1536), metadata JSONB);

Example PoC pipeline (JSON)
{
  "name": "action-items-from-emails",
  "steps": [
    { "id": "ingest", "kind": "mcp.pull", "tool": "mcp.gmail.listMessages", "params": { "query": "label:inbox newer_than:10m" } },
    { "id": "normalize", "kind": "transform", "fn": "emailToDocV1", "input": "@ingest.items" },
    { "id": "analyze", "kind": "llm.prompt", "model": "chat-default", "system": "Extract actionable items as JSON.", "input": "@normalize.docs" },
    { "id": "act", "kind": "mcp.call", "tool": "mcp.jira.createIssue", "params": { "project": "OPS", "payload": "@analyze.json" } }
  ]
}

docker-compose.yml (PoC)
version: "3.9"
services:
  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: agent
      POSTGRES_USER: agent
      POSTGRES_PASSWORD: agent
    ports: ["5432:5432"]
    volumes: ["pgdata:/var/lib/postgresql/data"]
    healthcheck: { test: ["CMD-SHELL", "pg_isready -U agent"], interval: 5s, timeout: 3s, retries: 10 }

  # Optional pgvector enable (run once or via DbUp)
  # command: ["postgres", "-c", "shared_preload_libraries=pgvector"]

  minio:
    image: minio/minio
    command: server /data --console-address ":9001"
    environment:
      MINIO_ROOT_USER: miniouser
      MINIO_ROOT_PASSWORD: miniopass
    ports: ["9000:9000", "9001:9001"]
    volumes: ["minio:/data"]

  litellm:
    image: ghcr.io/berriai/litellm:latest
    volumes: ["./infra/litellm/config.yaml:/app/config.yaml"]
    environment:
      LITELLM_CONFIG: /app/config.yaml
    ports: ["4000:4000"]

  # Local model (optional) – expose OpenAI-compatible
  vllm:
    image: vllm/vllm-openai:latest
    command: ["--model", "meta-llama/Llama-3.1-8B-Instruct"]
    ports: ["8000:8000"]
    deploy:
      resources:
        reservations:
          devices: [{ capabilities: ["gpu"] }]

  api:
    build: ./src/Api
    environment:
      ConnectionStrings__Db: Host=postgres;Database=agent;Username=agent;Password=agent
      Storage__S3__Endpoint: http://minio:9000
      Storage__S3__Bucket: org-1-artifacts
      Llm__BaseUrl: http://litellm:4000
      Policy__ModelAllowlist__0: chat-default
    ports: ["8080:8080"]
    depends_on: [postgres, minio, litellm]

  worker:
    build: ./src/Worker
    environment:
      ConnectionStrings__Db: Host=postgres;Database=agent;Username=agent;Password=agent
      Storage__S3__Endpoint: http://minio:9000
      Storage__S3__Bucket: org-1-artifacts
      Llm__BaseUrl: http://litellm:4000
    depends_on: [postgres, minio, litellm]

volumes:
  pgdata:
  minio:


In PoC, you may point LiteLLM at vLLM or a cloud model; your code doesn’t change.

API Sketch (PoC)
// Program.cs (Api)
var app = WebApplication.CreateBuilder(args).Build();
app.MapGet("/healthz", () => Results.Ok(new { ok = true }));

app.MapPost("/runs", async (RunRequest req, IRunRepository runs) => {
    var runId = Guid.NewGuid();
    await runs.CreateAsync(new Run(runId, req.PipelineName, req.PipelineSpec, "pending"));
    return Results.Created($"/runs/{runId}", new { runId });
});

app.MapGet("/runs/{id:guid}", async (Guid id, IRunRepository runs, IStepRepository steps) => {
    var run = await runs.GetAsync(id);
    var s = await steps.ListByRunAsync(id);
    return Results.Ok(new { run, steps = s });
});

app.Run();

What’s intentionally not in the PoC

Separate Orchestrator service (kept inside Worker for speed).

BullMQ/MassTransit (use in-process background worker + DB polling).

Full-blown vector/RAG (make it optional).

Complex UI (a simple Postman demo is fine; add Next.js later).

How this PoC evolves into the full structure
PoC Element	Full Architecture Target	Migration Notes
Worker executes pipelines	Separate Orchestrator + per-purpose workers	Move PipelineExecutor to Orchestrator; keep step executors as worker functions
In-process scheduling (DB poll)	Queue/bus (MassTransit/RabbitMQ)	Replace “poll runs” with “StartRun” messages; steps emit events
JSON pipeline definitions in API	DB-backed spec + UI editor	Keep schema; build editor later; no refactor needed
LlmClient (LiteLLM)	same	Same interface; add budgets/fallbacks in gateway config
McpClient wrapper	same	Add policy & circuit-breaker later without breaking calls
Dapper repos	EF Core (optional)	You can keep Dapper; or gradually move hot paths to EF
vectors optional	pgvector + ANN index	Run migrations; swap queries, keep interface
No UI	Next.js dashboard	Reuse API; add /runs viewer and pipeline editor
PoC Acceptance Criteria (tight)

docker compose up brings Postgres, MinIO, LiteLLM, API, Worker.

POST /runs with the sample pipeline ingests a real email/doc (via a mock MCP or live with limited scope), produces a JSON plan, and acts (e.g., creates a Jira issue via MCP).

Idempotency: re-posting the same run doesn’t create duplicate external issues (hash-based dedupe).

Observability: console JSON logs include run_id & step_id; OTel basic traces emitted.

Strong guidance

Keep types/contracts stable from day one — that’s your upgrade path.

Don’t over-engineer RAG in PoC. Prove the business loop (ingest → decide → act) first.

Make policy checks real even in PoC (allowlist, token cap, tool scope). It prevents costly mistakes later.