---
phase: 4-streamable-http-transport
plan: 04
type: execute
wave: 3
depends_on: [4-02, 4-03]
files_modified:
  - src/FlaUI.Mcp/Program.cs
  - .gsd/milestones/1.0/REQUIREMENTS.md
autonomous: true
requirements: [HTTP-06, HTTP-08]
must_haves:
  truths:
    - "Running `FlaUI.Mcp.exe` with no flags starts the Streamable HTTP transport on `http://127.0.0.1:3020/mcp` (default flip per D-02)."
    - "`--transport sse` continues to mount `/sse` + `/messages` (no behavior regression beyond the bind/Origin tightening from Plan 03)."
    - "`--transport stdio` is unchanged."
    - "`--bind <addr>` is honored: it widens or narrows the Kestrel listen address for both http and sse transports."
    - "`--help` text reflects: default transport `http`, available transport values `http|sse|stdio`, the `--bind` flag with default `127.0.0.1`, and the actual port default `3020` (fixes the pre-existing `8080` typo at line 71)."
    - Firewall rule creation now triggers for both `http` and `sse` transports.
    - NLog console target is enabled when transport is `http` OR `sse` (was previously only `sse`).
    - REQUIREMENTS.md HTTP-01..08 entries (declared pending in Wave 0) are now flipped to Complete with traceability.
    - "HTTP-08 default-transport-is-http coverage = automated CliParserTests in Plan 4-01 PLUS a `--help` text grep here in Plan 4-04."
  artifacts:
    - path: src/FlaUI.Mcp/Program.cs
      provides: integrated 3-way transport switch wiring HTTP/SSE/stdio with --bind
      contains: "HttpTransport.RunAsync"
    - path: .gsd/milestones/1.0/REQUIREMENTS.md
      provides: HTTP-01..08 flipped from Pending to Complete
      contains: "HTTP-01 \\| Phase 4 \\| Complete"
  key_links:
    - from: src/FlaUI.Mcp/Program.cs
      to: src/FlaUI.Mcp/Mcp/Http/HttpTransport.cs
      via: HttpTransport.RunAsync(...) invocation in http branch
      pattern: "HttpTransport\\.RunAsync"
    - from: src/FlaUI.Mcp/Program.cs
      to: src/FlaUI.Mcp/Mcp/SseTransport.cs
      via: 3-arg constructor with opts.BindAddress
      pattern: "new SseTransport\\(.*opts\\.BindAddress"
    - from: src/FlaUI.Mcp/Program.cs
      to: src/FlaUI.Mcp/CliOptions.cs
      via: opts.BindAddress flow into both transports
      pattern: "opts\\.BindAddress"
---

<objective>
Wave 3 — wire everything together in `Program.cs`: invoke `HttpTransport.RunAsync` for the new `http` branch, pass `--bind` into both transport constructors, flip the default transport from `sse` to `http`, fix the help text (including the pre-existing port-default typo), extend the firewall rule + NLog console target gates to include `http`, and flip the HTTP-* REQUIREMENTS.md entries from Pending (declared in Plan 4-01) to Complete.

Purpose: Plans 02 and 03 produced isolated, testable building blocks. This plan integrates them. After this plan completes the phase satisfies its full ROADMAP success criteria: a default `http`-mode binary that modern MCP clients can connect to over `/mcp`, with the legacy SSE path intact and uniformly secured.

Output: A single integration commit on `Program.cs`, REQUIREMENTS.md flipped to Complete, and the manual smoke checklist documented in the SUMMARY for the post-execute `/gsd:verify-work` step.

Note on HTTP-08 coverage: this plan does NOT carry the primary HTTP-08 automated test — that lives in Plan 4-01's `CliParserTests` (`DefaultTransportIsHttp`, `ParseEmptyArgsYieldsDefaultTransport`). This plan adds a complementary `--help` text grep to verify the user-facing announcement of the new default.
</objective>

