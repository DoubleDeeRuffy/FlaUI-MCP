---
phase: 4-streamable-http-transport
plan: 02
type: execute
wave: 1
depends_on: [4-01]
files_modified:
  - src/FlaUI.Mcp/FlaUI.Mcp.csproj
  - src/FlaUI.Mcp/Mcp/Http/HttpTransport.cs
  - src/FlaUI.Mcp/Mcp/Http/ToolBridge.cs
  - src/FlaUI.Mcp/Mcp/Http/OriginValidationMiddleware.cs
  - tests/FlaUI.Mcp.Tests/HttpTransportTests.cs
  - tests/FlaUI.Mcp.Tests/OriginMiddlewareTests.cs
  - tests/FlaUI.Mcp.Tests/ToolParityTests.cs
autonomous: true
requirements:
  - HTTP-01
  - HTTP-03
  - HTTP-04
  - HTTP-05
  - HTTP-07
must_haves:
  truths:
    - A POST to /mcp with the dual Accept header (application/json + text/event-stream) gets a JSON-RPC initialize response and an Mcp-Session-Id header.
    - "A subsequent tools/list and tools/call over the same /mcp session sees all 11 existing FlaUI tools and can invoke non-UI tools (ListWindows, Batch) returning JSON-RPC results."
    - A request whose Origin header points outside { localhost, 127.0.0.1 } is answered with HTTP 403.
    - Existing ITool registry instances are reused — tools are NOT rewritten with [McpServerTool] attributes.
    - "ToolParityTests functionally invokes ListWindows and an empty Batch over /mcp on the http transport (HTTP-05 — no longer name-only)."
  artifacts:
    - path: src/FlaUI.Mcp/Mcp/Http/ToolBridge.cs
      provides: ITool to McpServerTool adapter for all 11 registered tools
      min_lines: 20
    - path: src/FlaUI.Mcp/Mcp/Http/HttpTransport.cs
      provides: streamable HTTP host startup and routing on /mcp
      min_lines: 40
    - path: src/FlaUI.Mcp/Mcp/Http/OriginValidationMiddleware.cs
      provides: spec-required Origin allowlist (SDK does not supply)
      min_lines: 25
    - path: tests/FlaUI.Mcp.Tests/ToolParityTests.cs
      provides: functional HTTP-05 parity test invoking non-UI tools
      contains: "ListWindows"
  key_links:
    - from: src/FlaUI.Mcp/Mcp/Http/HttpTransport.cs
      to: src/FlaUI.Mcp/Mcp/Http/OriginValidationMiddleware.cs
      via: app.UseMiddleware
      pattern: "UseMiddleware<OriginValidationMiddleware>"
    - from: src/FlaUI.Mcp/Mcp/Http/HttpTransport.cs
      to: src/FlaUI.Mcp/Mcp/Http/ToolBridge.cs
      via: ToolBridge.CreateAll(toolRegistry) on startup
      pattern: "ToolBridge\\.CreateAll"
    - from: src/FlaUI.Mcp/Mcp/Http/HttpTransport.cs
      to: ModelContextProtocol.AspNetCore SDK
      via: AddMcpServer + WithHttpTransport + MapMcp
      pattern: "(AddMcpServer|MapMcp)"
---

<objective>
Wave 1 — implement the Streamable HTTP transport core: introduce the `ModelContextProtocol.AspNetCore` 1.2.0 SDK package, build a tool-bridge that adapts the existing hand-rolled `ITool` registry to SDK `McpServerTool` instances, host the SDK via `MapMcp("/mcp")`, and add the Origin-validation middleware that D-06 mandates.

Purpose: D-01 locks in the SDK path (no hand-rolled streamable-http). The bridge approach keeps all 11 existing FlaUI tools untouched and preserves the `ElementRegistry`/`SessionManager` lifetimes. Origin middleware is small but spec-required, and the SDK does not provide it.

Output: A self-contained `HttpTransport.RunAsync(...)` entry point that Plan 04 will call from `Program.cs`. Plan 03 (now Wave 2) consumes the `OriginValidationMiddleware` produced here. Wave 1 ends with HTTP-01/03/04/05/07 tests turned from skipped to passing.
</objective>

<execution_context>
@$HOME/.claude-account2/get-shit-done/workflows/execute-plan.md
@$HOME/.claude-account2/get-shit-done/templates/summary.md
</execution_context>

