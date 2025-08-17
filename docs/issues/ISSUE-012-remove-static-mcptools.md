# ISSUE-012: Remove Static McpTools From Production Path

Status: Deferred (Static fallback retained for tests) ⏸️ (2025-08-17)

Goal: Eliminate reliance on `McpTools` constants at runtime (keep for tests/examples).

Acceptance Criteria (Pending):
- Production code no longer references `McpTools` directly for validation.

Current State:
- Production ListTools now DB-backed (ISSUE-006). Static constants still used for mock fallback and mapping in McpStepExecutor.

Next Steps (Post-PoC):
1. Remove static tool name dependency in McpStepExecutor (derive directly from DB or pass through untouched).
2. Migrate tests relying on McpTools constants to seed DB records instead.
3. Eliminate mock fallback when repository available.
