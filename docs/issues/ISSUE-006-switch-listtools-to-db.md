# ISSUE-006: Switch ListTools to DB

Status: Done (2025-08-16) ✅

Goal: Replace mock `GetMockTools` usage with DB query through repository. Remove direct dependency on static `McpTools` for production path.

Acceptance Criteria (Pending):
- `ListToolsAsync` (SdkMcpClient) returns DB tools (currently still returns mock tool set).
- Tests updated to rely on DB content instead of `GetMockTools`.

Implementation Notes:
- Added optional repository constructor; DI now injects repositories.
- ListToolsAsync queries enabled servers and non-deleted tools; falls back to mock only if repos absent (test backwards compatibility).
- New test `SdkMcpClientDbListToolsTests` validates DB listing path.
Remaining (Optional): Remove fallback once all tests use repo path.
