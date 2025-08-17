# 006: Testing Strategy for MCP Client Refactor

Status: Proposed
Parent: 001
Labels: mcp, testing

## Scope
Detail additional unit & integration tests required to validate refactored MCP client architecture.

## Areas
1. Initialization & Lazy Loading (SDK clients)
2. Tool Listing Consistency (DB snapshot)
3. Chat-based Tool Invocation & Error Surfaces
4. Refresh & Disposal Semantics
5. Concurrency & Thread Safety

## Test Cases
### 1. Initialization & Lazy Loading
- [ ] Initialize coordinator: no SDK clients created until first invocation.
- [ ] First invocation triggers client creation.
- [ ] Subsequent invocation reuses same instance.

### 2. Tool Listing Consistency
- [ ] ListToolsAsync returns DB snapshot unaffected by runtime absence.
- [ ] After refresh tool sync, DB list updated (simulate repository returning new tool).

### 3. Chat-based Invocation
- [ ] Success path returns mapped `McpResponse` with toolUsed metadata.
- [ ] Missing tool -> `Success=false`, `Error=ToolNotFound`.
- [ ] Client creation failure -> `Success=false`, `Error=InvocationFailed`.

### 4. Refresh & Disposal
- [ ] Refresh disposes existing client and clears cache entry.
- [ ] Multiple sequential refreshes do not leak or double-dispose.

### 5. Concurrency
- [ ] Parallel invocations to same uninitialized server only create one runtime.
- [ ] Parallel refresh + invocation uses semaphore: invocation waits or uses new runtime.

## Tooling
- Use fakes for repositories.
- Inject a fake transport/client factory (interface) to count creations & simulate failures.

## Acceptance Criteria
All outlined tests implemented & passing; coverage for coordinator class >80% lines (soft goal).
