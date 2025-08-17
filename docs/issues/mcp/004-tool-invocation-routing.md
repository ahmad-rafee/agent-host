# 004: Implement Tool Invocation via Chat + MCP SDK

Status: Proposed
Parent: 001
Labels: mcp, tools, invocation

## Goal
Route `IMcpClient.CallToolAsync` through underlying SDK clients for the owning server, translating parameters & responses to existing `McpResponse` contract.

## Design Notes
- Follow quickstart pattern: obtain `IChatClient` (Azure OpenAI via `Microsoft.Extensions.AI`) and pass available MCP tools in request options `Tools = [..tools]`.
- For single-tool invocation, create a minimal chat (system + user prompt) encouraging or explicitly requesting the tool; parse streaming updates to extract final tool output.
- If SDK later exposes direct `CallToolAsync`, can swap implementation transparently.

## Tasks
- [ ] Repository method: resolve tool + server by tool name.
- [ ] Serialize parameters (System.Text.Json) and inject into user prompt (e.g., "Invoke tool <name> with: {json}").
- [ ] Acquire / create server MCP client & enumerate its tools (or use cached DB record for shape) to build tool list for chat client.
- [ ] Execute streaming chat, collate updates, extract tool invocation result or fallback to model response.
- [ ] Map outcome into `McpResponse` (Success, Result, Error, Metadata: timings, toolUsed, serverId).
- [ ] Structured log (ToolInvoked) with {toolName, serverId, durationMs, success}.
- [ ] Failure modes: ToolNotFound, ServerNotFound, InvocationFailed.

## Acceptance Criteria
- Unit test: success path (fake chat) returns Success=true & metadata has toolUsed.
- Unit test: missing tool returns ToolNotFound error.
- Unit test: server client creation failure returns InvocationFailed.
