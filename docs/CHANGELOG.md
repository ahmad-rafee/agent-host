# Changelog

All notable changes to this project will be documented in this file.

## [2025-08-17] PoC Policy & Documentation Consolidation
### Added
- Dynamic run-scoped policy extensions: `policy_scopes` table + repository (Add/List/Delete).
- Effective policy exposure on `GET /runs/{id}` (effectiveModels, effectiveTools, warnings).
- Policy scope management endpoints: `POST /runs/{id}/policy-scopes`, `GET /runs/{id}/policy-scopes`, `DELETE /runs/{id}/policy-scopes/{scopeId}`.
- Positive enforcement tests proving previously disallowed model/tool becomes allowed after scope addition (LLM & MCP executors).
- Documentation: Updated `Project Plan.md` with progress section; added `TODO.md` with completed vs deferred tasks.

### Changed
- Policy validator now merges static config with DB-provided run-scoped allowances and returns enriched validation result.
- ISSUE docs statuses: Marked 007–016 as Deferred (post-PoC) with rationale; ISSUE-009 split into Slice 1 (done) / Slice 2 (deferred).

### Deferred (Not in this release)
- Real MCP SDK invocation, pipeline dynamic tool resolution, DB required_scopes enforcement, advanced MCP observability, health & reconnect loop, removal of static tool fallback, invocation audit logging, internal MCP server scaffold.

### Removed / Dropped
- Cost tracking for PoC scope (explicitly de-scoped; may reintroduce later under budgeting initiative).

### Test Summary
- Total tests: 60 passing (API + Worker). No skipped tests; new positive policy enablement tests added.

---
Previous changes prior to 2025-08-17 are captured in issue-specific documentation under `docs/issues/`.