<context>
@.gsd/PROJECT.md
@.gsd/milestones/1.0/4-streamable-http-transport/4-CONTEXT.md
@.gsd/milestones/1.0/4-streamable-http-transport/4-RESEARCH.md
@.gsd/milestones/1.0/4-streamable-http-transport/4-VALIDATION.md
@src/FlaUI.Mcp/FlaUI.Mcp.csproj
@src/FlaUI.Mcp/Mcp/McpServer.cs
@src/FlaUI.Mcp/Mcp/ToolRegistry.cs
@src/FlaUI.Mcp/Mcp/SseTransport.cs

<interfaces>
Existing tool surface (do NOT modify): `PlaywrightWindows.Mcp.Core.ITool` defines `Name`, `Description`, `InputSchema (JsonElement or JsonNode)`, `Task<JsonElement> ExecuteAsync(JsonElement args, CancellationToken)`. `ToolRegistry.GetAll()` returns `IEnumerable<ITool>`. The 11 registered tools are listed in `Program.cs` lines 242-253 (Launch, Snapshot, Click, Type, Fill, GetText, Screenshot, ListWindows, FocusWindow, CloseWindow, Batch).

Non-UI tools safe to invoke in automated parity tests:
- `ListWindows` — pure read of OS window list, no UI side effect.
- `Batch` — with empty `operations` array returns `{ results: [] }` without touching UI.
All others (Launch/Click/Type/Fill/Screenshot/etc.) require real Windows windows and stay manual via `[Trait("Category", TestCategories.Manual)]`.

SDK 1.2.0 surface (per research):

```csharp
builder.Services
    .AddMcpServer()
    .WithHttpTransport(opts => {
        opts.Stateless = false;
        opts.IdleTimeout = TimeSpan.FromHours(2);
        opts.EnableLegacySse = false;  // D-03
    });
McpServerTool.Create(
    name: string,
    description: string,
    inputSchema: JsonElement,
    handler: Func<JsonElement, CancellationToken, Task<JsonElement>>);
app.MapMcp("/mcp");
```

If the 1.2.0 `MapMcp` overload does NOT accept a string pattern (research flagged LOW-confidence), fall back to `app.MapGroup("/mcp").MapMcp();` or `app.Map("/mcp", branch => branch.MapMcp());` — pick whichever compiles and document in SUMMARY.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Add SDK package and build the ITool→McpServerTool bridge</name>
  <files>
    <file>src/FlaUI.Mcp/FlaUI.Mcp.csproj</file>
    <file>src/FlaUI.Mcp/Mcp/Http/ToolBridge.cs</file>
  </files>
  <read_first>
    <file>src/FlaUI.Mcp/FlaUI.Mcp.csproj</file>
    <file>src/FlaUI.Mcp/Mcp/ToolRegistry.cs</file>
    <file>.gsd/milestones/1.0/4-streamable-http-transport/4-RESEARCH.md</file>
  </read_first>
  <action>
  Add `<PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.2.0" />` to `src/FlaUI.Mcp/FlaUI.Mcp.csproj`. Run `dotnet restore src/FlaUI.Mcp/FlaUI.Mcp.csproj`.

  Create `src/FlaUI.Mcp/Mcp/Http/ToolBridge.cs` with namespace `FlaUI.Mcp.Mcp.Http`. Provide a static class `ToolBridge` with method:

  ```csharp
  public static IEnumerable<McpServerTool> CreateAll(ToolRegistry registry)
  {
      foreach (var tool in registry.GetAll())
      {
          yield return McpServerTool.Create(
              name: tool.Name,
              description: tool.Description ?? tool.Name,
              inputSchema: tool.InputSchema,
              handler: async (args, ct) => await tool.ExecuteAsync(args, ct));
      }
  }
  ```

  If the SDK's `McpServerTool.Create` signature differs, inspect via LSP `goToDefinition` after restore and adapt. Contract: every existing ITool round-trips arguments and result identically. If `tool.InputSchema` is a JSON string, parse via `JsonDocument.Parse(...).RootElement`. If the SDK exposes only attribute-based registration, STOP and surface a checkpoint — rewriting all 11 tools exceeds Phase 4 scope.
  </action>
  <verify>
  `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo` succeeds. Package present: `Select-String src/FlaUI.Mcp/FlaUI.Mcp.csproj -Pattern 'ModelContextProtocol\.AspNetCore.*1\.2\.0'` returns 1 hit. Bridge file: `Select-String src/FlaUI.Mcp/Mcp/Http/ToolBridge.cs -Pattern 'McpServerTool\.Create'` returns 1 hit.

  <automated>dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo</automated>
  </verify>
  <acceptance_criteria>
    - `Test-Path src/FlaUI.Mcp/Mcp/Http/ToolBridge.cs` returns True.
    - `Select-String src/FlaUI.Mcp/FlaUI.Mcp.csproj -Pattern 'ModelContextProtocol\.AspNetCore'` returns ≥1 hit.
    - `Select-String src/FlaUI.Mcp/FlaUI.Mcp.csproj -Pattern 'Version="1\.2\.0"'` returns ≥1 hit (in same ItemGroup).
    - `Select-String src/FlaUI.Mcp/Mcp/Http/ToolBridge.cs -Pattern 'McpServerTool\.Create'` returns ≥1 hit.
    - `Select-String src/FlaUI.Mcp/Mcp/Http/ToolBridge.cs -Pattern 'CreateAll'` returns ≥1 hit.
    - `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo` exits 0.
  </acceptance_criteria>
  <done>SDK package referenced at version 1.2.0; `ToolBridge.CreateAll` enumerates all 11 tools as `McpServerTool` instances; build is green.</done>
