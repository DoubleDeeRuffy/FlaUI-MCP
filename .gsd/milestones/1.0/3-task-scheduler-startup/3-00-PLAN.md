---
phase: 3-task-scheduler-startup
plan: 00
type: execute
wave: 0
depends_on: []
files_modified:

  - verify.cmd
  - .gsd/milestones/1.0/3-task-scheduler-startup/UAT-CHECKLIST.md

autonomous: true
requirements:

  - TSK-01
  - TSK-02
  - TSK-03
  - TSK-04
  - TSK-06
  - TSK-07
  - TSK-09

must_haves:
  truths:

    - verify.cmd exists at repo root and runs build + 4 static smoke checks
    - UAT-CHECKLIST.md exists with 10 manual UAT scenarios mirroring Phase 2 format
    - verify.cmd exits 0 only when all checks green; exits non-zero on any failure
  artifacts:

    - [object Object]
    - [object Object]
  key_links:

    - [object Object]
    - [object Object]

---

<objective>
Author Wave 0 validation infrastructure that downstream plans depend on. Per VALIDATION.md, Phase 3 has no automated test framework (matches Phase 2 manual-UAT precedent), so we ship a `verify.cmd` script that bundles `dotnet build -c Release` plus four `findstr` static smoke checks AND a `UAT-CHECKLIST.md` enumerating the 10 manual UAT scenarios across TSK-01..TSK-09.

Purpose: Provide a one-shot `verify.cmd` for `/gsd:verify-work` and a checklist for the human UAT pass at phase end (Plan 07).

Output:

- `verify.cmd` (repo root) — exits 0 only when build succeeds and all 4 smoke checks pass
- `.gsd/milestones/1.0/3-task-scheduler-startup/UAT-CHECKLIST.md` — 10-scenario manual UAT checklist

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
@.gsd/milestones/1.0/3-task-scheduler-startup/3-CONTEXT.md
@.gsd/milestones/1.0/3-task-scheduler-startup/3-RESEARCH.md
@.gsd/milestones/1.0/3-task-scheduler-startup/3-VALIDATION.md
</context>

<tasks>

<task>
  <name>Task 1: Create verify.cmd at repo root</name>
  <files>
    <file>verify.cmd</file>
  </files>
  <action>
  Create `verify.cmd` (Windows batch) at repo root with this exact behavior:

1. Echo header `=== FlaUI-MCP Phase 3 Verify ===`
2. Run `dotnet build src\FlaUI.Mcp\FlaUI.Mcp.csproj -c Release` — on non-zero exit, echo FAIL and `exit /b 1`.
3. Run these 4 static smoke checks (each must produce output OR specific absence as noted):
   - `findstr /C:"<OutputType>WinExe</OutputType>" src\FlaUI.Mcp\FlaUI.Mcp.csproj` — must match (TSK-01)
   - `findstr /C:"Microsoft.Extensions.Hosting.WindowsServices" src\FlaUI.Mcp\FlaUI.Mcp.csproj` — must NOT match (TSK-07; if findstr returns 0, that means a hit, which is a fail — invert)
   - `findstr /C:"WinTaskSchedulerManager" src\FlaUI.Mcp\Program.cs` — must match (TSK-02)
   - `findstr /C:"AttachConsole" src\FlaUI.Mcp\Program.cs` — must match (TSK-04)
   - `findstr /C:"schtasks" src\FlaUI.Mcp\Program.cs` — must NOT match (TSK-02 raw shell-out removed)
4. On any failed check, echo `FAIL: <check>` and `exit /b 1`.
5. On all green, echo `=== ALL GREEN ===` and `exit /b 0`.

Use `@echo off` at the top. Use `if errorlevel 1` blocks (or check %errorlevel%) properly for the must-NOT-match cases (these check inversion). Note: `findstr` returns 0 if found, 1 if not found; for must-match, treat errorlevel 1 as failure; for must-NOT-match, treat errorlevel 0 as failure.

Note: this script will FAIL initially against the unedited Program.cs / csproj — that is expected. It becomes green only after Plans 01..06 complete. Wave 0's job is to ship the gate, not to satisfy it.
  </action>
  <verify>
  <verify>
  <automated>type verify.cmd</automated>
</verify>

File exists at `verify.cmd` repo root. Manual: `verify.cmd` exits non-zero against current code (expected) and will exit 0 after all phase plans complete.
  </verify>
  <done>verify.cmd exists at repo root, is a valid .cmd batch file, contains `dotnet build`, contains all 5 findstr lines (4 must-match + 1 must-NOT-match logic), uses correct errorlevel inversion for must-NOT-match cases, returns 0/1 cleanly.</done>
</task>

<task>
  <name>Task 2: Create UAT-CHECKLIST.md</name>
  <files>
    <file>.gsd/milestones/1.0/3-task-scheduler-startup/UAT-CHECKLIST.md</file>
  </files>
  <action>
  Create `UAT-CHECKLIST.md` enumerating all manual UAT scenarios. Use a checklist format (Markdown checkboxes `- [ ]`) with these 10 scenarios, each with a one-line goal and the exact step-by-step instructions:

