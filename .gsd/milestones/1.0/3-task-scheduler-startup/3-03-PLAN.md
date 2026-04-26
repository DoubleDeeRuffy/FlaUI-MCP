---
phase: 3-task-scheduler-startup
plan: 03
type: execute
wave: 3
depends_on: [3-02]
files_modified: [src/FlaUI.Mcp/Program.cs]
autonomous: true
requirements: [TSK-04, TSK-08]
must_haves:
  truths:

    - AttachConsole(ATTACH_PARENT_PROCESS) is called immediately after CLI parsing, before any Console.WriteLine
    - AttachConsole gates are -console, -install/-i, -uninstall/-u, --task, --removetask, --help
    - Console window sizing block is gated on the `console` flag (not Environment.UserInteractive)
    - When launched headless by Task Scheduler, Console.BufferWidth/WindowWidth/WindowHeight are not assigned
  artifacts:

    - [object Object]
  key_links:

    - [object Object]
    - [object Object]

---

<objective>
Implement TSK-04 (AttachConsole P/Invoke for parent-shell output under WinExe) and TSK-08 (console window sizing guarded against headless mode).

Under `OutputType=WinExe` (Plan 01), no console is allocated at process creation. Any `Console.WriteLine` becomes a silent no-op unless we re-attach to the parent shell's console via `AttachConsole(ATTACH_PARENT_PROCESS)`. We do this when the user explicitly invokes a CLI mode (-c, -i/-install, -u/-uninstall, --task, --removetask, --help). Additionally, the existing sizing block (lines 82-94) must be re-guarded: `Environment.UserInteractive` returns true under InteractiveToken even when no console is attached, which causes `Console.BufferWidth = 180` to throw `IOException: The handle is invalid` (RESEARCH Pitfall 3).

Purpose: Make CLI feedback visible to the user; prevent IOException crash when launched headless by Task Scheduler.

Output: Program.cs with NativeMethods static class (AttachConsole P/Invoke), AttachConsole call positioned after CLI parsing but before any Console.* call (including the --help block), sizing block guarded by `if (console)`.
</objective>

<execution_context>
@$HOME/.claude-account2/get-shit-done/workflows/execute-plan.md
@$HOME/.claude-account2/get-shit-done/templates/summary.md
</execution_context>

<context>
@.gsd/milestones/1.0/3-task-scheduler-startup/3-RESEARCH.md
@src/FlaUI.Mcp/Program.cs

<interfaces>
P/Invoke target:

```csharp
using System.Runtime.InteropServices;

internal static class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool AttachConsole(int dwProcessId);
    internal const int ATTACH_PARENT_PROCESS = -1;
}
```

Current --help block (Program.cs:57-74) calls Console.WriteLine inline DURING the for-loop arg parsing. After Plan 03, the --help case must defer printing OR call AttachConsole locally before printing. Recommended: set a `helpRequested = true;` flag in the loop, exit the loop normally, then run the AttachConsole gate, then print help and exit.

Current sizing block (Program.cs:82-94):

```csharp
if (!Debugger.IsAttached && Environment.UserInteractive)
{
    try { Console.BufferWidth = 180; Console.WindowWidth = 180; Console.WindowHeight = 50; }
    catch { }
}
```

Must become:

```csharp
if (console)
{
    try { Console.BufferWidth = 180; Console.WindowWidth = 180; Console.WindowHeight = 50; }
    catch { }
}
```

Note: dropping the `!Debugger.IsAttached` predicate is correct because under F5 the Debugger guard (Plan 04) sets `console = true` explicitly; the original guard's intent was "only when running interactively from cmd", which `console` flag now expresses cleanly.
</interfaces>
</context>

<tasks>

<task>
  <name>Task 1: Add NativeMethods (AttachConsole P/Invoke), restructure --help, add AttachConsole gate, fix sizing guard</name>
  <files>
    <file>src/FlaUI.Mcp/Program.cs</file>
  </files>
  <action>
  Make the following precise changes to `src/FlaUI.Mcp/Program.cs`:

**Change 1 — Add NativeMethods static class.** Because Program.cs uses top-level statements, this class must go AT THE BOTTOM of the file (after all top-level statements) OR in a new file. Prefer keeping it inline at the bottom to minimize file count. Add at the very end of Program.cs:

```csharp

internal static class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool AttachConsole(int dwProcessId);
    internal const int ATTACH_PARENT_PROCESS = -1;
}
```

(Use fully-qualified `System.Runtime.InteropServices.DllImport` to avoid adding a new `using` directive at the top — top-level statements + `using` placement is finicky.)

**Change 2 — Restructure --help to defer printing.** Replace the existing `case "--help" or "-?":` block (Program.cs:57-74) with:

```csharp
case "--help" or "-?":
    helpRequested = true;
    break;
```

Add `var helpRequested = false;` at the top with the other flag locals (around line 22, near `var port = 3020;`).

