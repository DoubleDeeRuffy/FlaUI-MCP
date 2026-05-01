---
phase: 260430-aie
plan: "01"
type: execute
wave: 1
depends_on: []
files_modified:
  - src/FlaUI.Mcp/Program.cs
autonomous: true
requirements:
  - FIX-01
  - FIX-02
  - FIX-03
  - FIX-04
must_haves:
  truths:
    - "On every non-help startup, all FlaUI.Mcp processes other than the new one are terminated before Kestrel binds port 3020."
    - "Headless / Task-Scheduler / -c / Debugger / --task / --removetask / --install / --uninstall launches all execute the stale-kill block (only --help skips it)."
    - "Each killed process is awaited via WaitForExit(2000) and its Process handle is disposed (IDisposable leak fix from research Q1d)."
    - "The Debugger.IsAttached branch still forces console=true and debug=true but no longer contains the stale-kill loop."
    - "Existing CliOptions xunit tests (tests/FlaUI.Mcp.Tests/CliParserTests.cs) continue to pass — no parsing regressions."
  artifacts:
    - path: src/FlaUI.Mcp/Program.cs
      provides: "Unconditional stale-instance kill block (post-help, pre-Debugger-override, pre-CodePages, pre-logging) + slimmed Debugger.IsAttached branch"
      contains: 'GetProcessesByName\("FlaUI\.Mcp"\)'
  key_links:
    - from: "src/FlaUI.Mcp/Program.cs (helpRequested short-circuit, line ~59)"
      to: "src/FlaUI.Mcp/Program.cs (new stale-kill block)"
      via: "fall-through after Environment.Exit(0) on help"
      pattern: 'Environment\.Exit\(0\);[\s\S]+?GetProcessesByName'
    - from: "src/FlaUI.Mcp/Program.cs (new stale-kill block)"
      to: "src/FlaUI.Mcp/Program.cs (Debugger.IsAttached branch)"
      via: "sequential top-level execution; Debugger branch retains only console/debug overrides"
      pattern: 'if \(Debugger\.IsAttached\)\s*\{\s*console = true;\s*debug = true;\s*\}'
    - from: "src/FlaUI.Mcp/Program.cs (stale-kill block)"
      to: "Kestrel HTTP transport bind (port 3020)"
      via: "WaitForExit(2000) ensures port is released before later FlaUI.Mcp.Mcp.Http.HttpTransport.RunAsync call"
      pattern: 'WaitForExit\(2000\)'
---

<objective>
Fix the broken startup path where headless / non-`-c` / Task-Scheduler-relaunched FlaUI.Mcp instances silently fail to bind port 3020 because a stale instance is still listening. Today, the stale-instance kill only runs inside the `if (Debugger.IsAttached)` branch (Program.cs:63-82), so production launches skip it.

**What this plan does:**
- Lifts the stale-kill loop out of the Debugger branch into its own unconditional top-level block, gated only by `!helpRequested`.
- Reduces `WaitForExit(5000)` to `WaitForExit(2000)` (per D-Q2 — keeps startup snappy).
- Wraps each `Process` in `using` to fix the IDisposable handle leak surfaced by research Q1d.
- Slims the Debugger.IsAttached branch to only set `console = true; debug = true` (kill is now redundant there).
- Diagnostics on failure go to `Console.Error.WriteLine` inside the existing try/catch (NLog is not yet configured at this point); failures must not abort startup.

**Why:** Task Scheduler relaunches at logon (Phase-3 design) collide with prior instances holding port 3020 (Phase-4 streamable HTTP). The new instance binds-fails, no console is attached, no error is visible — the user sees a "broken" startup. Killing stale instances unconditionally restores the visible-success / visible-failure invariant.

**Output:** A single edit to `src/FlaUI.Mcp/Program.cs` and a passing `dotnet test` run on the existing CliOptions tests. No new tests (research Q4 — Process API is unmockable without an out-of-scope abstraction refactor). Manual UAT happens via the 5-scenario checklist in `<verification>`.
</objective>

