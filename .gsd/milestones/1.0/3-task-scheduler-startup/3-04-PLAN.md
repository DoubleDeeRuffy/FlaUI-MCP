---
phase: 3-task-scheduler-startup
plan: 04
type: execute
wave: 4
depends_on: [3-03]
files_modified: [src/FlaUI.Mcp/Program.cs]
autonomous: true
requirements: [TSK-05]
must_haves:
  truths:

    - When Debugger.IsAttached is true, console = true and debug = true are auto-set
    - Debugger guard kills any FlaUI.Mcp processes other than the current PID
    - Own PID is NEVER killed (Where(p => p.Id != currentPid))
  artifacts:

    - [object Object]
  key_links:

    - [object Object]

---

<objective>
Implement TSK-05: when running under a debugger (F5 in Visual Studio / Rider), auto-enable `-c -d` flags AND kill any leftover `FlaUI.Mcp` processes from previous debug sessions, EXCLUDING the current PID.

The own-PID exclusion is critical (RESEARCH Pitfall 2): without `.Where(p => p.Id != Environment.ProcessId)`, the kill loop targets self, the debugger immediately disconnects, and `dotnet run` exits with code -1.

Purpose: Smooth F5 debugging experience. Stale processes from a previous debug session that crashed or hung are auto-cleaned. Auto-enabling -c -d ensures debug logging + console output during the debug session without the developer needing to configure launch profile args.

Output: Program.cs with `if (Debugger.IsAttached)` block placed immediately after CLI parsing (and after the AttachConsole gate from Plan 03), setting `console = true; debug = true;` and killing stale FlaUI.Mcp processes excluding own PID.
</objective>

<execution_context>
@$HOME/.claude-account2/get-shit-done/workflows/execute-plan.md
@$HOME/.claude-account2/get-shit-done/templates/summary.md
</execution_context>

<context>
@.gsd/milestones/1.0/3-task-scheduler-startup/3-RESEARCH.md
@src/FlaUI.Mcp/Program.cs

<interfaces>
Existing imports in Program.cs (line 1): `using System.Diagnostics;` is ALREADY present — Process and Debugger types are accessible without new usings.

Existing flag locals (line 19-22, after Plan 03's helpRequested addition): `console`, `debug` are local bool vars declared at the top, modifiable.

Process name to target: the EXE name without extension is `FlaUI.Mcp` (matches `<AssemblyName>FlaUI.Mcp</AssemblyName>` in csproj). `Process.GetProcessesByName("FlaUI.Mcp")` returns all matching processes including self.
</interfaces>
</context>

<tasks>

<task>
  <name>Task 1: Add Debugger.IsAttached guard with stale-process kill</name>
  <files>
    <file>src/FlaUI.Mcp/Program.cs</file>
  </files>
  <action>
  Insert this block in `src/FlaUI.Mcp/Program.cs` immediately AFTER the help-print exit block (added by Plan 03) and BEFORE `Encoding.RegisterProvider(...)`:

```csharp
// === 1c. Debugger guard: F5 from VS auto-enables -c -d, kills stale FlaUI.Mcp procs (TSK-05) ===
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
```

**Critical:** the `.Where(p => p.Id != currentPid)` clause is mandatory. Without it the process kills itself.

**LINQ availability:** `using System.Linq;` is implicit via `<ImplicitUsings>enable</ImplicitUsings>` in csproj — no using directive needed.

**Process.GetProcessesByName is in System.Diagnostics** (already imported via `using System.Diagnostics;` at line 1).

**Sequencing rationale:** placing this AFTER the help-exit means `--help` does not trigger a stale-process kill (good — running `FlaUI.Mcp.exe --help` from cmd shouldn't murder a running production task). Placing it BEFORE `Encoding.RegisterProvider` and the sizing block means the `console = true; debug = true;` assignment is visible to the sizing guard (`if (console)`) and to ConfigureLogging (Plan 05 makes ConfigureLogging read `console`).

Note on tightening criteria (CONTEXT D-5 "Claude's Discretion"): kill-by-name across all sessions is the literal requirement. Same-session/same-user tightening is NOT applied here — for this project's solo-developer context, simpler is better. If a future cross-session debugger collision surfaces, that's a follow-up phase.

After the edit: `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Release` must succeed.
  </action>
  <verify>
  <verify>
  <automated>dotnet build src\FlaUI.Mcp\FlaUI.Mcp.csproj -c Release && findstr /C:"Debugger.IsAttached" /C:"GetProcessesByName" /C:"Environment.ProcessId" src\FlaUI.Mcp\Program.cs</automated>
</verify>

All three substrings must match. Reading the source must confirm the LINQ `Where` clause excludes own PID.
  </verify>
  <done>Build green. Debugger.IsAttached block present, sets console=true and debug=true, enumerates FlaUI.Mcp processes via GetProcessesByName, filters with .Where(p => p.Id != currentPid), Kill() inside try/catch, WaitForExit(5000).</done>
</task>

</tasks>

<verification>
Build succeeds. Static greps confirm guard structure and own-PID exclusion. Manual UAT (Plan 07): F5 with a stale FlaUI.Mcp.exe running → confirm stale PID killed via Task Manager + own debugger session survives.
</verification>

<success_criteria>

- [ ] Debugger.IsAttached guard present after help-exit and before Encoding.RegisterProvider
- [ ] console=true and debug=true auto-set under debugger
- [ ] Stale FlaUI.Mcp processes killed
- [ ] Own PID never killed (LINQ filter present)

</success_criteria>

<output>
SUMMARY records: TSK-05 implemented. F5 debug auto-flags + stale-process cleanup with own-PID exclusion in place.
</output>