</task>

<task type="auto">
  <name>Task 2a: Implement HttpTransport host + Origin middleware</name>
  <files>
    <file>src/FlaUI.Mcp/Mcp/Http/HttpTransport.cs</file>
    <file>src/FlaUI.Mcp/Mcp/Http/OriginValidationMiddleware.cs</file>
  </files>
  <read_first>
    <file>src/FlaUI.Mcp/Mcp/Http/ToolBridge.cs</file>
    <file>src/FlaUI.Mcp/Mcp/SseTransport.cs</file>
    <file>.gsd/milestones/1.0/4-streamable-http-transport/4-RESEARCH.md</file>
  </read_first>
  <action>
  Create `src/FlaUI.Mcp/Mcp/Http/OriginValidationMiddleware.cs` (namespace `FlaUI.Mcp.Mcp.Http`) per research §Pattern 3. Allowed hosts: `localhost`, `127.0.0.1` (case-insensitive). Behavior:

  - Origin header absent → continue (CLI/native clients).
  - Origin equals literal string `"null"` → continue (HTML5 sandboxed iframe; D-06 explicit).
  - Origin parses as Uri AND host is in allowlist → continue.
  - Otherwise → respond `403 Forbidden` with body `"Origin not allowed"` and log a warning via `ILogger<OriginValidationMiddleware>` containing the rejected Origin string. Do NOT call `next` after 403.

  Create `src/FlaUI.Mcp/Mcp/Http/HttpTransport.cs` (same namespace) with:

  ```csharp
  public static class HttpTransport
  {
      public static async Task RunAsync(
          SessionManager sessionManager,
          ElementRegistry elementRegistry,
          ToolRegistry toolRegistry,
          string bindAddress,
          int port,
          CancellationToken cancellationToken);
  }
  ```

  Implementation per research §Pattern 1: `WebApplication.CreateBuilder()` → `UseUrls($"http://{bindAddress}:{port}")` → `Logging.ClearProviders()` + `Host.UseNLog()` → register sessionManager/elementRegistry/toolRegistry as singletons → `AddMcpServer().WithHttpTransport(o => { o.Stateless = false; o.EnableLegacySse = false; })` → register tools by iterating `ToolBridge.CreateAll(toolRegistry)` and adding via the SDK's tool-registration API → `Build()` → `app.UseMiddleware<OriginValidationMiddleware>()` BEFORE `app.MapMcp("/mcp")` → `await app.RunAsync(cancellationToken)`. Log `Info` on startup with the bind URL.

  If the SDK 1.2.0 `MapMcp` does not accept a string pattern, fall back to `app.Map("/mcp", branch => branch.MapMcp())` or `app.MapGroup("/mcp").MapMcp()`. Document the chosen path in the file's XML doc-comment AND the SUMMARY.

  This task ships ONLY the production code. Tests land in Task 2b. Stub tests in `HttpTransportTests.cs` / `OriginMiddlewareTests.cs` REMAIN skipped after this task — Task 2b unskips them.
  </action>
  <verify>
  `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo` succeeds. Three files exist under `src/FlaUI.Mcp/Mcp/Http/`. `Select-String src/FlaUI.Mcp/Mcp/Http/HttpTransport.cs -Pattern 'UseMiddleware<OriginValidationMiddleware>'` returns ≥1 hit.

  <automated>dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo</automated>
  </verify>
  <acceptance_criteria>
    - `Test-Path src/FlaUI.Mcp/Mcp/Http/HttpTransport.cs` returns True.
    - `Test-Path src/FlaUI.Mcp/Mcp/Http/OriginValidationMiddleware.cs` returns True.
    - `Select-String src/FlaUI.Mcp/Mcp/Http/HttpTransport.cs -Pattern 'public static async Task RunAsync'` returns ≥1 hit.
    - `Select-String src/FlaUI.Mcp/Mcp/Http/HttpTransport.cs -Pattern 'AddMcpServer'` returns ≥1 hit.
    - `Select-String src/FlaUI.Mcp/Mcp/Http/HttpTransport.cs -Pattern 'UseMiddleware<OriginValidationMiddleware>'` returns ≥1 hit.
    - `Select-String src/FlaUI.Mcp/Mcp/Http/HttpTransport.cs -Pattern 'ToolBridge\.CreateAll'` returns ≥1 hit.
    - `Select-String src/FlaUI.Mcp/Mcp/Http/OriginValidationMiddleware.cs -Pattern '403'` returns ≥1 hit.
    - `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo` exits 0.
  </acceptance_criteria>
  <done>HttpTransport.RunAsync compiles; OriginValidationMiddleware enforces allowlist with 403; SDK MapMcp wired with EnableLegacySse=false; all production code green; tests still skipped (Task 2b owns).</done>