<execution_context>
@$HOME/.claude-account2/get-shit-done/workflows/execute-plan.md
@$HOME/.claude-account2/get-shit-done/templates/summary.md
</execution_context>

<context>
@.gsd/PROJECT.md
@.gsd/STATE.md
@.gsd/milestones/1.0/quick/260430-aie-fix-the-broken-startup-the-broken-state-/260430-aie-CONTEXT.md
@.gsd/milestones/1.0/quick/260430-aie-fix-the-broken-startup-the-broken-state-/260430-aie-RESEARCH.md
@src/FlaUI.Mcp/Program.cs
@src/FlaUI.Mcp/CliOptions.cs
@tests/FlaUI.Mcp.Tests/CliParserTests.cs

<interfaces>
## Current Program.cs structure (the parts that matter)

```csharp
// Line 17: opts parsed from CliOptions.Parse(args)
var opts = FlaUI.Mcp.CliOptions.Parse(args);
// ... var assignments through line 27 ...
var helpRequested = opts.Help;

// Lines 30-35: AttachConsole when console/install/uninstall/task/removeTask/help

// Lines 37-60: helpRequested short-circuit — prints help, Environment.Exit(0)

// Lines 62-82: CURRENT location of the kill — INSIDE Debugger.IsAttached branch.
if (Debugger.IsAttached)
{
    console = true;
    debug = true;

    var currentPid = Environment.ProcessId;
    foreach (var stale in Process.GetProcessesByName("FlaUI.Mcp")
                                 .Where(p => p.Id != currentPid))
    {
        try
        {
            stale.Kill();
            stale.WaitForExit(5000);
        }
        catch
        {
            // Process may have exited between enumeration and kill — race is fine.
        }
    }
}

// Line 85: CodePagesEncodingProvider
// Line 88+: console window sizing, CleanOldLogfiles, ConfigureLogging, ...
```

## Target structure after this plan

```csharp
// Lines 17-27: opts parsing — UNCHANGED
// Lines 30-35: AttachConsole — UNCHANGED
// Lines 37-60: helpRequested short-circuit (Environment.Exit(0)) — UNCHANGED

// === NEW: 1c. Always kill stale FlaUI.Mcp instances (excluding own PID) before binding ports ===
// Runs on every startup except --help. NLog is not yet configured here, so diagnostics go to stderr.
var currentPid = Environment.ProcessId;
foreach (var stale in Process.GetProcessesByName("FlaUI.Mcp")
                             .Where(p => p.Id != currentPid))
{
    using (stale)
    {
        try
        {
            stale.Kill();
            stale.WaitForExit(2000);
        }
        catch (Exception ex)
        {
            // Race (process exited between enumeration and kill) or AccessDenied (UAC mismatch).
            // Non-fatal: if a stale instance survives, port-bind will fail visibly later.
            try { Console.Error.WriteLine($"FlaUI.Mcp: stale-kill skipped (pid={stale.Id}): {ex.Message}"); } catch { }
        }
    }
}

// === 1d. Debugger guard: F5 from VS auto-enables -c -d (kill code REMOVED — handled above) ===
if (Debugger.IsAttached)
{
    console = true;
    debug = true;
}

// Line 85+ onwards: UNCHANGED (CodePagesEncodingProvider, console sizing, logging, ...)
```

## Constraints (from CONTEXT + RESEARCH)