1. **TSK-01 — WinExe headless**: Run `FlaUI.Mcp.exe` via Task Scheduler. Open Process Explorer. Verify NO `conhost.exe` child process.
2. **TSK-02 — Task registers in user session**: Run elevated cmd: `FlaUI.Mcp.exe --task`. Log off, log on. Open Task Manager → Details. Verify `FlaUI.Mcp.exe` Session column = user's session (NOT 0).
3. **TSK-02 — Skoosoft API used (visual)**: Run `schtasks /query /tn FlaUI-MCP /v /fo LIST`. Verify `Run As User` shows InteractiveToken context, `Run Level` shows Highest, task is hidden.
4. **TSK-03 — Idempotent removetask**: Run `FlaUI.Mcp.exe --removetask` twice in a row. Both must exit 0, no error to console.
5. **TSK-04 — AttachConsole displays output**: Open cmd.exe. Run `FlaUI.Mcp.exe --help`. Verify help text prints to that cmd window (not silently swallowed).
6. **TSK-05 — Debugger guard kills stale procs**: Start `FlaUI.Mcp.exe -c -d` from cmd. F5 in Visual Studio to start a second instance. Verify the first instance is killed, F5 instance survives, debugger stays attached.
7. **TSK-06 — ConsoleTarget gated on -console**: Run as scheduled task (no -c flag). Inspect `Log/Error.log` — confirm no NLog ConsoleTarget write attempts (no internal NLog warnings about target failures).
8. **TSK-07 — WindowsServices package gone**: `dotnet list src\FlaUI.Mcp\FlaUI.Mcp.csproj package` — verify `Microsoft.Extensions.Hosting.WindowsServices` is NOT listed.
9. **TSK-08 — Sizing skipped headless**: Launch via Task Scheduler → tail `Log/Error.log` after startup → no `IOException: The handle is invalid` entry.
10. **TSK-09 — Help layout**: Run `FlaUI.Mcp.exe --help` from cmd. Visually confirm sections appear in this order: Registration (--task/--removetask) → Runtime (-c/-d/-s/--transport/--port) → Aliases (-i/-u). Confirm port shown matches actual default 3020 (not 8080).

**D-1 auto-migration scenario** (bonus, document but mark optional): If a machine has the legacy v0.x `FlaUI-MCP` Windows Service installed, running `--task` must auto-uninstall it silently and emit one info-level NLog line: `Detected legacy FlaUI-MCP Windows Service — uninstalling before creating scheduled task`.

Format: each scenario as `### N. <Title> (<TSK-IDs>)`, then `**Goal:**`, `**Steps:**` (numbered), `**Pass criteria:**`. Include a results-summary table at the bottom (`| Scenario | Status | Notes |`) for the human to fill in during UAT.
  </action>
  <verify>
  <verify>
  <automated>findstr /C:"TSK-01" /C:"TSK-02" /C:"TSK-03" /C:"TSK-04" /C:"TSK-05" /C:"TSK-06" /C:"TSK-07" /C:"TSK-08" /C:"TSK-09" .gsd\milestones\1.0\3-task-scheduler-startup\UAT-CHECKLIST.md</automated>
</verify>

All 9 TSK requirement IDs must match in the file.
  </verify>
  <done>UAT-CHECKLIST.md exists with 10 numbered scenarios, all 9 TSK-* IDs cross-referenced, each scenario has Goal/Steps/Pass-criteria, results-summary table appended.</done>
</task>

</tasks>

<verification>
Both files exist at the specified paths. `verify.cmd` is syntactically valid batch (parses, runs without batch parse errors). UAT-CHECKLIST.md covers all 9 TSK requirements with concrete steps a human can follow.

Run `verify.cmd` against current (unedited) code — it MUST fail (raw schtasks still present, OutputType still Exe). This proves the gate works. After Plans 01..06, the same script must pass.
</verification>

<success_criteria>

- [ ] verify.cmd exists at repo root
- [ ] .gsd/milestones/1.0/3-task-scheduler-startup/UAT-CHECKLIST.md exists
- [ ] verify.cmd contains all 5 documented findstr smoke checks
- [ ] UAT-CHECKLIST.md cross-references all 9 TSK requirement IDs
- [ ] verify.cmd uses correct errorlevel inversion for must-NOT-match (Microsoft.Extensions.Hosting.WindowsServices, schtasks)

</success_criteria>

<output>
SUMMARY records: verify.cmd ships with build + 4 findstr smoke checks (errorlevel inversion correct for must-NOT-match cases). UAT-CHECKLIST.md ships with 10 scenarios, all 9 TSK IDs covered. Wave 0 deliverables complete; Plans 01-06 can now proceed knowing their work will be gated by these artifacts.
</output>
