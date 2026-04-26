---
phase: 4-streamable-http-transport
plan: 02
subsystem: transport/http
tags: [http, mcp, transport, sdk-1.2.0, origin-validation, tool-bridge]
tasks_completed: 3
tasks_total: 3
files_created: 4
files_modified: 4
completed: 2026-04-26
requirements: [HTTP-01, HTTP-03, HTTP-04, HTTP-05, HTTP-07]
---

# Phase 4 Plan 02: Streamable HTTP Transport Core Summary

Wave 1 lands the official `ModelContextProtocol.AspNetCore` 1.2.0 SDK behind a runtime adapter (`ToolBridge`) that wires the existing 11 hand-rolled FlaUI `ITool` instances into the SDK's `MapMcp("/mcp")` endpoint without rewriting a single tool with `[McpServerTool]` attributes; the spec-mandated Origin allowlist (D-06) is hand-rolled as middleware since the SDK does not provide one.

## Resumption Note

The previous executor stalled while authoring `HttpTransportFixture.cs` — the diagnosis in the handoff prompt about C# raw-string interpolation (`$"""…{{…}}…"""`) was the right shape of the problem but in the actual on-disk file the agent had already worked around it by using **non-interpolated** raw strings (`"""{"jsonrpc":"2.0",…}"""`) for fixed JSON-RPC bodies and plain string concatenation (`"...\"name\":\"" + toolName + "\"..."`) for the parameterised `tools/call` body in `ToolParityTests.NonUiToolsExecuteOverHttp`. So no JSON literal needed `JsonSerializer.Serialize`. The remaining gaps when this run picked up were:

1. **Missing `using Xunit;` in `HttpTransportFixture.cs`** → `IAsyncLifetime` did not resolve, killing the test build with CS0246. Fixed by adding the using.
2. **Method-name collision** — the fixture had two `InitializeAsync` methods (one from `IAsyncLifetime` returning `Task`, one returning `Task<(string, JsonElement)>`); the helper was unreachable from tests. Renamed the helper to `InitializeMcpAsync` and updated the 4 callers.
3. **Wrong canonical tool names in tests** — plan assumed `Launch / Snapshot / Click / …` but the real `ITool.Name` values are prefixed `windows_*` (e.g. `windows_launch`, `windows_list_windows`, `windows_batch`). Updated `CanonicalTools[]` and the `[InlineData]` rows in `ToolParityTests`.
4. **SDK handler-binding shape** — `McpServerTool.Create(Delegate, McpServerToolCreateOptions)` in 1.2.0 binds delegate parameters by **name** from the JSON-RPC `arguments` dictionary. A `(JsonElement args, CancellationToken ct)` handler caused the SDK to wrap every invocation in `"An error occurred invoking '<tool>'."` when no `args` key was present. Switched the bridge handler to `(RequestContext<CallToolRequestParams> ctx, CancellationToken ct)` (auto-injected by the SDK) and rebuilt the existing `JsonElement` arguments shape in-bridge from `ctx.Params.Arguments`.
5. **Batch tool's argument key** — test sent `{"operations":[]}`, real `windows_batch` schema requires `actions`. Updated test inline data.

No deletions of `obj/`, no `dotnet clean`, no Program.cs wiring (Plan 04 owns that).

## One-Liner

Streamable HTTP transport on `/mcp` via `ModelContextProtocol.AspNetCore` 1.2.0 with a runtime `ITool→McpServerTool` bridge and an Origin-allowlist middleware.

## What Shipped

### Production Code (`src/FlaUI.Mcp/Mcp/Http/`)

