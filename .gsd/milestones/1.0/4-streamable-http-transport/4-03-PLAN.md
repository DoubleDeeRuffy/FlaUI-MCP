---
phase: 4-streamable-http-transport
plan: 03
type: execute
wave: 2
depends_on: [4-01, 4-02]
files_modified:
  - src/FlaUI.Mcp/Mcp/SseTransport.cs
  - tests/FlaUI.Mcp.Tests/SseTransportTests.cs
  - tests/FlaUI.Mcp.Tests/ToolParityTests.cs
autonomous: true
requirements: [HTTP-02, HTTP-06]
must_haves:
  truths:
    - Legacy `--transport sse` still mounts `/sse` (GET) and `/messages` (POST) as before.
    - The legacy SSE Kestrel host now binds to `127.0.0.1` by default (was previously default-Kestrel = all interfaces).
    - The legacy SSE host accepts a `bindAddress` parameter so `--bind` can widen it (D-06 parity).
    - The legacy SSE host applies the same Origin allowlist as the new HTTP transport (D-06 uniformity).
    - "ToolParityTests adds an SSE-side functional invocation of ListWindows + empty Batch (HTTP-05 cross-transport parity)."
  artifacts:
    - path: src/FlaUI.Mcp/Mcp/SseTransport.cs
      provides: SSE transport host with bind+Origin parity to HTTP transport
      contains: "bindAddress"
    - path: tests/FlaUI.Mcp.Tests/SseTransportTests.cs
      provides: HTTP-02 regression + SSE-side Origin parity test
      min_lines: 30
    - path: tests/FlaUI.Mcp.Tests/ToolParityTests.cs
      provides: SSE-side non-UI tool invocation (HTTP-05 parity across both transports)
      contains: "NonUiToolsExecuteOverSse"
  key_links:
    - from: src/FlaUI.Mcp/Mcp/SseTransport.cs
      to: src/FlaUI.Mcp/Mcp/Http/OriginValidationMiddleware.cs
      via: app.UseMiddleware (shared with HttpTransport)
      pattern: "UseMiddleware<.*OriginValidationMiddleware>"
    - from: tests/FlaUI.Mcp.Tests/SseTransportTests.cs
      to: src/FlaUI.Mcp/Mcp/SseTransport.cs
      via: 3-arg constructor (server, bindAddress, port)
      pattern: "new SseTransport"
---

<objective>
Wave 2 — bring the legacy hand-rolled SSE transport into D-06 parity with the new HTTP transport: default bind to `127.0.0.1`, accept a `bindAddress` parameter, and enforce the same Origin allowlist via the middleware Plan 02 produced. Also extend HTTP-05 parity testing to functionally invoke non-UI tools over the SSE transport so the requirement is proven on BOTH transports.

Purpose: D-06 explicitly applies the bind+Origin policy to BOTH `--transport http` AND `--transport sse`. Without this plan, an existing SSE deployment would remain a DNS-rebinding target. The legacy SSE path is hand-rolled, so we cannot reuse the SDK middleware directly — but we CAN reuse the `OriginValidationMiddleware` class produced by Plan 02 (it depends only on `Microsoft.AspNetCore.Http`). HTTP-02 regression test stays here so the SSE branch is provably still working. This plan now lives in Wave 2 (depends on Plan 02 having shipped the middleware) — formerly Wave 1 with a hidden dependency.

Output: Updated `SseTransport` class with new constructor and Origin enforcement; HTTP-02 regression test green; SSE-side HTTP-05 functional parity test green; ready for Plan 04 (Wave 3) to call the new ctor with the parsed `--bind` address.
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
@src/FlaUI.Mcp/Mcp/SseTransport.cs
@src/FlaUI.Mcp/Mcp/Http/OriginValidationMiddleware.cs

<interfaces>
Existing constructor surface (read via LSP `find_symbol` on `SseTransport` before editing): `public SseTransport(McpServer server, int port)` and `public Task RunAsync(CancellationToken ct)`. The class hosts a `WebApplication` with two endpoints — `MapGet("/sse", ...)` and `MapPost("/messages", ...)`.

New constructor surface required by Plan 04:

```csharp
public SseTransport(McpServer server, string bindAddress, int port)
```

