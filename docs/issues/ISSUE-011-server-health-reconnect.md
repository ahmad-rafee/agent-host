# ISSUE-011: Server Health Monitoring & Reconnect

Status: Deferred (Post-PoC) ⏸️ (2025-08-17)

Goal: Periodic heartbeat & reconnect logic with exponential backoff.

Acceptance Criteria (Pending):
- Failed server transitions to error, attempts reconnect after backoff.
- Metrics reflect status changes.

Current State:
- Initial connect only; no heartbeat/backoff logic.

Next Steps (Post-PoC):
1. Add periodic heartbeat job.
2. Implement exponential backoff per server.
3. Record status change metrics (counters) & log structured events.