<execution_context>
@$HOME/.claude-account2/get-shit-done/workflows/execute-plan.md
@$HOME/.claude-account2/get-shit-done/templates/summary.md
</execution_context>

<context>
@.gsd/PROJECT.md
@.gsd/ROADMAP.md
@.gsd/milestones/1.0/REQUIREMENTS.md
@.gsd/milestones/1.0/4-streamable-http-transport/4-CONTEXT.md
@.gsd/milestones/1.0/4-streamable-http-transport/4-RESEARCH.md
@.gsd/milestones/1.0/4-streamable-http-transport/4-VALIDATION.md
@src/FlaUI.Mcp/Program.cs
@src/FlaUI.Mcp/CliOptions.cs
@src/FlaUI.Mcp/Mcp/Http/HttpTransport.cs
@src/FlaUI.Mcp/Mcp/SseTransport.cs
@src/FlaUI.Mcp/Logging/LoggingConfig.cs

<interfaces>
Existing Program.cs structure (post Plan 01):

- Line ~15: `var opts = CliOptions.Parse(args);` then locals copied out of opts.
- Lines 96-101: `LogArchiver.CleanOldLogfiles(...)` then `LoggingConfig.ConfigureLogging(debug, logDirectory, enableConsoleTarget: transport == "sse");` — needs widening to `transport != "stdio"`.
- Lines 113-126: `if (transport == "sse") { firewall rule }` — needs widening to `transport == "sse" || transport == "http"`.
- Lines 237-275: shared services + tool registration + the transport switch — needs a new `http` branch BEFORE the sse one and the sse branch needs the new 3-arg ctor.
- Lines 57-74: `--help` Console.WriteLine block.

New transport switch shape:

```csharp
if (transport == "http")
    await FlaUI.Mcp.Mcp.Http.HttpTransport.RunAsync(
        sessionManager, elementRegistry, toolRegistry,
        opts.BindAddress, port, cts.Token);
else if (transport == "sse")
    await new SseTransport(server, opts.BindAddress, port).RunAsync(cts.Token);
else  // stdio
    await server.RunAsync(cts.Token);
```