- **`HttpTransport.cs`** — public `RunAsync(SessionManager, ElementRegistry, ToolRegistry, string bindAddress, int port, CancellationToken)` and an internal test-overload that surfaces the actually-bound URL via a `TaskCompletionSource<string>` (used when `port = 0`). Wires `WebApplication.CreateBuilder` → NLog → DI singletons → `AddMcpServer().WithHttpTransport(o => { o.Stateless = false; o.EnableLegacySse = false; o.IdleTimeout = 2h; })` → registers every `McpServerTool` from the bridge as a singleton → `app.UseMiddleware<OriginValidationMiddleware>()` BEFORE `app.MapMcp("/mcp")`.
- **`ToolBridge.cs`** — `static IEnumerable<McpServerTool> CreateAll(ToolRegistry registry)` enumerates `registry.GetToolDefinitions()` and creates one `McpServerTool` per definition via `McpServerTool.Create(Delegate, McpServerToolCreateOptions)`. Handler closure routes through `ToolRegistry.ExecuteToolAsync(name, args)` and converts the `McpToolResult` content blocks into the SDK's `CallToolResult` (`TextContentBlock` / `ImageContentBlock` with base64-decoded payload).
- **`OriginValidationMiddleware.cs`** — D-06 allowlist `{ localhost, 127.0.0.1 }` (case-insensitive). Behavior matrix: absent Origin → continue; literal `null` → continue; URI host in allowlist → continue; otherwise log warning + `403 Forbidden` body `Origin not allowed`. Does NOT call `next` after rejection.

### Test Code (`tests/FlaUI.Mcp.Tests/`)

- **`HttpTransportFixture.cs`** (NEW, `IClassFixture` shared across the three test classes) — boots `HttpTransport` on `127.0.0.1:0`, reflects the internal test overload via `BindingFlags.NonPublic | BindingFlags.Static`, captures the Kestrel-chosen URL via `TaskCompletionSource<string>` registered against `app.Lifetime.ApplicationStarted`, exposes `Client` preconfigured with the dual `Accept: application/json, text/event-stream` header, and provides `InitializeMcpAsync()` + `PostJsonRpcAsync(sessionId, body)` helpers. The SSE-framing-aware `ExtractJsonRpcPayload` parses both raw JSON and `data:`-line response bodies because the SDK negotiates between them based on the request's Accept.
- **`HttpTransportTests.cs`** — `MapsMcpEndpoint` (HTTP-01), `EndToEndToolCall` (HTTP-03, sends `notifications/initialized` ack before `tools/call`), `SessionIdLifecycle` (HTTP-04, 400 without session-id, 404 with random GUID).
- **`OriginMiddlewareTests.cs`** — `RejectsExternalOrigin` (HTTP-07) — validates evil Origin → 403; localhost/127.0.0.1/null/absent → not 403.
- **`ToolParityTests.cs`** — `ToolsListReturnsAll11Tools` enumerates the canonical 11 names; `NonUiToolsExecuteOverHttp` Theory invokes `windows_list_windows` (empty args) and `windows_batch` (`{"actions":[]}`) through `tools/call` and asserts no JSON-RPC error and no `isError: true` (HTTP-05 *functional*, no longer name-only).

## Decisions Made

- **D-01 honoured.** SDK adapter path, no hand-rolled streamable-http. All 11 tools untouched.
- **Handler binding via `RequestContext<CallToolRequestParams>`** — confirmed against `ModelContextProtocol.Core.xml` 1.2.0; the SDK auto-injects this type without consuming an `arguments` dictionary slot, giving the bridge access to the raw arguments dictionary so it can synthesise the legacy `JsonElement?` shape the existing `ToolRegistry.ExecuteToolAsync` expects.
- **`MapMcp("/mcp")` accepts the string overload directly.** No `MapGroup("/mcp").MapMcp()` fallback was needed in 1.2.0.
- **`McpServerTool.Create(Delegate, McpServerToolCreateOptions)`** is the SDK signature used; the planning-doc `(name, description, inputSchema, handler)` overload does not exist in 1.2.0.
- **Origin middleware mounted BEFORE `MapMcp`** so external Origins never reach the SDK's session/handshake machinery.
- **`EnableLegacySse = false`** on the HTTP branch (D-03). SSE stays in the legacy `--transport sse` process.

## Origin Behaviour Matrix (HTTP-07 verification)

