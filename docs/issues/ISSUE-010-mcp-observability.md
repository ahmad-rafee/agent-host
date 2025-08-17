# ISSUE-010: MCP Observability Metrics & Tracing

Status: Deferred (Post-PoC) ⏸️ (2025-08-17)

Goal: Add tracing spans & metrics for server connect and tool invocation.

Acceptance Criteria (Pending):
- Spans: mcp.server.connect, mcp.tool.invoke.
- Metrics: counters (invocations, failures), histogram (latency).

Current State:
- Generic OTel instrumentation only; no custom MCP spans/counters.

Next Steps (Post-PoC):
1. Add ActivitySource for MCP operations.
2. Record spans in connection manager & tool invocation.
3. Register Meter for counters + histograms.
4. Add test (or manual doc) verifying metrics names.
