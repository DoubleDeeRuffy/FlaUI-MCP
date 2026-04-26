---
phase: 4-streamable-http-transport
plan: 01
type: execute
wave: 0
depends_on: []
files_modified:
  - tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj
  - tests/FlaUI.Mcp.Tests/CliParserTests.cs
  - tests/FlaUI.Mcp.Tests/HttpTransportTests.cs
  - tests/FlaUI.Mcp.Tests/SseTransportTests.cs
  - tests/FlaUI.Mcp.Tests/OriginMiddlewareTests.cs
  - tests/FlaUI.Mcp.Tests/ToolParityTests.cs
  - tests/FlaUI.Mcp.Tests/TestCategories.cs
  - src/FlaUI.Mcp/CliOptions.cs
  - src/FlaUI.Mcp/Program.cs
  - FlaUI-MCP.sln
  - .gsd/milestones/1.0/REQUIREMENTS.md
autonomous: true
requirements: [HTTP-08]
must_haves:
  truths:
    - A test project exists and `dotnet test tests/FlaUI.Mcp.Tests` runs (even if tests are pending stubs).
    - CLI parsing is extracted into a static, testable `CliOptions.Parse(string[])` method consumed by Program.cs.
    - The test project includes failing/skipped stubs for every HTTP-* requirement so Wave 1 has a place to land green tests.
    - REQUIREMENTS.md declares HTTP-01..08 (status pending) BEFORE any Wave 1 plan claims them.
    - CLI parser tests cover HTTP-08 default-transport-is-http behavior with no Kestrel involvement.
    - "Test category convention is defined: `TestCategories.Manual` constant; manual-only live-client tests carry `[Trait(\"Category\", TestCategories.Manual)]`."
  artifacts:
    - path: tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj
      provides: xunit test project wired into solution
      min_lines: 15
    - path: tests/FlaUI.Mcp.Tests/CliParserTests.cs
      provides: 7 passing CLI parser tests including HTTP-08 default
      min_lines: 30
    - path: tests/FlaUI.Mcp.Tests/HttpTransportTests.cs
      provides: skipped stubs for HTTP-01/03/04
    - path: tests/FlaUI.Mcp.Tests/SseTransportTests.cs
      provides: skipped stub for HTTP-02
    - path: tests/FlaUI.Mcp.Tests/OriginMiddlewareTests.cs
      provides: skipped stub for HTTP-07
    - path: tests/FlaUI.Mcp.Tests/ToolParityTests.cs
      provides: skipped stub for HTTP-05
    - path: tests/FlaUI.Mcp.Tests/TestCategories.cs
      provides: TestCategories.Manual constant
    - path: src/FlaUI.Mcp/CliOptions.cs
      provides: testable CliOptions record with Parse and Default
    - path: .gsd/milestones/1.0/REQUIREMENTS.md
      provides: HTTP-01..08 entries declared (status pending) for Wave 1 to claim
      contains: "HTTP-01"
  key_links:
    - from: src/FlaUI.Mcp/Program.cs
      to: src/FlaUI.Mcp/CliOptions.cs
      via: CliOptions.Parse(args) call
      pattern: "CliOptions\\.Parse"
    - from: tests/FlaUI.Mcp.Tests/CliParserTests.cs
      to: src/FlaUI.Mcp/CliOptions.cs
      via: project reference + Default/Parse assertions
      pattern: "CliOptions\\.(Default|Parse)"
    - from: FlaUI-MCP.sln
      to: tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj
      via: dotnet sln add
      pattern: "FlaUI\\.Mcp\\.Tests"
---

<objective>
Wave 0 — establish the validation infrastructure required by all later HTTP-* tasks. No HTTP transport runtime code lands here. Three outcomes: (1) a runnable xunit test project wired into the solution with failing/skipped stubs for every HTTP-* requirement (so Wave 1 implementers have a target); (2) extract CLI parsing into a static, testable `CliOptions` class so the default-transport flip and `--bind` parsing can be unit-tested without spinning up Kestrel; (3) declare HTTP-01..08 in `REQUIREMENTS.md` (pending status) so Wave 1 plans can legitimately reference those IDs in their `requirements:` frontmatter.

