# Phase 1: Logging Infrastructure - Context

**Gathered:** 2026-03-17
**Status:** Ready for planning

<domain>
## Phase Boundary

Add structured NLog logging to the MCP server with programmatic configuration, file/console targets, archive rotation, ASP.NET Core integration, and framework noise suppression. Covers requirements LOG-01 through LOG-12. No new features or capabilities — purely observability infrastructure.

</domain>

<decisions>
## Implementation Decisions

### Log directory
- App-local always: `{AppBaseDirectory}\Log`
- No per-user fallback, no CLI override
- Same path for both console and service mode

### Console target behavior
- Console NLog target enabled ONLY in SSE transport mode
- Disabled entirely in stdio mode (stdout is JSON-RPC protocol, must not be polluted)
- Console layout per convention: time + namespace-stripped format
- Existing `Console.Error.WriteLine()` diagnostic pattern is fully replaced by NLog

### Logger integration depth
- Full replacement of ALL `Console.Error.WriteLine()` calls with NLog logger calls
- Static logger per class pattern (LOG-11): `private static readonly Logger Logger = LogManager.GetCurrentClassLogger();`
- Appropriate log levels: Info for startup/transport events, Error for exceptions, Debug for request details
- Applies to: `Program.cs`, `McpServer.cs`, `SseTransport.cs`, and any other files with stderr writes

### Archive behavior
- Archive unconditionally on every startup — zip all existing `.log` files regardless of age or content
- 10-zip rotation (LOG-10) handles cleanup of accumulated archives
- No debounce or "skip if recent" logic — simple and predictable even in crash-loop scenarios

### Claude's Discretion
- Zip file naming format (timestamp pattern)
- Exact NLog layout strings (within convention constraints)
- Whether to introduce a shared logging helper or keep setup inline in Program.cs
- How to detect SSE vs stdio mode for console target toggle

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### NLog conventions
- `~/.claude/knowledge/nlog-conventions.md` — Programmatic config rules, two-file target pattern, archive-on-startup, async writes, framework noise suppression

### Project conventions
- `.planning/PROJECT.md` — Log directory decision, service name, constraint on programmatic-only NLog config
- `.planning/REQUIREMENTS.md` — LOG-01 through LOG-12 requirement definitions with acceptance criteria

### Existing code
- `src/FlaUI.Mcp/Program.cs` — Entry point where NLog setup, CLI parsing, and shutdown hook will be added
- `src/FlaUI.Mcp/Mcp/McpServer.cs` — Contains `Console.Error.WriteLine()` calls to replace
- `src/FlaUI.Mcp/Mcp/SseTransport.cs` — Contains stderr writes and ASP.NET Core builder (NLog integration point)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- None for logging — no existing NLog or logging framework in the project
- `Program.cs` already has CLI arg parsing loop that can be extended for `-debug` flag

### Established Patterns
- Top-level statements in `Program.cs` (no explicit Main method)
- Manual service instantiation (no DI container) — NLog setup will be inline in Program.cs
- `Console.Error.WriteLine()` used for diagnostics in 3 files (McpServer.cs, SseTransport.cs, Program.cs)
- CancellationTokenSource + Console.CancelKeyPress for graceful shutdown — LogManager.Shutdown() goes in existing `finally` block

### Integration Points
- `Program.cs` finally block — add `LogManager.Shutdown()` alongside existing `sessionManager.Dispose()`
- `SseTransport.cs` ASP.NET Core builder — add `ClearProviders()` + `UseNLog()` for LOG-08
- CLI arg switch in `Program.cs` — extend for `-debug`/`-d` flag
- Transport mode detection — needed to conditionally enable console target

</code_context>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches following the established NLog conventions.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 01-logging-infrastructure*
*Context gathered: 2026-03-17*
