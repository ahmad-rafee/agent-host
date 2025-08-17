# PoC TODO & Issue Status

Date: 2025-08-17

This file tracks PoC-scope tasks: what is completed vs. intentionally deferred (not required to demo core ingest → analyze → act loop).

## ✅ Completed (PoC)
- Policy enforcement in LLM & MCP executors (allowlist + tool scope + run-scoped DB extensions)
- Timing instrumentation for step / run execution
- DB-backed policy scopes table + repository (List/Add/Delete)
- Policy contract enrichment (RunId propagation; validation result includes warnings, effective models/tools)
- Effective policy exposure via `GET /runs/{id}`
- Policy scope creation endpoint `POST /runs/{id}/policy-scopes`
- Policy scope listing & deletion endpoints (`GET` / `DELETE`)
- Integration test: dynamic model allowance after adding scope
- Worker tests: positive enablement after scope addition (LLM + MCP) and negative enforcement scenarios
- Migration script 007 applied for policy scopes
- Refactoring test suite to align with new contracts (all tests green: 60)

## ⏸ Deferred (Post-PoC Enhancements)
| Area | Item | Rationale for Deferral |
|------|------|------------------------|
| Policy | Derive effective models/tools from original pipeline spec (not step heuristic) | Demo OK with heuristic; correctness improvement later |
| Policy | In-memory cache for `policy_scopes` (TTL + invalidation) | Premature optimization until perf needs observed |
| Policy | Additional indexes: `(run_id, allowed_tool)`, `(run_id, allowed_model)` | Only beneficial with larger scope volumes |
| Policy | Expose `effectiveScopes` in API responses | Not required for initial showcase |
| Policy | Refine `EffectiveTools` to always return authoritative union (no fallback to request tools) | Cosmetic accuracy enhancement |
| Policy | Negative test for revoked scope (post-delete denial) | Edge-case scenario beyond core demo |
| Policy | Normalize tool name utility (shared) | Internal refactor; no user-facing value now |
| Policy | Cost warning only when model has cost configured | Minor UX improvement |
| Policy | Optimistic concurrency & duplicate prevention (unique constraint + proper 404/409) | Adds migration + complexity not needed in demo |
| Policy | PolicyValidator instrumentation (counts, Activity tags) | Observability nice-to-have |
| Policy | Scope deletion audit logging | Audit trail overkill for PoC |
| Performance | Scope cache hit/miss metrics | Depends on cache implementation |
| Persistence | Unique partial index for (run_id, allowed_model/allowed_tool, scope) to prevent duplicates | Only needed with higher concurrency |

## 🎯 Next Suggested Milestones After PoC
1. Accurate effective policy derivation from pipeline spec.
2. Add indexes + lightweight caching if scope lookups become hot.
3. Add `effectiveScopes` to run details for full transparency.
4. Introduce revocation test + duplicate prevention (schema & test).
5. Enhanced tracing/metrics around policy merge.

## Mapping to Original Goals
- Reliability (tests unskipped & extended): Completed
- Governance (policy enforcement, dynamic scopes, transparency): Completed
- Observability (timings; further tracing deferred): Partially Complete
- Cost tracking: Explicitly de-scoped per user direction

## Test Baseline
Current test count: 60 passing (Api + Worker). No skips introduced.

---
Generated during PoC; adjust as priorities evolve.