Purpose: Phase 4 RESEARCH.md identifies that the repo has zero test infrastructure. Per VALIDATION.md, Wave 0 MUST install xunit + Microsoft.AspNetCore.Mvc.Testing and create test stubs before any HTTP-* test can be written. CLI extraction is also a Wave 0 prerequisite for HTTP-08 (default transport flip is testable only if parsing is testable). Declaring the requirement IDs here prevents Wave 1 plans from referencing nonexistent IDs.

Output: New test project `tests/FlaUI.Mcp.Tests/`, new `src/FlaUI.Mcp/CliOptions.cs`, modified `Program.cs` (delegates parsing only), updated `FlaUI-MCP.sln`, and an updated `REQUIREMENTS.md`. After this plan, `dotnet build` and `dotnet test FlaUI-MCP.sln` succeed.
</objective>

<execution_context>
@$HOME/.claude-account2/get-shit-done/workflows/execute-plan.md
@$HOME/.claude-account2/get-shit-done/templates/summary.md
</execution_context>

<context>
@.gsd/PROJECT.md
@.gsd/ROADMAP.md
@.gsd/STATE.json
@.gsd/milestones/1.0/REQUIREMENTS.md
@.gsd/milestones/1.0/4-streamable-http-transport/4-CONTEXT.md
@.gsd/milestones/1.0/4-streamable-http-transport/4-RESEARCH.md
@.gsd/milestones/1.0/4-streamable-http-transport/4-VALIDATION.md
@src/FlaUI.Mcp/Program.cs
@src/FlaUI.Mcp/FlaUI.Mcp.csproj

<interfaces>
Existing CLI parsing in `src/FlaUI.Mcp/Program.cs` lines 15-76 — top-level argument loop sets local vars: `silent, debug, install, uninstall, console, task, removeTask, transport (default "sse"), port (default 3020)`. Help block at lines 57-74 hard-codes the help text and ALSO hard-codes a stale port-default string `(default: 8080)` at line 71 that disagrees with the actual default `3020` at line 24 — DO NOT fix that here (Plan 04 owns the help-text rewrite); but do preserve the inconsistency exactly when copying the parser, because Plan 04 will rewrite the help block top-to-bottom.

Target surface for `CliOptions`:

```csharp
namespace FlaUI.Mcp;
public sealed record CliOptions(
    bool Silent, bool Debug, bool Install, bool Uninstall, bool Console,
    bool Task, bool RemoveTask, bool Help,
    string Transport, int Port,
    string BindAddress)  // NEW field — default "127.0.0.1"; honored by Plan 04
{
    public static CliOptions Parse(string[] args);
    public static CliOptions Default { get; } // Transport="http", Port=3020, BindAddress="127.0.0.1"
}
```

Note the default Transport in `CliOptions.Default` is already `"http"` per D-02 — Plan 04 wires the rest. Tests in this plan assert `CliOptions.Default.Transport == "http"` (HTTP-08).
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Extract CLI parsing into CliOptions and add --bind</name>
  <files>
    <file>src/FlaUI.Mcp/CliOptions.cs</file>
    <file>src/FlaUI.Mcp/Program.cs</file>
  </files>
  <read_first>
    <file>src/FlaUI.Mcp/Program.cs</file>
    <file>.gsd/milestones/1.0/4-streamable-http-transport/4-CONTEXT.md</file>
    <file>.gsd/milestones/1.0/4-streamable-http-transport/4-RESEARCH.md</file>
  </read_first>
  <action>
  Create `src/FlaUI.Mcp/CliOptions.cs` with namespace `FlaUI.Mcp` exposing a public sealed record `CliOptions` with all fields listed in the interfaces block above. Implement a static `Parse(string[] args)` method that mirrors the existing switch in `Program.cs` lines 26-76 EXACTLY for the existing flags. Add new flag handling: `case "--bind" when i + 1 < args.Length: bindAddress = args[++i]; break;` (no validation here — accept any non-empty string; Plan 04 wires actual binding). Set defaults: `Transport="http"`, `Port=3020`, `BindAddress="127.0.0.1"`, all booleans false. Add `public static CliOptions Default => new CliOptions(false,false,false,false,false,false,false,false,"http",3020,"127.0.0.1");`. Do NOT include the `--help` print block in `CliOptions.Parse` — set `Help=true` and let Program.cs handle printing (Plan 04 owns the help text rewrite).