| Origin header                  | Outcome      |
| ------------------------------ | ------------ |
| (absent)                       | continue     |
| `null` (literal)               | continue     |
| `http://localhost:3020`        | continue     |
| `http://127.0.0.1:3020`        | continue     |
| `https://evil.example.com`     | **403**      |

## Files

### Created
- `src/FlaUI.Mcp/Mcp/Http/HttpTransport.cs`
- `src/FlaUI.Mcp/Mcp/Http/ToolBridge.cs`
- `src/FlaUI.Mcp/Mcp/Http/OriginValidationMiddleware.cs`
- `tests/FlaUI.Mcp.Tests/HttpTransportFixture.cs`

### Modified
- `src/FlaUI.Mcp/FlaUI.Mcp.csproj` (added `ModelContextProtocol.AspNetCore` 1.2.0)
- `tests/FlaUI.Mcp.Tests/HttpTransportTests.cs`
- `tests/FlaUI.Mcp.Tests/OriginMiddlewareTests.cs`
- `tests/FlaUI.Mcp.Tests/ToolParityTests.cs`

## Deviations from Plan

### [Rule 3 - Blocking] Fixture method-name collision
- **Found during:** Test build immediately after the resumption picked up the uncommitted work.
- **Issue:** Two methods named `InitializeAsync` in `HttpTransportFixture` — one from `IAsyncLifetime` (returning `Task`), one returning `Task<(string, JsonElement)>` — overload-resolution ambiguity manifested as `void` deconstruction errors at every call site.
- **Fix:** Renamed the helper to `InitializeMcpAsync`. Updated callers in `HttpTransportTests.cs` and `ToolParityTests.cs`.
- **Commit:** fdf4238

### [Rule 1 - Bug] Wrong canonical tool names
- **Found during:** First passing build's first test run.
- **Issue:** Plan listed tool names as `Launch, Snapshot, Click, …`; actual `ITool.Name` returns `windows_launch, windows_snapshot, windows_click, windows_type, windows_fill, windows_get_text, windows_screenshot, windows_list_windows, windows_focus, windows_close, windows_batch`.
- **Fix:** Updated `CanonicalTools[]` and theory inline-data in `ToolParityTests.cs`. Updated `EndToEndToolCall`'s asserted name in `HttpTransportTests.cs`.
- **Commit:** fdf4238

### [Rule 1 - Bug] SDK handler binding shape
- **Found during:** First test run after canonical-name fix.
- **Issue:** The bridge originally took `(JsonElement args, CancellationToken)` — SDK 1.2.0 binds delegate parameters by name from the JSON-RPC `arguments` dictionary, so `args` was unbound when no `args` key existed and the SDK returned the generic `"An error occurred invoking '<tool>'."`.
- **Fix:** Switched handler signature to `(RequestContext<CallToolRequestParams> ctx, CancellationToken ct)`. Bridge rebuilds the legacy `JsonElement?` shape in-process from `ctx.Params.Arguments` (a `Dictionary<string, JsonElement>`).
- **Commit:** fdf4238

### [Rule 1 - Bug] Wrong batch parameter key
- **Found during:** Final test pass after handler fix.
- **Issue:** Plan body sent `{"operations":[]}` but `BatchTool` schema requires `actions`.
- **Fix:** Updated theory inline data to `{"actions":[]}`.
- **Commit:** fdf4238

## Test Results

```
Bestanden!   : Fehler: 0, erfolgreich: 16, übersprungen: 1, gesamt: 17
```

Only `SseTransportTests.LegacyEndpointsRespond` (HTTP-02) remains skipped — Plan 03 owns it.

## Self-Check: PASSED

- `src/FlaUI.Mcp/Mcp/Http/HttpTransport.cs` — FOUND
- `src/FlaUI.Mcp/Mcp/Http/ToolBridge.cs` — FOUND
- `src/FlaUI.Mcp/Mcp/Http/OriginValidationMiddleware.cs` — FOUND
- `tests/FlaUI.Mcp.Tests/HttpTransportFixture.cs` — FOUND
- Commit fdf4238 — FOUND
- `dotnet test` — 16 passed / 1 skipped / 0 failed