</task>

<task type="auto">
  <name>Task 2b: Turn HTTP-01/03/04/05/07 stubs green</name>
  <files>
    <file>tests/FlaUI.Mcp.Tests/HttpTransportTests.cs</file>
    <file>tests/FlaUI.Mcp.Tests/OriginMiddlewareTests.cs</file>
    <file>tests/FlaUI.Mcp.Tests/ToolParityTests.cs</file>
  </files>
  <read_first>
    <file>src/FlaUI.Mcp/Mcp/Http/HttpTransport.cs</file>
    <file>src/FlaUI.Mcp/Mcp/Http/OriginValidationMiddleware.cs</file>
    <file>tests/FlaUI.Mcp.Tests/TestCategories.cs</file>
    <file>tests/FlaUI.Mcp.Tests/HttpTransportTests.cs</file>
  </read_first>
  <action>
  Unskip and implement the following tests. Recommended fixture: a small helper that boots `HttpTransport.RunAsync` on a background task with port `0` (let Kestrel pick a free port), then resolves the actual bound port via `IServer.Features.Get<IServerAddressesFeature>()`. If `HttpTransport` cannot expose its `IServer` from outside, refactor it to optionally yield the bound URL via a `TaskCompletionSource<string>` parameter — keep the public 6-arg signature but add an internal overload for tests.

  **`HttpTransportTests.MapsMcpEndpoint` (HTTP-01)** — POST `/mcp` body `{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}` with `Accept: application/json, text/event-stream` → 200 status + non-empty `Mcp-Session-Id` response header.

  **`HttpTransportTests.EndToEndToolCall` (HTTP-03)** — initialize → grab session-id → POST `tools/list` with the session-id → response includes 11 tools including `ListWindows` → POST `tools/call` invoking `ListWindows` (empty args) → response is a JSON-RPC result object (no error). Mark this test `[Trait("Category", TestCategories.Manual)]` ONLY if it requires real windows; `ListWindows` itself returns an empty list cleanly even with no windows, so this test should run automated. Keep it non-Manual.

  **`HttpTransportTests.SessionIdLifecycle` (HTTP-04)** — POST `tools/list` without `Mcp-Session-Id` → 400. POST with random GUID `Mcp-Session-Id` → 404.

  **`OriginMiddlewareTests.RejectsExternalOrigin` (HTTP-07)** — POST `/mcp` with `Origin: https://evil.example.com` → 403. With `Origin: http://127.0.0.1:3020` → not 403. With no Origin → not 403. With `Origin: null` literal → not 403.

  **`ToolParityTests.AllToolsCallableOverHttp` (HTTP-05) — REWRITTEN, no longer name-only**. This is the B5 fix. Convert the existing stubbed Theory into TWO real tests:

  1. `ToolsListReturnsAll11Tools` — POST `tools/list` after init → response result has exactly 11 tools, name set equals the canonical 11 (Launch, Snapshot, Click, Type, Fill, GetText, Screenshot, ListWindows, FocusWindow, CloseWindow, Batch).
  2. `NonUiToolsExecuteOverHttp` — for each non-UI tool name in `["ListWindows", "Batch"]`, POST `tools/call`:
     - `ListWindows` with empty args → JSON-RPC result, no error field, result contains a `windows` array (empty allowed).
     - `Batch` with `{"operations": []}` → JSON-RPC result, `results` array empty, no error.

  Plan 03 will add the SSE-side parity counterpart (`NonUiToolsExecuteOverSse`) so HTTP-05 is functionally proven on BOTH transports by end of Wave 2.

  UI-side-effect tools (Launch/Click/Type/Fill/GetText/Screenshot/FocusWindow/CloseWindow) remain documented as manual smoke in VALIDATION.md — they require real windows and stay covered by the live-client check in `/gsd:verify-work`.
  </action>
  <verify>
  Run full suite: `dotnet test tests/FlaUI.Mcp.Tests --nologo`. Must show all HttpTransportTests, OriginMiddlewareTests, and ToolParityTests passing (no skips for HTTP-01/03/04/05/07). Only `Wave 1: HTTP-02` skip remains (Plan 03 owns it).

  <automated>dotnet test tests/FlaUI.Mcp.Tests --nologo</automated>
  </verify>
  <acceptance_criteria>
    - `Select-String tests/FlaUI.Mcp.Tests/HttpTransportTests.cs -Pattern 'Skip\s*='` returns 0 hits.
    - `Select-String tests/FlaUI.Mcp.Tests/OriginMiddlewareTests.cs -Pattern 'Skip\s*='` returns 0 hits.
    - `Select-String tests/FlaUI.Mcp.Tests/ToolParityTests.cs -Pattern 'Skip\s*='` returns 0 hits.
    - `Select-String tests/FlaUI.Mcp.Tests/ToolParityTests.cs -Pattern 'NonUiToolsExecuteOverHttp'` returns ≥1 hit.
    - `Select-String tests/FlaUI.Mcp.Tests/ToolParityTests.cs -Pattern 'tools/call'` returns ≥1 hit (functional invocation, not name-only).
    - `dotnet test tests/FlaUI.Mcp.Tests --filter "FullyQualifiedName~HttpTransportTests|FullyQualifiedName~OriginMiddlewareTests|FullyQualifiedName~ToolParityTests" --nologo` exits 0.
  </acceptance_criteria>
  <done>All five HTTP-* tests previously skipped now pass; HTTP-05 functionally invokes non-UI tools (not just enumerates names); only the Plan-03-owned HTTP-02 stub remains skipped.</done>