**Change 3 — Insert AttachConsole gate immediately after the for-loop ends (after line 76, before the `Encoding.RegisterProvider` call at line 79):**

```csharp
// === 1b. Re-attach to parent console under WinExe so Console.* writes are visible (TSK-04) ===
if (console || install || uninstall || task || removeTask || helpRequested)
{
    NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
    // Return value intentionally ignored: false means no parent console (e.g. launched
    // by Task Scheduler) — Console.WriteLine then becomes a silent no-op, which is fine.
}
```

**Change 4 — Print help AFTER AttachConsole, then exit.** Immediately after the AttachConsole gate, add:

```csharp
if (helpRequested)
{
    Console.WriteLine("FlaUI-MCP \u2014 MCP server for Windows desktop automation");
    Console.WriteLine();
    Console.WriteLine("Usage: FlaUI.Mcp.exe [options]");
    Console.WriteLine();
    Console.WriteLine("Registration:");
    Console.WriteLine("  --task              Register as scheduled task (runs at user logon, sees desktop)");
    Console.WriteLine("  --removetask        Remove scheduled task");
    Console.WriteLine();
    Console.WriteLine("Runtime:");
    Console.WriteLine("  --console, -c       Run in console mode (attach to parent shell, enable ConsoleTarget)");
    Console.WriteLine("  --debug, -d         Enable debug-level logging (Debug.log)");
    Console.WriteLine("  --silent, -s        Suppress prompts during registration");
    Console.WriteLine("  --transport <type>  Transport: sse (default) or stdio");
    Console.WriteLine("  --port <number>     SSE listen port (default: 3020)");
    Console.WriteLine("  --help, -?          Show this help");
    Console.WriteLine();
    Console.WriteLine("Aliases (compatibility with v0.x service-based scripts):");
    Console.WriteLine("  --install, -i       Same as --task");
    Console.WriteLine("  --uninstall, -u     Same as --removetask");
    Environment.Exit(0);
}
```

This emits the help text from Plan 06's design AND fixes the port-drift bug (8080 → 3020) — Plan 06 will verify this is the final wording but doesn't need to change it again.

**Change 5 — Fix sizing guard.** Replace the existing block at Program.cs:82-94:

```csharp
if (!Debugger.IsAttached && Environment.UserInteractive)
```

with:

```csharp
if (console)
```

Keep the `try { Console.BufferWidth = 180; Console.WindowWidth = 180; Console.WindowHeight = 50; } catch { /* ignore */ }` body unchanged.

**Sequencing matters:** the order in Program.cs after this plan must be:

1. CLI parse (lines 26-76, with --help now setting helpRequested instead of writing inline)
2. AttachConsole gate (NEW, immediately after parse)
3. Help print + exit (NEW, immediately after AttachConsole)
4. `Encoding.RegisterProvider(...)` (existing line 79)
5. Sizing block (existing line 82, now guarded by `if (console)`)
6. CleanOldLogfiles, ConfigureLogging, ... (rest unchanged)

After the edit: `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Release` must succeed.
  </action>
  <verify>
  <verify>
  <automated>dotnet build src\FlaUI.Mcp\FlaUI.Mcp.csproj -c Release && findstr /C:"NativeMethods.AttachConsole" /C:"ATTACH_PARENT_PROCESS" /C:"if (console)" src\FlaUI.Mcp\Program.cs && (findstr /C:"Environment.UserInteractive" src\FlaUI.Mcp\Program.cs | findstr /C:"BufferWidth" && exit 1 || exit 0)</automated>
</verify>

AttachConsole call present, ATTACH_PARENT_PROCESS constant present, sizing block uses `if (console)`. Old `Environment.UserInteractive` guard on sizing is gone (Environment.UserInteractive may still appear elsewhere — only the BufferWidth-context one must be replaced).
  </verify>
  <done>Build green. AttachConsole called after CLI parse, before any Console.WriteLine. --help block deferred to fire after AttachConsole. Sizing block guarded by `if (console)` (not UserInteractive). NativeMethods class defined.</done>
</task>

</tasks>

<verification>
Build succeeds. Static greps confirm AttachConsole and `if (console)` sizing guard. Manual UAT (deferred to Plan 07) verifies: launching `FlaUI.Mcp.exe --help` from cmd shows the new help text in that cmd window; launching via Task Scheduler does NOT throw IOException at startup.
</verification>

<success_criteria>

- [ ] AttachConsole P/Invoke present and called from gate
- [ ] Gate triggers on console|install|uninstall|task|removeTask|helpRequested
- [ ] Help printing deferred until AFTER AttachConsole
- [ ] Sizing block guarded by `if (console)` only
- [ ] Help text shows port 3020 (not 8080)

</success_criteria>

<output>
SUMMARY records: NativeMethods class added; AttachConsole gate placed immediately after CLI parse; help text restructured per D-4 (Registration → Runtime → Aliases) with port 3020 fix; sizing guard switched from UserInteractive to `console` flag.
</output>
