# 005: Implement Refresh & Disposal Semantics

Status: Proposed
Parent: 001
Labels: mcp, lifecycle, disposal

## Goal
Ensure safe refresh of tools and per-server SDK MCP clients without process leaks.

## Tasks
- [ ] Track active SDK clients with timestamp & last refresh.
- [ ] Refresh endpoint: after DB sync, dispose existing client; defer recreation until next invocation (lazy) unless eager flag set.
- [ ] Implement `DisposeAsync` on coordinator (SdkMcpClient) enumerating clients.
- [ ] Diagnostic (internal) method returning active server ids & counts for tests.

## Acceptance Criteria
- Unit test: refresh disposes prior client (dispose counter increments, cache entry removed).
- Unit test: final disposal disposes all clients exactly once.
- Acceptance: refresh & audit scenarios remain green.

## Risks
- Concurrent refresh vs invocation: mitigate with per-server semaphore.

## Notes
Per-server semaphore maintained in dictionary; acquire for refresh & invocation creation path; release after.
