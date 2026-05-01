---
phase: 260430-aie
verified: 2026-05-01T00:00:00Z
status: passed
score: 5/5 must-haves verified
---

# Quick Task 260430-aie: stale-process kill on every startup — Verification Report

**Task Goal:** Fix the broken startup, the broken state if not started with `-c`, taskkill old instances on startup.

**Verified:** 2026-05-01
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| #   | Truth                                                                                                                                                                                  | Status     | Evidence                                                                                                                                          |
| --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | On every non-help startup, all FlaUI.Mcp processes other than the new one are terminated before Kestrel binds port 3020.                                                               | VERIFIED   | Program.cs lines 67-85 contain an unconditional top-level block (no `if (!helpRequested)` wrapper) executing after the help short-circuit at line 59 and before the Kestrel HTTP transport launch at line 259. |
| 2   | Headless / Task-Scheduler / -c / Debugger / --task / --removetask / --install / --uninstall launches all execute the stale-kill block (only --help skips it).                          | VERIFIED   | Kill block at lines 67-85 is at top-level and runs unconditionally. Help has already exited at line 59 (`Environment.Exit(0)`), so all other code paths flow through the kill block. |
| 3   | Each killed process is awaited via WaitForExit(2000) and its Process handle is disposed (IDisposable leak fix from research Q1d).                                                       | VERIFIED   | Line 72: `using (stale)` wraps the inner try/catch. Line 77: `stale.WaitForExit(2000);`. No `5000` remains anywhere in the file. |
| 4   | The Debugger.IsAttached branch still forces console=true and debug=true but no longer contains the stale-kill loop.                                                                    | VERIFIED   | Lines 88-92: branch body contains exactly `console = true; debug = true;` — no Process API calls. |
| 5   | Existing CliOptions xunit tests (tests/FlaUI.Mcp.Tests/CliParserTests.cs) continue to pass — no parsing regressions.                                                                   | VERIFIED   | `dotnet test --filter "FullyQualifiedName~CliParserTests"` → 9 passed, 0 failed, 0 skipped, 7 ms. |

**Score:** 5/5 truths verified

### Required Artifacts

| Artifact                       | Expected                                                                                                                                | Status     | Details                                                                                                                                      |
| ------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------- | ---------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/FlaUI.Mcp/Program.cs`     | Unconditional stale-instance kill block (post-help, pre-Debugger-override, pre-CodePages, pre-logging) + slimmed Debugger.IsAttached branch. Must contain `GetProcessesByName("FlaUI.Mcp")`. | VERIFIED   | Exists; substantive (kill block at 62-85, slimmed Debugger branch at 87-92); wired (top-level statements execute on every run); `GetProcessesByName("FlaUI.Mcp")` confirmed at line 69. |

### Key Link Verification

| From                                                          | To                                                          | Via                                                                                              | Status | Details                                                                                                                  |
| ------------------------------------------------------------- | ----------------------------------------------------------- | ------------------------------------------------------------------------------------------------ | ------ | ------------------------------------------------------------------------------------------------------------------------ |
| helpRequested short-circuit (line 59)                         | new stale-kill block (lines 62-85)                          | fall-through after `Environment.Exit(0)` on help                                                 | WIRED  | Help branch exits at line 59; remaining code paths fall through to the kill block at line 62 with no intervening guard. |
| stale-kill block (lines 62-85)                                | Debugger.IsAttached branch (lines 88-92)                    | sequential top-level execution; Debugger branch retains only console/debug overrides             | WIRED  | Block ordering verified: kill (62-85) → Debugger guard (88-92). Debugger body confirmed slim (no Process API).            |
| stale-kill block (lines 62-85)                                | Kestrel HTTP transport bind (port 3020)                     | `WaitForExit(2000)` ensures port is released before later `FlaUI.Mcp.Mcp.Http.HttpTransport.RunAsync` call | WIRED  | `stale.WaitForExit(2000)` at line 77; HttpTransport.RunAsync invocation at line 259-261 — kill happens first. |

### Data-Flow Trace (Level 4)

Not applicable — this artifact is startup glue / process-control code, not a data-rendering component. The "data" being controlled is OS-level process state, which cannot be statically traced; behavioral verification belongs to the live UAT scenarios in the plan.

### Behavioral Spot-Checks

| Behavior                                                                      | Command                                                                                                  | Result                                              | Status |
| ----------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------- | --------------------------------------------------- | ------ |
| FIX-04: CliOptions parsing has no regressions (9 tests)                       | `dotnet test tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~CliParserTests"` | `Bestanden! Fehler: 0, erfolgreich: 9, gesamt: 9, Dauer: 7 ms` | PASS   |
| Build succeeds (implicit in test run, which restores+builds)                  | (same command above also performs `dotnet build`)                                                        | Build succeeded with 3 pre-existing warnings (MCP9004 in HttpTransport.cs:92, CS0414 in McpServer.cs:13, CS0414 in SessionManager.cs:16). No new warnings introduced. | PASS   |