Then modify `src/FlaUI.Mcp/Program.cs` to replace lines 15-76 with: `var opts = FlaUI.Mcp.CliOptions.Parse(args); var silent = opts.Silent; var debug = opts.Debug; var install = opts.Install; var uninstall = opts.Uninstall; var console = opts.Console; var task = opts.Task; var removeTask = opts.RemoveTask; var transport = opts.Transport; var port = opts.Port; if (opts.Help) { /* keep existing help Console.WriteLine block here verbatim */ Environment.Exit(0); }`. Preserve the rest of Program.cs UNCHANGED — Plan 04 will rewrite the `transport == "sse"` branch and the help text.

IMPORTANT: this task does NOT change runtime behavior other than the new default transport ("http" instead of "sse"). Plan 04 adds the actual `"http"` branch handler — until then, running with no `--transport` will fall into the stdio fallback `else` branch in Program.cs line 272. Document this transient state in the task's commit message.
  </action>
  <verify>
  Run `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug` — must succeed. `Select-String -Path src/FlaUI.Mcp/Program.cs -Pattern 'CliOptions\.Parse'` returns 1 hit. `Select-String -Path src/FlaUI.Mcp/CliOptions.cs -Pattern 'BindAddress'` returns ≥ 2 hits.

  <automated>dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo</automated>
  </verify>
  <acceptance_criteria>
    - `Test-Path src/FlaUI.Mcp/CliOptions.cs` returns True.
    - `Select-String src/FlaUI.Mcp/CliOptions.cs -Pattern 'public static CliOptions Default'` returns ≥1 hit.
    - `Select-String src/FlaUI.Mcp/CliOptions.cs -Pattern 'public static CliOptions Parse'` returns ≥1 hit.
    - `Select-String src/FlaUI.Mcp/CliOptions.cs -Pattern 'BindAddress'` returns ≥2 hits.
    - `Select-String src/FlaUI.Mcp/Program.cs -Pattern 'CliOptions\.Parse'` returns ≥1 hit.
    - `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo` exits 0.
  </acceptance_criteria>
  <done>`CliOptions.cs` exists with `Parse` and `Default` members; `Program.cs` calls `CliOptions.Parse(args)`; project builds cleanly; `--bind` is parsed (but not yet honored at runtime).</done>
</task>

<task type="auto">
  <name>Task 2: Create xunit test project, stub all HTTP-* tests, define test category convention</name>
  <files>
    <file>tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj</file>
    <file>tests/FlaUI.Mcp.Tests/TestCategories.cs</file>
    <file>tests/FlaUI.Mcp.Tests/CliParserTests.cs</file>
    <file>tests/FlaUI.Mcp.Tests/HttpTransportTests.cs</file>
    <file>tests/FlaUI.Mcp.Tests/SseTransportTests.cs</file>
    <file>tests/FlaUI.Mcp.Tests/OriginMiddlewareTests.cs</file>
    <file>tests/FlaUI.Mcp.Tests/ToolParityTests.cs</file>
    <file>FlaUI-MCP.sln</file>
  </files>
  <read_first>
    <file>src/FlaUI.Mcp/CliOptions.cs</file>
    <file>src/FlaUI.Mcp/FlaUI.Mcp.csproj</file>
    <file>.gsd/milestones/1.0/4-streamable-http-transport/4-VALIDATION.md</file>
  </read_first>
  <action>
  Create `tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj` with: SDK `Microsoft.NET.Sdk`, `<TargetFramework>net8.0-windows</TargetFramework>`, `<IsPackable>false</IsPackable>`, `<UseWindowsForms>true</UseWindowsForms>`, `<Nullable>enable</Nullable>`. PackageReferences (latest stable as of 2026-04): `Microsoft.NET.Test.Sdk` 17.x, `xunit` 2.x, `xunit.runner.visualstudio` 2.x, `Microsoft.AspNetCore.Mvc.Testing` 8.x, `coverlet.collector` 6.x. ProjectReference to `..\..\src\FlaUI.Mcp\FlaUI.Mcp.csproj`.