</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Wire http branch, --bind, default flip, firewall + NLog gates, help text</name>
  <files>
    <file>src/FlaUI.Mcp/Program.cs</file>
  </files>
  <read_first>
    <file>src/FlaUI.Mcp/Program.cs</file>
    <file>src/FlaUI.Mcp/CliOptions.cs</file>
    <file>src/FlaUI.Mcp/Mcp/Http/HttpTransport.cs</file>
    <file>src/FlaUI.Mcp/Mcp/SseTransport.cs</file>
  </read_first>
  <action>
  Apply the following edits to `src/FlaUI.Mcp/Program.cs`:

  1. **Console-target gate (line ~101).** Replace `enableConsoleTarget: transport == "sse"` with `enableConsoleTarget: transport != "stdio"`. Activates console target for both `http` and `sse` while keeping stdio frame-clean (Pitfall 2).

  2. **Firewall gate (line ~115).** Replace `if (transport == "sse")` with `if (transport == "sse" || transport == "http")` so `--transport http` also gets the firewall rule (Pitfall 5).

  3. **Transport switch (lines ~267-275).** Replace the existing `if/else` with the three-way switch shown in the interfaces block above. Use `opts.BindAddress` for both `http` and `sse`. Pass `port` and `cts.Token`. Keep the `try/catch/finally` envelope unchanged.

  4. **Help text (lines ~57-74).** Rewrite the `--help` block:
     - `--transport <type>  Transport: http (default), sse, or stdio`
     - `--bind <addr>      Kestrel bind address (default: 127.0.0.1; use 0.0.0.0 for LAN)`
     - `--port <number>    Listen port (default: 3020)`  ← FIX the existing `(default: 8080)` typo at line 71.

     Keep the existing `--install / --uninstall / --task / --removetask / --silent / --debug / --console / --help` lines verbatim.

  5. **Startup banner (line ~105).** Update to: `logger.Info("FlaUI-MCP starting (transport={Transport}, bind={Bind}, port={Port}, debug={Debug})", transport, opts.BindAddress, port, debug);`.

  6. **Sanity guard.** If `transport` is none of `http|sse|stdio`, log an error and `Environment.Exit(2)`. Pre-existing code silently fell into the stdio else-branch — surface it now.

  Do NOT touch service install/uninstall paths, scheduled-task creation, the unhandled-exception handler, the SVC-08 service-stop block, or any tool registration code.
  </action>
  <verify>
  1. `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo` — clean build.
  2. `dotnet run --project src/FlaUI.Mcp -- --help` — output contains `Transport: http (default), sse, or stdio`, `default: 3020`, `--bind`.
  3. Manual smoke: `dotnet run --project src/FlaUI.Mcp` (no args) → `netstat -ano | findstr :3020` shows process listening on `127.0.0.1:3020`.
  4. Manual smoke: `--transport sse` → `/sse` still responds.
  5. `dotnet test tests/FlaUI.Mcp.Tests --nologo` — full suite green.

  <automated>dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo</automated>
  </verify>
  <acceptance_criteria>
    - `Select-String src/FlaUI.Mcp/Program.cs -Pattern 'HttpTransport\.RunAsync'` returns ≥1 hit.
    - `Select-String src/FlaUI.Mcp/Program.cs -Pattern 'opts\.BindAddress'` returns ≥2 hits (http + sse branches).
    - `Select-String src/FlaUI.Mcp/Program.cs -Pattern 'enableConsoleTarget: transport != "stdio"'` returns ≥1 hit.
    - `Select-String src/FlaUI.Mcp/Program.cs -Pattern 'transport == "sse" \|\| transport == "http"'` returns ≥1 hit.
    - `Select-String src/FlaUI.Mcp/Program.cs -Pattern 'http \(default\)'` returns ≥1 hit (help text).
    - `Select-String src/FlaUI.Mcp/Program.cs -Pattern 'default: 3020'` returns ≥1 hit (help text typo fix).
    - `Select-String src/FlaUI.Mcp/Program.cs -Pattern 'default: 8080'` returns 0 hits (typo removed).
    - `Select-String src/FlaUI.Mcp/Program.cs -Pattern '--bind'` returns ≥1 hit (help text).
    - `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo` exits 0.
    - `dotnet test tests/FlaUI.Mcp.Tests --nologo` exits 0.
  </acceptance_criteria>
  <done>Program.cs wires http transport, default-flips to http, accepts --bind, fixes help text typo, extends firewall + NLog gates; full test suite green.</done>
</task>