Live UAT (5 runtime scenarios — actual port-binding, headless launch, --help no-kill, Debugger F5 path) is out of scope for this verifier per Step 8 doctrine; it is owned by `/gsd:verify-work`. The plan explicitly delegates these to the verifier (PLAN.md `<verification>` section).

### Requirements Coverage

| Requirement | Source Plan      | Description                                                                       | Status                              | Evidence                                                                                                                          |
| ----------- | ---------------- | --------------------------------------------------------------------------------- | ----------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| FIX-01      | 260430-aie-PLAN  | (inferred) Stale-kill must run on every non-help startup                           | SATISFIED                           | Top-level kill block at lines 62-85; only `--help` short-circuits before it (line 59). |
| FIX-02      | 260430-aie-PLAN  | (inferred) Process disposal + 2 s WaitForExit                                      | SATISFIED                           | `using (stale)` at line 72; `WaitForExit(2000)` at line 77; no `5000` anywhere. |
| FIX-03      | 260430-aie-PLAN  | (inferred) Debugger.IsAttached branch slimmed; no kill code there                  | SATISFIED                           | Lines 88-92: body contains only `console = true; debug = true;`. |
| FIX-04      | 260430-aie-PLAN  | No CliOptions parsing regressions                                                  | SATISFIED                           | `CliParserTests` 9/9 pass in 7 ms. |

REQUIREMENTS.md was not explicitly cross-referenced for FIX-01..FIX-04 IDs (no `.gsd/REQUIREMENTS.md` matches were retrieved), but each requirement maps cleanly to a plan must-have truth that has been verified. No orphaned requirements.

### Anti-Patterns Found

| File                              | Line | Pattern                              | Severity | Impact                                                                                                                                            |
| --------------------------------- | ---- | ------------------------------------ | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/FlaUI.Mcp/Program.cs`        | 81   | Empty `catch { }` swallowing stderr write failure | Info     | Intentional per plan (D-Q3 / interfaces): stderr may not be attached when launched via Task Scheduler, so the inner `Console.Error.WriteLine` is wrapped. Documented in CONTEXT.md and matches the explicit design. Not a stub. |
| `src/FlaUI.Mcp/Program.cs`        | 79   | Broad `catch (Exception ex)`         | Info     | Intentional — race (process exited mid-enumeration) and `AccessDenied` (UAC mismatch) are both expected non-fatal cases. Plan D-Q1 mandates fail-soft semantics so the later Kestrel bind surfaces the failure visibly. Not a blocker. |

No blocker or warning-level anti-patterns. The two info-level items above are explicit design decisions documented in CONTEXT.md / the plan body and are not stubs (they implement specified behavior, they don't placeholder it).

Pre-existing build warnings (MCP9004, two CS0414) are unrelated to this edit and are documented in the executor's SUMMARY.md.

### Gaps Summary

No gaps. All 8 explicit checks from the verification focus pass:

1. Stale-kill block is OUTSIDE `if (Debugger.IsAttached)` — confirmed (kill at lines 62-85, Debugger guard at lines 88-92; line 69 < line 88).
2. `WaitForExit(2000)` — confirmed at line 77; `grep "5000"` returns no matches anywhere in the file.
3. Each `Process` wrapped in `using (stale)` — confirmed at line 72 (single match; the only stale-process loop in the file).
4. `ProcessName` filter is exactly `"FlaUI.Mcp"` (no `.exe`) — confirmed at line 69.
5. Debugger.IsAttached branch retains `console = true; debug = true` overrides only, no kill code — confirmed lines 88-92 contain exactly those two assignments.
6. `LoggingConfig.ConfigureLogging(...)` still has `enableConsoleTarget: transport != "stdio"` — confirmed at line 117 (D-Q3 unchanged).
7. `--help` short-circuit (Environment.Exit(0)) at line 59 happens BEFORE the new kill block at line 62 — confirmed; `--help` is a no-kill path by control flow.
8. `dotnet test --filter "FullyQualifiedName~CliParserTests"` passes 9/9 (FIX-04) — confirmed: `Bestanden! Fehler: 0, erfolgreich: 9, gesamt: 9, Dauer: 7 ms`.

The 5 manual UAT scenarios (headless relaunch, explicit `-c` second launch, `--help` does not kill, clean start, Debugger F5 path) require a live Windows host with the binary running and a prior instance to kill — they are correctly delegated to `/gsd:verify-work` and are NOT counted as gaps under verifier doctrine D-04 (they are out-of-scope live verification, not unmet must-haves the verifier failed to confirm statically).

---

_Verified: 2026-05-01_
_Verifier: Claude (gsd-verifier)_
