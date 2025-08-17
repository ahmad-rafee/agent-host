# ISSUE-008: Pipeline Dynamic Tool Resolution

Status: Deferred (Partial work in place) ⏸️ (2025-08-17)

Goal: Pipeline executor resolves tools from DB (server + tool name) before invoking; fails early if not found or disabled.

Acceptance Criteria (Pending):
- Executor logs tool resolution details.
- Unknown tool yields consistent error status on step.

Current State:
- McpStepExecutor now accepts optional IMcpToolRepository (injected) and placeholder validation hook added.

Remaining Work (Post-PoC):
1. Implement actual lookup (requires server context in step spec; currently absent).
2. Add tests for missing/deleted tool producing error.
3. Consider enriching step spec with server ID or name.
Rationale for Deferral: PoC success criteria met without dynamic resolution in pipelines; focus shifted to policy scopes & transparency.
