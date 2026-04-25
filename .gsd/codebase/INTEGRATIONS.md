# External Integrations

**Analysis Date:** 2026-03-17

## APIs & External Services

**None** - This is a standalone MCP server with no external API integrations.

## Data Storage

**Databases:**
- Not used - Stateless server, no persistent storage

**File Storage:**
- Local filesystem only - Screenshots saved as PNG files in memory or to disk on demand

**Caching:**
- None - Session data stored in-memory dictionaries in `SessionManager` and `ElementRegistry` (cleared on shutdown)

## Authentication & Identity

**Auth Provider:**
- None - MCP protocol handles auth at the client level (stdio or HTTP), server is transport-agnostic

**Transport Security:**
- Stdio transport: Inherits authentication from MCP client configuration
- SSE transport: HTTP over unencrypted connection (no TLS/SSL in implementation) — intended for local/intranet use only

## Monitoring & Observability

**Error Tracking:**
- None - No external error tracking service

**Logs:**
- Console-based logging:
  - Errors written to `Console.Error`
  - Server startup messages written to `Console.Error` (e.g., SSE endpoint URLs)
  - Request errors logged to stderr with message only

## CI/CD & Deployment

**Hosting:**
- GitHub Actions - Windows runners for multi-arch builds
- No cloud hosting required (on-premises deployment model)

**CI Pipeline:**
- GitHub Actions (`.github/workflows/build.yml` and `release.yml`)
- Triggered on: push to main, pull requests, git version tags
- Outputs: Self-contained and framework-dependent binaries for win-x64 and win-arm64

## Environment Configuration

**Required env vars:**
- None - Server requires no environment variables

**Secrets location:**
- N/A - No secrets, API keys, or credentials used

## Webhooks & Callbacks

**Incoming:**
- None - Server does not expose webhooks

**Outgoing:**
- None - Server does not call external webhooks

## Windows UI Automation APIs

**Local System Integration:**
- Uses Windows UI Automation (UIA3) via FlaUI - Direct access to system accessibility APIs, not an external integration
- No network communication required
- Operates on local machine only

## Protocol & Communication

**MCP Transport:**
- Stdio (default) - JSON-RPC 2.0 over standard input/output
- SSE (optional) - Server-Sent Events over HTTP with JSON-RPC 2.0 in message bodies
  - Implementation: `SseTransport` in `src/FlaUI.Mcp/Mcp/SseTransport.cs`
  - Endpoints:
    - `GET /sse` - Stream endpoint (sends session ID and waits for messages)
    - `POST /messages?sessionId=<id>` - Message endpoint (receives JSON-RPC requests)
  - No authentication; session ID is per-connection

---

*Integration audit: 2026-03-17*