- `ProcessName` is `"FlaUI.Mcp"` — NO `.exe` extension (research Q2; verified against `<AssemblyName>FlaUI.Mcp</AssemblyName>` in csproj).
- `WaitForExit(2000)` — not 5000 (D-Q2).
- Skip ONLY for `--help`. `--task`, `--removetask`, `--install`, `--uninstall`, `-c`, headless default — all kill.
- The existing `--help` branch already calls `Environment.Exit(0)` at line 59, so the new kill block does NOT need an explicit `if (!helpRequested)` guard — control flow guarantees help has already exited. (You MAY still wrap in `if (!helpRequested)` for explicitness — Claude's discretion. Both are correct.)
- NLog is configured AFTER the kill block (line 107) — use `Console.Error.WriteLine` for any diagnostics, wrapped in `try { ... } catch { }` because stderr may not be attached.
- Wrap each `Process` in `using` (research Q1d — IDisposable handle leak fix).
- Do NOT touch `LoggingConfig.ConfigureLogging` call (line 107) — `enableConsoleTarget: transport != "stdio"` stays per D-Q3.
- Do NOT add new CLI flags. Do NOT touch `CliOptions.cs`.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Move stale-kill block out of Debugger branch and add IDisposable / 2 s timeout</name>
  <files>src/FlaUI.Mcp/Program.cs</files>
  <action>
Edit `src/FlaUI.Mcp/Program.cs` to relocate and harden the stale-instance kill logic.

**Step-by-step edits:**

1. **Locate** the existing `if (Debugger.IsAttached) { ... }` block at lines 62-82.

2. **Replace** that whole block with the following two consecutive blocks (preserving the `// === ... ===` comment-banner style used elsewhere in the file):

```csharp
// === 1c. Always kill stale FlaUI.Mcp instances (excluding own PID) before any port-binding work. ===
// Runs on every startup except --help (which already Environment.Exit(0)'d above).
// NLog is not yet configured here — diagnostics go to stderr inside try/catch so failures never
// abort startup. If a kill fails (race / AccessDenied), the later Kestrel bind will fail visibly,
// which is the correct user-facing failure mode.
{
    var currentPid = Environment.ProcessId;
    foreach (var stale in Process.GetProcessesByName("FlaUI.Mcp")
                                 .Where(p => p.Id != currentPid))
    {
        using (stale)
        {
            try
            {
                stale.Kill();
                stale.WaitForExit(2000);
            }
            catch (Exception ex)
            {
                try { Console.Error.WriteLine($"FlaUI.Mcp: stale-kill skipped (pid={stale.Id}): {ex.Message}"); } catch { }
            }
        }
    }
}

// === 1d. Debugger guard: F5 from VS auto-enables -c -d (TSK-05; stale-kill is now unconditional above) ===
if (Debugger.IsAttached)
{
    console = true;
    debug = true;
}
```

3. **Do NOT** wrap the stale-kill block in `if (!helpRequested)` — the help branch already calls `Environment.Exit(0)` at line 59 before reaching this code. Adding the guard is harmless but redundant; either choice is acceptable. Pick whichever you find clearer; the existing comment makes the invariant explicit.

4. **Verify nothing else changed:** `using` directives at lines 1-9 must remain identical (`System.Diagnostics`, `System.Linq` is implicit via global usings — confirm by attempting to build; if `Where` is unresolved, add `using System.Linq;` explicitly). `CodePagesEncodingProvider` registration (now at line ~85) must remain; the `if (console) { Console.BufferWidth = ... }` block must remain; `LogArchiver.CleanOldLogfiles`, `LoggingConfig.ConfigureLogging`, the firewall/service/task blocks, and the transport switch at the bottom must all remain untouched.

5. **Confirm `ProcessName` is `"FlaUI.Mcp"` (NO extension)** — do not change this. Research Q2 confirms `Process.ProcessName` strips `.exe` from the friendly name; `"FlaUI.Mcp.exe"` would silently match zero processes.

6. **Confirm wait is 2000 ms, not 5000 ms** — per locked decision D-Q2.

**Per locked decisions:**
- D-Q1 (kill on every startup, exclude self, skip --help only): the new block runs unconditionally after the help short-circuit; PID exclusion via `p.Id != currentPid`.
- D-Q2 (WaitForExit(2000)): replaces the old 5000.
- D-Q3 (ConsoleTarget gating untouched): do NOT modify the `LoggingConfig.ConfigureLogging(..., enableConsoleTarget: transport != "stdio")` call.
- Discretion (helper vs inline): inlining keeps the top-level statement layout clean and avoids the `internal static class NativeMethods` precedent of needing a separate type — inline is the preferred shape per CONTEXT.md.
- Discretion (ordering): post-help, pre-Debugger-override, pre-CodePages, pre-logging — exactly as written above.
- Discretion (stderr diagnostics): inner `try { Console.Error.WriteLine(...) } catch { }` because stderr may not be attached when launched headlessly via Task Scheduler.
- Research Q1d (IDisposable leak): `using (stale) { ... }` wrapping the inner try/catch.
  </action>
  <verify>
**Automated:**

```bash
# 1. Build must succeed without warnings new to this edit.
dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo /p:TreatWarningsAsErrors=false

# 2. Existing CliOptions xunit tests must still pass (FIX-04 — no parsing regression).
dotnet test tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj --nologo --filter "FullyQualifiedName~CliParserTests"
```

**Static spot-checks (the executor should grep to confirm):**

```bash
# A. Kill block is OUTSIDE the Debugger branch (the line containing GetProcessesByName must NOT be inside the if-block that contains Debugger.IsAttached).
grep -n "GetProcessesByName\|Debugger.IsAttached\|WaitForExit" src/FlaUI.Mcp/Program.cs
# Expected: GetProcessesByName line number is LESS than the Debugger.IsAttached line number.

# B. WaitForExit timeout is 2000.
grep -n "WaitForExit" src/FlaUI.Mcp/Program.cs
# Expected: "WaitForExit(2000)" — no 5000 anywhere.

# C. ProcessName argument is exactly "FlaUI.Mcp" (no .exe).
grep -n 'GetProcessesByName("' src/FlaUI.Mcp/Program.cs
# Expected: GetProcessesByName("FlaUI.Mcp") — NOT "FlaUI.Mcp.exe".

# D. `using (stale)` is present for IDisposable correctness.
grep -n "using (stale)" src/FlaUI.Mcp/Program.cs
# Expected: at least one match.

# E. Debugger.IsAttached branch is now slim — only sets console/debug, no Process / Kill / GetProcessesByName.
grep -n -A 5 "if (Debugger.IsAttached)" src/FlaUI.Mcp/Program.cs
# Expected: body contains exactly `console = true; debug = true;` and closing brace — no Process API calls.

# F. ConsoleTarget gating untouched (D-Q3 guard).
grep -n "enableConsoleTarget" src/FlaUI.Mcp/Program.cs
# Expected: enableConsoleTarget: transport != "stdio" — unchanged.
```

<automated>dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo &amp;&amp; dotnet test tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj --nologo --filter "FullyQualifiedName~CliParserTests"</automated>

**Both `dotnet build` and `dotnet test` MUST exit 0. All six grep checks MUST match expectations. If any fail, fix before claiming the task done.**
  </verify>
  <done>Program.cs compiles and `dotnet test --filter CliParserTests` passes; the stale-kill loop lives in its own top-level block before the Debugger.IsAttached branch; each Process is wrapped in `using`; WaitForExit timeout is 2000 ms; the Debugger.IsAttached body contains only `console = true; debug = true;`.</done>
</task>

</tasks>

<verification>
## Phase-level verification

### Automated (must pass for plan to be considered done)

```bash
dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo
dotnet test tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj --nologo --filter "FullyQualifiedName~CliParserTests"
```

Both must exit 0. The `CliParserTests` filter is the FIX-04 guard: parsing semantics must be unchanged.

### Manual UAT — 5 scenarios (transcribed from RESEARCH.md §4c)

**The verifier (`/gsd:verify-work`) will execute these on a Windows host. The executor producing this fix is NOT expected to run them — they are listed here so the verifier knows what to do.**

**Scenario 1 — Headless Task-Scheduler-style relaunch (THE failing case being fixed):**
1. Start instance A: `FlaUI.Mcp.exe` (no flags → http transport, port 3020).
2. Confirm A listening: `netstat -ano | findstr :3020` → expect `LISTENING` row with A's PID.
3. Start instance B with no flags.
4. **Expected:** B kills A; B binds 3020.
5. Verify: `Get-Process -Name FlaUI.Mcp` returns exactly one row (B's PID); `netstat -ano | findstr :3020` shows B's PID.

**Scenario 2 — Explicit `-c` second launch (the previously broken case the user reported):**
1. Start instance A headless (no flags).
2. Start instance B with `-c`.
3. **Expected:** B kills A; B's console attaches; ConsoleTarget output appears in B's terminal; port 3020 binds in B.
4. Verify console output is visible in B's terminal; A is gone from `tasklist /FI "IMAGENAME eq FlaUI.Mcp.exe"`.

**Scenario 3 — `--help` does NOT kill (the only excluded path):**
1. Start instance A: `FlaUI.Mcp.exe`.
2. From another shell run `FlaUI.Mcp.exe --help`.
3. **Expected:** help text prints, exit code 0; A still running.
4. Verify: `Get-Process -Name FlaUI.Mcp` still shows A's PID.

**Scenario 4 — Clean start (no stale processes):**
1. Ensure no `FlaUI.Mcp` is running (`Stop-Process -Name FlaUI.Mcp -Force` if needed).
2. Start instance A.
3. **Expected:** clean startup, no stderr noise from the kill block, port 3020 bound.
4. Verify: stderr is empty (or contains only normal log routing); the empty enumeration in the kill loop is a silent no-op.

**Scenario 5 — Debugger F5 path (regression check for Phase-3 TSK-05):**
1. From Visual Studio / Rider, F5-launch a Debug build with no args.
2. Pre-condition: have a prior `FlaUI.Mcp` instance running outside the debugger.
3. **Expected:** the new debugger-attached process applies `console = true; debug = true` (visible: ConsoleTarget output, debug-level log entries) AND the prior instance is killed (now via the unconditional block, not the Debugger branch).
4. Verify: only the debugger's PID appears in `Get-Process -Name FlaUI.Mcp`; debug.log is being written.

### Pass criteria for the plan

- Automated: both `dotnet build` and `dotnet test --filter CliParserTests` exit 0.
- Static spot-checks (Task 1's six grep assertions): all match.
- Manual UAT: scenarios 1-5 all pass when run by the verifier.
- No new `.cs` files; only `src/FlaUI.Mcp/Program.cs` is modified.
</verification>

<success_criteria>
- src/FlaUI.Mcp/Program.cs compiles (`dotnet build` exit 0).
- Existing CliOptions xunit tests pass (`dotnet test --filter CliParserTests` exit 0) — proves FIX-04 (no parsing regression).
- The stale-kill loop is OUTSIDE the `if (Debugger.IsAttached)` branch — `grep -n` confirms `GetProcessesByName` line number < `Debugger.IsAttached` line number.
- `Process.GetProcessesByName("FlaUI.Mcp")` is called with NO `.exe` extension (research Q2 — verified to match the actual exe friendly name).
- Each enumerated `Process` is wrapped in `using` (research Q1d — IDisposable handle leak fix).
- `WaitForExit(2000)` — locked decision D-Q2 (replaces former 5000).
- The `Debugger.IsAttached` body contains only `console = true; debug = true;` — no Process API calls remain in that branch.
- ConsoleTarget gating (`enableConsoleTarget: transport != "stdio"`) is unchanged — D-Q3 guard.
- Manual UAT scenarios 1-5 from `<verification>` all pass when run by `/gsd:verify-work`.
</success_criteria>

<output>
After completion, create `.gsd/milestones/1.0/quick/260430-aie-fix-the-broken-startup-the-broken-state-/260430-aie-01-SUMMARY.md` documenting:
- The exact diff applied to `src/FlaUI.Mcp/Program.cs` (kill block relocation, 5000 → 2000 timeout, `using` wrapper, slimmed Debugger branch, stderr diagnostics).
- `dotnet build` + `dotnet test --filter CliParserTests` results (both expected: exit 0).
- The six static-grep assertions and their results.
- A note that manual UAT scenarios 1-5 are the verifier's responsibility (per research Q4 — Process API not unit-testable without an out-of-scope abstraction refactor).
- Confirmation that `CliOptions.cs` and `LoggingConfig.cs` were NOT touched (D-Q3 + scope guard).
</output>
