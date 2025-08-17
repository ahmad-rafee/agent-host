# AI Coding Agent RULES (AgentHost)
Purpose: Constrain and guide AI contributions. Follow every MUST / NEVER rule. Produce minimal, targeted diffs.

## 1. Core Architecture (Know This Before Editing)
MUST treat solution as 3 parts: `Api` (HTTP endpoints), `Worker` (executes pipeline steps), `Shared` (contracts/clients/repos/policy/storage). Data flow: `POST /runs` -> rows in Postgres -> Worker polling -> step executors -> persist step + artifacts (MinIO) -> external AI (LiteLLM) & MCP servers. JSONB used for flexible fields (spec, input, output, costs, timings).

## 2. Deterministic Mappings (Do Not Invent Names)
Step Kind -> Executor:
`mcp.pull` → `McpStepExecutor`
`mcp.call` → `McpStepExecutor`
`llm.prompt` → `LlmStepExecutor`
`transform` → `TransformStepExecutor`
`pipeline` → `PipelineExecutor`

Repository -> Table:
`RunRepository` → `runs`
`StepRepository` → `steps`
`ArtifactRepository` → `artifacts`
`McpToolRepository` → `mcp_tools`
`McpServerRepository` → `mcp_servers`

## 3. MCP Tool Listing (Critical Rule Set)
ALWAYS use DB-backed `SdkMcpClient.ListToolsAsync` path. NEVER use mock fallback except inside explicitly mock-focused tests. NEVER reintroduce static `McpTools` in production DI paths. Discovery fallback only seeds minimal tools when DB is empty—do not expand without a migration + tests.

## 4. Repositories & Migrations
MUST use Dapper with parameterized SQL. NEVER add Entity Framework. MUST open connection before transactions (see existing repos). When persistence changes schema, MUST add new timestamped script under `src/Shared/Persistence/Scripts` and include a test touching new path.

## 5. Executors & Step Logic
MUST keep executors idempotent w.r.t. persisted step status. On error: ALWAYS persist status + error details; NEVER swallow or just log. When adding a step kind: add executor class under `src/Worker/Jobs`, wire DI in `src/Worker/Program.cs`, update any mapping registry, and add a deterministic test in `tests/Worker.Tests`.

## 6. Contracts & Cross-Service Changes
When modifying DTOs in `src/Shared/Contracts`, MUST update both Api and Worker usages in same change and adjust affected tests (Api + Worker). NEVER leave stale properties referenced.

## 7. Configuration & DI
Add new options by extending `appsettings.json` and binding via existing options pattern. MUST preserve current lifetimes (repos transient/scoped, clients singleton where already). NEVER silently change lifetimes of existing registrations.

## 8. Logging & Tracing
MUST use Serilog structured logging. Include run/step IDs inside executors. For new external calls, wrap in Activity if logical span boundary. NEVER replace Serilog with another logging framework.

## 9. Testing Rules
ALWAYS add or update tests with behavior changes. Use deterministic assertions (poll DB rather than Thread.Sleep). NEVER skip tests unless adding a clear TODO with rationale and a linked issue. Avoid timing flakiness.

## 10. Developer Commands (Use These Forms Exactly)
Build: `make build`  |  Tests: `make test`  |  Full infra: `docker-compose -f infra/docker-compose.yml up -d`  |  Run API: `make run-api`  |  Run Worker: `make run-worker`  |  Add migration: create new timestamped SQL file then rebuild.

## 11. NEVER DO (Hard Guardrails)
NEVER add Entity Framework or ORM abstractions.
NEVER reintroduce static `McpTools` for production paths.
NEVER swallow executor errors (must persist status + error).
NEVER reformat or rename unrelated files/classes.
NEVER introduce broad refactors without a linked issue.
NEVER add new external dependency without necessity + test.
NEVER commit skipped tests without rationale + issue link.
NEVER change public contract shapes silently.

## 12. AI Action Prompts (Follow When Performing Tasks)
If adding executor: create file → implement logic → wire DI → add mapping → add Worker test.
If modifying repository: ensure SQL aligns with table schema → add/adjust migration if schema changes → add test verifying new query path.
If adding endpoint: place in `src/Api/Endpoints` → orchestrate only (no heavy logic) → call repository/client → add integration test in `tests/Api.Tests`.
If touching contracts: update usages across Api + Worker + Tests before finishing.
If adding MCP tool source: insert server + tools via repos → ensure enabled & not soft-deleted → verify via `SdkMcpClient` test.

## 13. Minimal Diff Discipline
Limit edits to required lines. Keep formatting/style identical. Do not reorder unrelated members. Prefer surgical additions.

## 14. Contribution Checklist (Run Every Change)
1. New step kind? Executor + DI + mapping + test added.
2. Persistence change? Migration script + repository update + test.
3. New/changed endpoint? Endpoint file + repo call only + integration test.
4. Shared contract change? Api + Worker + tests updated in same PR.
5. MCP listing logic touched? Confirm DB path retained; no static fallback added.
6. Logging added? Includes run/step IDs where applicable.
7. Run `make build` and `make test` (all green, no unintended skips).
8. Diff review: no unrelated reformatting or dependency creep.

## 15. If Unsure
Default to existing pattern in closest analogous file. Ask (via issue/comment) before adding new architectural concepts.

End of rules. Follow strictly.