Create `tests/FlaUI.Mcp.Tests/TestCategories.cs`:

```csharp
namespace FlaUI.Mcp.Tests;
public static class TestCategories
{
    public const string Manual = "Manual";  // live-client tests requiring real Claude Code or real Windows windows
}
```

This establishes the test-trait convention. Wave 1+ tests that require manual smoke (HTTP-03 live client, UI-side-effect tools) MUST carry `[Trait("Category", TestCategories.Manual)]` so the default `dotnet test` run can exclude them via `--filter "Category!=Manual"`.

Create test files. Every test that depends on Wave 1 code uses `[Fact(Skip = "Wave 1: HTTP-XX")]`. CliParserTests has REAL passing tests since CliOptions exists in Task 1.

`CliParserTests.cs` — namespace `FlaUI.Mcp.Tests`. Real tests covering HTTP-08:

- `DefaultTransportIsHttp` — `Assert.Equal("http", CliOptions.Default.Transport);` (HTTP-08)
- `ParseEmptyArgsYieldsDefaultTransport` — `Assert.Equal("http", CliOptions.Parse(Array.Empty<string>()).Transport);` (HTTP-08)
- `ParseTransportSseOverridesDefault` — `Assert.Equal("sse", CliOptions.Parse(new[]{"--transport","sse"}).Transport);`
- `ParseTransportStdioOverridesDefault` — `Assert.Equal("stdio", CliOptions.Parse(new[]{"--transport","stdio"}).Transport);`
- `DefaultPortIs3020` — `Assert.Equal(3020, CliOptions.Default.Port);`
- `DefaultBindIsLoopback` — `Assert.Equal("127.0.0.1", CliOptions.Default.BindAddress);`
- `ParseBindFlagAcceptsAddress` — `Assert.Equal("0.0.0.0", CliOptions.Parse(new[]{"--bind","0.0.0.0"}).BindAddress);`
- `ParseBindFlagAcceptsLoopback` — `Assert.Equal("127.0.0.1", CliOptions.Parse(new[]{"--bind","127.0.0.1"}).BindAddress);`
- `ParsePortFlagYieldsCustomPort` — `Assert.Equal(4000, CliOptions.Parse(new[]{"--port","4000"}).Port);`

(Note: `--bind` accepts address-only per CONTEXT D-06 / D-Discretion; explicit port stays on `--port`. No address+port composite syntax in Phase 4.)

`HttpTransportTests.cs` — three skipped facts: `MapsMcpEndpoint` (HTTP-01), `EndToEndToolCall` (HTTP-03 — also `[Trait("Category", TestCategories.Manual)]` since live tool round-trip requires real UI), `SessionIdLifecycle` (HTTP-04). Each: `[Fact(Skip = "Wave 1: HTTP-0X — implemented in Plan 02")]`.

`SseTransportTests.cs` — one skipped fact: `LegacyEndpointsRespond` (HTTP-02), Skip = `"Wave 1: HTTP-02 — implemented in Plan 03"`.

`OriginMiddlewareTests.cs` — one skipped fact: `RejectsExternalOrigin` (HTTP-07), Skip = `"Wave 1: HTTP-07 — implemented in Plan 02"`.

`ToolParityTests.cs` — one skipped Theory: `AllToolsCallableOverHttp` (HTTP-05), Skip = `"Wave 1: HTTP-05 — implemented in Plan 02"`.