<task type="auto">
  <name>Task 2: Flip REQUIREMENTS.md HTTP-01..08 from Pending to Complete</name>
  <files>
    <file>.gsd/milestones/1.0/REQUIREMENTS.md</file>
  </files>
  <read_first>
    <file>.gsd/milestones/1.0/REQUIREMENTS.md</file>
  </read_first>
  <action>
  Edit `.gsd/milestones/1.0/REQUIREMENTS.md` (the HTTP-01..08 entries were declared as Pending in Plan 4-01 Wave 0):

  1. In the `### Streamable HTTP Transport` section, replace each `- [ ]` with `- [x]` for HTTP-01..08.

  2. In the Traceability table, replace each `| HTTP-0X | Phase 4 | Pending |` row with `| HTTP-0X | Phase 4 | Complete |` for HTTP-01..08.

  3. Update the `*Last updated:* ` footer to `2026-04-26 after Phase 4 wiring`.

  Do NOT touch the existing LOG-/SVC-/TSK- entries, and do NOT modify the `**Coverage:**` total — Plan 4-01 already set it to `40 total`.
  </action>
  <verify>
  `Select-String -Path .gsd/milestones/1.0/REQUIREMENTS.md -Pattern '^- \[x\] \*\*HTTP-0[1-8]\*\*'` returns 8 hits. `Select-String -Path .gsd/milestones/1.0/REQUIREMENTS.md -Pattern '^\| HTTP-0[1-8] \| Phase 4 \| Complete \|'` returns 8 hits. `Select-String -Path .gsd/milestones/1.0/REQUIREMENTS.md -Pattern 'HTTP-0[1-8] \| Phase 4 \| Pending'` returns 0 hits.

  <automated>powershell -Command "$x = (Select-String -Path .gsd/milestones/1.0/REQUIREMENTS.md -Pattern '^- \[x\] \*\*HTTP-0[1-8]\*\*').Count; $p = (Select-String -Path .gsd/milestones/1.0/REQUIREMENTS.md -Pattern 'HTTP-0[1-8] \| Phase 4 \| Pending').Count; if ($x -eq 8 -and $p -eq 0) { exit 0 } else { exit 1 }"</automated>
  </verify>
  <acceptance_criteria>
    - `Select-String .gsd/milestones/1.0/REQUIREMENTS.md -Pattern '^- \[x\] \*\*HTTP-01\*\*'` returns ≥1 hit.
    - `Select-String .gsd/milestones/1.0/REQUIREMENTS.md -Pattern '^- \[x\] \*\*HTTP-08\*\*'` returns ≥1 hit.
    - `Select-String .gsd/milestones/1.0/REQUIREMENTS.md -Pattern 'HTTP-0[1-8] \| Phase 4 \| Pending'` returns 0 hits.
    - `Select-String .gsd/milestones/1.0/REQUIREMENTS.md -Pattern 'HTTP-01 \| Phase 4 \| Complete'` returns ≥1 hit.
    - `Select-String .gsd/milestones/1.0/REQUIREMENTS.md -Pattern 'HTTP-08 \| Phase 4 \| Complete'` returns ≥1 hit.
  </acceptance_criteria>
  <done>REQUIREMENTS.md HTTP-01..08 flipped to Complete; no Pending HTTP rows remain.</done>
</task>

</tasks>

<verification>

1. `dotnet build FlaUI-MCP.sln -c Debug --nologo` is green.
2. `dotnet test tests/FlaUI.Mcp.Tests --nologo` — full suite green, zero skipped HTTP-* tests, zero failed.
3. `dotnet run --project src/FlaUI.Mcp -- --help` shows new help text (http default, --bind, port 3020).
4. `dotnet run --project src/FlaUI.Mcp` (no args) starts on http://127.0.0.1:3020/mcp; verified by `netstat -ano | findstr :3020`.
5. `dotnet run --project src/FlaUI.Mcp -- --transport sse` starts legacy SSE on http://127.0.0.1:3020/sse.
6. `dotnet run --project src/FlaUI.Mcp -- --bind 0.0.0.0` (interactive smoke only) widens listen address.
7. REQUIREMENTS.md grep checks all pass (Task 2 acceptance).
8. Manual smoke per VALIDATION.md captured in SUMMARY for /gsd:verify-work.

</verification>

<success_criteria>

- [ ] Default transport is `http` (HTTP-08, dual-coverage: CliParserTests + --help grep).
- [ ] `--bind` honored uniformly across http and sse (HTTP-06).
- [ ] Legacy SSE `--transport sse` regression-clean (HTTP-02).
- [ ] Stdio transport unchanged.
- [ ] Help text reflects new defaults; pre-existing port-typo fixed.
- [ ] Firewall rule + NLog console target now fire for `http` as well as `sse`.
- [ ] REQUIREMENTS.md HTTP-01..08 all Complete; zero Pending HTTP rows.
- [ ] Full test suite green; phase ready for `/gsd:verify-work`.

</success_criteria>

<output>
SUMMARY documents: the integrated transport-switch shape used, exact help-text now shown, the manual Claude Code smoke result (tools listed + one tool round-trip succeeded), confirmation that Pitfalls 2 and 5 from research are now closed, and the requirements traceability flip from Pending to Complete. Also note that existing scheduled tasks created by `--task` continue to inherit the new default `http` (per RESEARCH §Runtime State Inventory) — call this out as a deployment note.
</output>
