# ISSUE-009: Policy Validator Uses DB Tool Scopes

Status: Slice 1 Done ✅ / Slice 2 Deferred ⏸️ (2025-08-17)

Goal: Replace static scope mapping with scopes from `mcp_tools.required_scopes`.

Acceptance Criteria (Slice 1 - Done):
- Per-step policy enforcement for model allowlist + inferred scope mapping.
- Negative tests prevent MCP & LLM execution when invalid.

Acceptance Criteria (Slice 2 - Pending):
- Policy check queries tool scopes from DB (`mcp_tools.required_scopes`).
- Unit tests cover allowed + missing DB scopes.

Current State:
- Scopes stored in DB (required_scopes) but not enforced at runtime.
- Additional dynamic run-level model/tool scope extension implemented (separate PoC deliverable) enabling on-the-fly allowances.

Implementation Notes:
- Added optional `IPolicyValidator` injection into `McpStepExecutor` and `LlmStepExecutor`.
- Early-return failure path before any external call when policy invalid.
- Logging: warning with concatenated error list.

Next Steps (Deferred to Post-PoC):
1. Repository accessor already holds required_scopes; add retrieval in validator when tool list present.
2. Extend PolicyValidationRequest or internal lookup to gather required scopes.
3. Enforce missing-scope failures with aggregated error list.
4. Add unit tests covering authorized, unauthorized, partial.
5. Remove deferral marker and mark Complete.

Risks / Considerations:
- Additional DB round trips per step (mitigate via per-run cache once implemented).
- Tool rename / deletion during run; handle by re-query returning null -> treat as policy failure.
- Must ensure no regression to dynamic run-scope overrides when integrating DB required scopes.