Keep the old `(server, port)` ctor as a thin wrapper that calls the new one with `bindAddress = "127.0.0.1"` so any internal call site or test that uses the 2-arg ctor still compiles (Plan 04 migrates Program.cs to the 3-arg call).

Shared middleware (Plan 02 output): `FlaUI.Mcp.Mcp.Http.OriginValidationMiddleware` — already compiled and available because this plan depends on 4-02.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: SseTransport bind default + Origin parity</name>
  <files>
    <file>src/FlaUI.Mcp/Mcp/SseTransport.cs</file>
  </files>
  <read_first>
    <file>src/FlaUI.Mcp/Mcp/SseTransport.cs</file>
    <file>src/FlaUI.Mcp/Mcp/Http/OriginValidationMiddleware.cs</file>
  </read_first>
  <action>
  Read `src/FlaUI.Mcp/Mcp/SseTransport.cs` end-to-end. Then:

  1. Add a new constructor `public SseTransport(McpServer server, string bindAddress, int port)` that stores `bindAddress` in a private field. Keep the old `public SseTransport(McpServer server, int port) : this(server, "127.0.0.1", port) {}` as a backward-compat shim.
  2. In `RunAsync`, change the Kestrel URL configuration to `app.Urls.Add($"http://{_bindAddress}:{_port}")` (or equivalent `WebHost.UseUrls` matching existing style). Remove any `+:` wildcard binding.
  3. Apply the Origin allowlist via `app.UseMiddleware<FlaUI.Mcp.Mcp.Http.OriginValidationMiddleware>();` placed BEFORE `MapGet("/sse"...)` and `MapPost("/messages"...)`. Add `using FlaUI.Mcp.Mcp.Http;` at the top.
  4. Add an `Info`-level NLog entry on startup logging the actual bind URL: `logger.Info("SSE transport listening on http://{Bind}:{Port}", _bindAddress, _port);` matching the existing logger pattern.

  Do NOT rewrite `Mcp/Protocol.cs` or `Mcp/McpServer.cs` — they remain hand-rolled per D-03.
  </action>
  <verify>
  `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo` succeeds.

  <automated>dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo</automated>
  </verify>
  <acceptance_criteria>
    - `Select-String src/FlaUI.Mcp/Mcp/SseTransport.cs -Pattern 'string bindAddress'` returns ≥1 hit.
    - `Select-String src/FlaUI.Mcp/Mcp/SseTransport.cs -Pattern 'UseMiddleware<.*OriginValidationMiddleware>'` returns ≥1 hit.
    - `Select-String src/FlaUI.Mcp/Mcp/SseTransport.cs -Pattern 'using FlaUI\.Mcp\.Mcp\.Http'` returns ≥1 hit.
    - `Select-String src/FlaUI.Mcp/Mcp/SseTransport.cs -Pattern '\+:'` returns 0 hits (no wildcard binds).
    - `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo` exits 0.
  </acceptance_criteria>
  <done>SseTransport accepts `bindAddress` parameter, defaults to 127.0.0.1, applies Origin allowlist via shared middleware; backward-compat 2-arg ctor preserved; build green.</done>
</task>