Finally, register the test project in `FlaUI-MCP.sln`: run `dotnet sln FlaUI-MCP.sln add tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj`. (If `FlaUI-MCP.sln` does not exist at repo root, search for `*.sln` first; if none, this task creates one via `dotnet new sln -n FlaUI-MCP` and then `dotnet sln add src/FlaUI.Mcp/FlaUI.Mcp.csproj` plus the test project.)
  </action>
  <verify>
  Run `dotnet test tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj --nologo` — must exit 0 with all CliParserTests passing and all other tests skipped. Run `dotnet test tests/FlaUI.Mcp.Tests --filter FullyQualifiedName~CliParserTests` — must show 9 passed.

  <automated>dotnet test tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj --nologo</automated>
  </verify>
  <acceptance_criteria>
    - `Test-Path tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj` returns True.
    - `Test-Path tests/FlaUI.Mcp.Tests/TestCategories.cs` returns True.
    - `Select-String tests/FlaUI.Mcp.Tests/TestCategories.cs -Pattern 'public const string Manual'` returns ≥1 hit.
    - `Select-String FlaUI-MCP.sln -Pattern 'FlaUI\.Mcp\.Tests'` returns ≥1 hit.
    - `Select-String tests/FlaUI.Mcp.Tests/CliParserTests.cs -Pattern 'DefaultTransportIsHttp'` returns ≥1 hit (HTTP-08 coverage).
    - `dotnet test tests/FlaUI.Mcp.Tests --filter FullyQualifiedName~CliParserTests --nologo` exits 0 with 9 passed.
    - `dotnet test tests/FlaUI.Mcp.Tests --nologo` exits 0 (skipped allowed).
  </acceptance_criteria>
  <done>Test project compiles, integrates with the solution, all 9 CLI parser tests pass (HTTP-08 covered by automated tests), all HTTP-* stub tests are present and skipped with `Wave 1: HTTP-XX` reason strings, TestCategories.Manual constant defined.</done>
</task>

<task type="auto">
  <name>Task 3: Declare HTTP-01..08 in REQUIREMENTS.md (pending) for Wave 1 to claim</name>
  <files>
    <file>.gsd/milestones/1.0/REQUIREMENTS.md</file>
  </files>
  <read_first>
    <file>.gsd/milestones/1.0/REQUIREMENTS.md</file>
    <file>.gsd/milestones/1.0/4-streamable-http-transport/4-CONTEXT.md</file>
  </read_first>
  <action>
  Edit `.gsd/milestones/1.0/REQUIREMENTS.md`:

1. Add a new section `### Streamable HTTP Transport` between `### Task Scheduler Startup` and `## v2 Requirements`. Populate with these 8 checkbox lines, ALL marked `[ ]` (pending — Wave 1 + Plan 04 will flip them):
   - `- [ ] **HTTP-01**: "--transport http" mounts Streamable HTTP on /mcp (POST/GET) via ModelContextProtocol.AspNetCore SDK`
   - `- [ ] **HTTP-02**: "--transport sse" continues to mount legacy /sse and /messages endpoints`
   - `- [ ] **HTTP-03**: Modern MCP client (Claude Code "type":"http") can initialize, list tools, and invoke a tool over /mcp`
   - `- [ ] **HTTP-04**: Mcp-Session-Id header per spec — auto-issued on initialize, 400 if absent on subsequent, 404 if expired`
   - `- [ ] **HTTP-05**: All 11 tools (Launch/Snapshot/Click/Type/Fill/GetText/Screenshot/ListWindows/FocusWindow/CloseWindow/Batch) callable on http and sse transports`
   - `- [ ] **HTTP-06**: Default Kestrel bind = 127.0.0.1; --bind <addr> CLI escape hatch; policy applies to both http and sse`
   - `- [ ] **HTTP-07**: Origin header rejected (HTTP 403) unless absent, "null", localhost, or 127.0.0.1`
   - `- [ ] **HTTP-08**: Default transport flipped from sse to http; --help text updated`

2. Update the `**Coverage:**` block to read `v1 requirements: 40 total` (was 32, +8). Mapped: 40. Unmapped: 0.