</task>

</tasks>

<verification>

1. Full solution build: `dotnet build FlaUI-MCP.sln -c Debug --nologo` is green.
2. Test suite: `dotnet test tests/FlaUI.Mcp.Tests --nologo` — only `Wave 1: HTTP-02` skip remains (Plan 03 owns it).
3. Three new files exist under `src/FlaUI.Mcp/Mcp/Http/`.
4. Package `ModelContextProtocol.AspNetCore` 1.2.0 in csproj.
5. HTTP-05 parity test functionally invokes ListWindows + empty Batch over /mcp.

</verification>

<success_criteria>

- [ ] POST /mcp initialize → returns Mcp-Session-Id (HTTP-01, HTTP-04).
- [ ] tools/list returns all 11 FlaUI tools (HTTP-05).
- [ ] tools/call functionally invokes ListWindows + empty Batch over /mcp (HTTP-05 functional, not name-only).
- [ ] tools/call for ListWindows succeeds round-trip (HTTP-03).
- [ ] External Origin rejected 403; localhost/127.0.0.1/null/absent allowed (HTTP-07).
- [ ] Existing ITool registry untouched — bridge adapts at runtime.
- [ ] EnableLegacySse=false guarantees no co-mounted /sse on the http branch (D-03).

</success_criteria>

<output>
SUMMARY documents: actual `MapMcp` overload used (with-string-pattern vs fallback), exact `McpServerTool.Create` signature observed, package version pulled in, list of 11 tools verified in tools/list, the Origin middleware behavior matrix, the non-UI parity test names, and any deviations forced by the SDK API surface.
</output>