<task type="auto">
  <name>Task 2: HTTP-02 regression test + SSE-side HTTP-05 parity</name>
  <files>
    <file>tests/FlaUI.Mcp.Tests/SseTransportTests.cs</file>
    <file>tests/FlaUI.Mcp.Tests/ToolParityTests.cs</file>
  </files>
  <read_first>
    <file>src/FlaUI.Mcp/Mcp/SseTransport.cs</file>
    <file>tests/FlaUI.Mcp.Tests/SseTransportTests.cs</file>
    <file>tests/FlaUI.Mcp.Tests/ToolParityTests.cs</file>
  </read_first>
  <action>
  Unskip and implement `SseTransportTests.LegacyEndpointsRespond` (HTTP-02). Test recipe:

  - Construct `SseTransport(new McpServer(toolRegistry), "127.0.0.1", 0)` with port 0 to let Kestrel pick a free port.
  - Run on a background task with a CTS.
  - Resolve the actual bound port via `IServer.Features` pattern — if `SseTransport` doesn't expose its server, add an internal overload that yields the bound URL via `TaskCompletionSource<string>`, mirroring the test hook used in Plan 02 Task 2b.
  - Assert `GET /sse` with `Accept: text/event-stream` returns 200 (do not require message body — just connection establishment).
  - Assert `POST /messages` with a minimal JSON-RPC `{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}` returns 200 or 202.
  - Assert request with `Origin: https://evil.example.com` returns 403 — SSE-side parity check for HTTP-07 (D-06 uniformity). Do NOT name this test for HTTP-07; the Origin parity assertion lives inside `LegacyEndpointsRespond` as a sub-step.

  Then add a NEW test method to `tests/FlaUI.Mcp.Tests/ToolParityTests.cs`:

  **`NonUiToolsExecuteOverSse` (HTTP-05 parity, SSE side)** — Boot SseTransport on port 0. Initialize handshake over `/messages`. Then for each non-UI tool in `["ListWindows", "Batch"]`, POST a `tools/call` to `/messages` and assert a JSON-RPC result with no error field:
  - `ListWindows` with empty args → result contains `windows` array (empty allowed).
  - `Batch` with `{"operations": []}` → result `results` array empty.

  Together with Plan 02's `NonUiToolsExecuteOverHttp`, HTTP-05 is now functionally proven on BOTH transports (B5 fix — full requirement coverage, not name-only).

  Note on test categorization: NONE of these SSE-side tests carry `[Trait("Category", TestCategories.Manual)]` because they exercise non-UI tools only. Manual UI smoke is owned by `/gsd:verify-work`.
  </action>
  <verify>
  `dotnet test tests/FlaUI.Mcp.Tests --filter "FullyQualifiedName~SseTransportTests|FullyQualifiedName~ToolParityTests" --nologo` — must show all SSE tests passing, both `NonUiToolsExecuteOverHttp` and `NonUiToolsExecuteOverSse` passing.

  <automated>dotnet test tests/FlaUI.Mcp.Tests --nologo</automated>
  </verify>
  <acceptance_criteria>
    - `Select-String tests/FlaUI.Mcp.Tests/SseTransportTests.cs -Pattern 'Skip\s*='` returns 0 hits.
    - `Select-String tests/FlaUI.Mcp.Tests/SseTransportTests.cs -Pattern 'LegacyEndpointsRespond'` returns ≥1 hit.
    - `Select-String tests/FlaUI.Mcp.Tests/SseTransportTests.cs -Pattern 'evil\.example\.com'` returns ≥1 hit (Origin parity).
    - `Select-String tests/FlaUI.Mcp.Tests/ToolParityTests.cs -Pattern 'NonUiToolsExecuteOverSse'` returns ≥1 hit.
    - `dotnet test tests/FlaUI.Mcp.Tests --nologo` exits 0 with zero remaining HTTP-* skips.
  </acceptance_criteria>
  <done>HTTP-02 regression test green; SSE-side Origin allowlist proven; HTTP-05 functionally proven on BOTH transports (http and sse).</done>
</task>

</tasks>

<verification>

1. `dotnet build FlaUI-MCP.sln -c Debug --nologo` is green.
2. `dotnet test tests/FlaUI.Mcp.Tests --nologo` — all tests pass, no `Skip = "Wave 1` strings remain anywhere.
3. SseTransport.cs no longer wildcard-binds; defaults to loopback.
4. HTTP-05 parity proven on both transports.

</verification>

<success_criteria>

- [ ] Legacy SSE endpoints (`/sse`, `/messages`) still respond on the new bind address.
- [ ] Default bind address is `127.0.0.1` for SSE just like HTTP (D-06 parity).
- [ ] Origin allowlist enforced uniformly across both transports.
- [ ] Tests prove HTTP-02 (legacy parity) and the SSE-side branch of HTTP-07 (Origin parity).
- [ ] HTTP-05 functionally proven on BOTH transports via NonUiToolsExecuteOverHttp + NonUiToolsExecuteOverSse.

</success_criteria>

<output>
SUMMARY documents: existing SseTransport binding pattern observed, exact constructor signature change, whether the old 2-arg ctor was kept as shim or removed, the test fixture pattern used to drive a real Kestrel port, and the cross-transport HTTP-05 parity coverage.
</output>