3. Append 8 rows to the Traceability table at the bottom, all `Pending`:

   ```
   | HTTP-01 | Phase 4 | Pending |
   | HTTP-02 | Phase 4 | Pending |
   | HTTP-03 | Phase 4 | Pending |
   | HTTP-04 | Phase 4 | Pending |
   | HTTP-05 | Phase 4 | Pending |
   | HTTP-06 | Phase 4 | Pending |
   | HTTP-07 | Phase 4 | Pending |
   | HTTP-08 | Phase 4 | Pending |
   ```

4. Update the `*Last updated:* ` footer to `2026-04-26 after Phase 4 Wave 0 declaration`.

Do NOT touch the existing LOG-/SVC-/TSK- entries — Phase 1/2/3 own them. Plan 04 (Wave 3) will flip these from Pending to Complete.
  </action>
  <verify>
  `Select-String -Path .gsd/milestones/1.0/REQUIREMENTS.md -Pattern '^- \[ \] \*\*HTTP-0[1-8]\*\*'` returns 8 hits. `Select-String -Path .gsd/milestones/1.0/REQUIREMENTS.md -Pattern '40 total'` returns 1 hit.

  <automated>powershell -Command "if ((Select-String -Path .gsd/milestones/1.0/REQUIREMENTS.md -Pattern 'HTTP-0[1-8]').Count -ge 16) { exit 0 } else { exit 1 }"</automated>
  </verify>
  <acceptance_criteria>
    - `Select-String .gsd/milestones/1.0/REQUIREMENTS.md -Pattern '\*\*HTTP-01\*\*'` returns ≥1 hit.
    - `Select-String .gsd/milestones/1.0/REQUIREMENTS.md -Pattern '\*\*HTTP-08\*\*'` returns ≥1 hit.
    - `Select-String .gsd/milestones/1.0/REQUIREMENTS.md -Pattern '40 total'` returns ≥1 hit.
    - `Select-String .gsd/milestones/1.0/REQUIREMENTS.md -Pattern 'HTTP-01 \| Phase 4 \| Pending'` returns ≥1 hit.
  </acceptance_criteria>
  <done>REQUIREMENTS.md declares HTTP-01..08 as pending Phase 4 items so Wave 1 plans (02, 03) can legitimately reference them in `requirements:` frontmatter.</done>
</task>

</tasks>

<verification>

1. `dotnet build FlaUI-MCP.sln -c Debug --nologo` — entire solution builds cleanly.
2. `dotnet test tests/FlaUI.Mcp.Tests --nologo` — exits 0; 9 CliParserTests pass; HTTP-* stubs skipped; zero failed.
3. `Select-String -Path src/FlaUI.Mcp/CliOptions.cs -Pattern 'BindAddress|public static CliOptions Parse|Default'` returns ≥ 3 hits.
4. `Select-String -Path FlaUI-MCP.sln -Pattern 'FlaUI\.Mcp\.Tests'` returns ≥ 1 hit.
5. REQUIREMENTS.md declares HTTP-01..08 (pending status) — verified by 16 Select-String hits.

</verification>

<success_criteria>

- [ ] Test project compiles and runs as part of the solution.
- [ ] CLI parser is extracted into a unit-testable static class with `--bind` recognized and default transport `"http"`.
- [ ] HTTP-08 covered by automated CLI parser tests (`DefaultTransportIsHttp`, `ParseEmptyArgsYieldsDefaultTransport`).
- [ ] Skipped test stubs exist for every HTTP-* requirement so Wave 1 plans can replace skips with passing tests.
- [ ] Test category convention (`TestCategories.Manual`) defined and documented.
- [ ] REQUIREMENTS.md declares HTTP-01..08 entries (pending) for downstream plans to claim.
- [ ] No HTTP transport runtime code introduced — Wave 1 owns that.
- [ ] Existing service/scheduler/stdio behavior unchanged.

</success_criteria>

<output>
After completion, the SUMMARY captures: package versions installed, the exact `CliOptions` record shape, list of stubbed tests with their Wave-1 owner plans, the solution-file change, REQUIREMENTS.md HTTP-01..08 declaration, and any pre-existing port/help-text inconsistencies surfaced (defer their fix to Plan 04).
</output>
