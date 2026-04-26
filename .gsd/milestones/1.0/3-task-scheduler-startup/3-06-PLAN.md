---
phase: 3-task-scheduler-startup
plan: 06
type: execute
wave: 6
depends_on: [3-05]
files_modified: [src/FlaUI.Mcp/Program.cs]
autonomous: true
requirements: [TSK-09]
must_haves:
  truths:

    - "--help text shows three sections in this order: Registration, Runtime, Aliases (per D-4)"
    - Registration section lists --task and --removetask first
    - Aliases section lists -i/--install and -u/--uninstall mapped to --task/--removetask
    - Help text shows port 3020 (the actual default), not 8080 (the prior drift)
  artifacts:

    - [object Object]
  key_links:

    - [object Object]

---

<objective>
Final review and verification of TSK-09: ensure the `--help` text emitted by Program.cs (already restructured by Plan 03) matches the D-4 layout spec exactly — Registration first, Runtime middle, Aliases last; port 3020 (not 8080); concise and copy-pasteable.

Purpose: Sanity-check pass over the help text that Plan 03 wrote. Plan 03 was focused on the AttachConsole/help-defer mechanics; Plan 06 is the dedicated TSK-09 verification (and minor cosmetic fixes if Plan 03's text drifted from spec).

Output: Program.cs help block matches the canonical layout from RESEARCH §Pattern 5. If Plan 03 already wrote it correctly, this plan is a no-op verification with a SUMMARY confirming compliance.
</objective>

<execution_context>
@$HOME/.claude-account2/get-shit-done/workflows/execute-plan.md
@$HOME/.claude-account2/get-shit-done/templates/summary.md
</execution_context>

<context>
@.gsd/milestones/1.0/3-task-scheduler-startup/3-CONTEXT.md
@.gsd/milestones/1.0/3-task-scheduler-startup/3-RESEARCH.md
@src/FlaUI.Mcp/Program.cs

<interfaces>
Canonical help layout (RESEARCH §Pattern 5):

```
Usage: FlaUI.Mcp.exe [options]

Registration:
  --task              Register as scheduled task (runs at user logon, sees desktop)
  --removetask        Remove scheduled task

Runtime:
  --console, -c       Run in console mode (attach to parent shell, enable ConsoleTarget)
  --debug, -d         Enable debug-level logging (Debug.log)
  --silent, -s        Suppress prompts during registration
  --transport <type>  Transport: sse (default) or stdio
  --port <number>     SSE listen port (default: 3020)
  --help, -?          Show this help

Aliases (compatibility with v0.x service-based scripts):
  --install, -i       Same as --task
  --uninstall, -u     Same as --removetask
```

</interfaces>
</context>

<tasks>

<task>
  <name>Task 1: Verify and finalize --help text per D-4</name>
  <files>
    <file>src/FlaUI.Mcp/Program.cs</file>
  </files>
  <action>
  Read the current `if (helpRequested) { ... }` block in `src/FlaUI.Mcp/Program.cs` (Plan 03 wrote it). Compare line-by-line against the canonical layout in the `<context_interfaces>` section above.

**Required checks:**

1. Section headers in this exact order: `Registration:`, `Runtime:`, `Aliases (compatibility with v0.x service-based scripts):`. Each followed by a blank `Console.WriteLine();`.
2. Registration section contains `--task` and `--removetask`, in that order.
3. Runtime section contains, in this order: `--console, -c`, `--debug, -d`, `--silent, -s`, `--transport <type>`, `--port <number>`, `--help, -?`.
4. Aliases section contains `--install, -i` mapped to `Same as --task` and `--uninstall, -u` mapped to `Same as --removetask`.
5. Port default mention shows `3020`, NOT `8080`.
6. The block ends with `Environment.Exit(0);`.

**If any check fails**, edit the help block to match the canonical layout exactly. Use Console.WriteLine("...") per line; preserve the `\u2014` em-dash on the title line if present (it's English; ASCII hyphen-hyphen also acceptable per Plan 03's note).

**If all checks pass**, this task is verification-only — no edits needed. Record `no-op verification` in the SUMMARY.

**Do NOT touch** any code outside the `if (helpRequested) { ... }` block.

After any edit (or no-op verification), run `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Release` to confirm it still builds. Then run `verify.cmd` from the repo root — it MUST now exit 0 (this is the first plan after which all 5 static smoke checks should be green: WinExe present, WindowsServices absent, WinTaskSchedulerManager present, AttachConsole present, schtasks absent).
  </action>
  <verify>
  <verify>
  <automated>dotnet build src\FlaUI.Mcp\FlaUI.Mcp.csproj -c Release && findstr /C:"Registration:" /C:"Runtime:" /C:"Aliases" src\FlaUI.Mcp\Program.cs && findstr /C:"3020" src\FlaUI.Mcp\Program.cs && (findstr /C:"default: 8080" src\FlaUI.Mcp\Program.cs && exit 1 || exit 0) && verify.cmd</automated>
</verify>

All three section headers present. Port 3020 mentioned. No `default: 8080` drift. verify.cmd exits 0 (the full Wave 0 gate).
  </verify>
  <done>Build green. Help text matches D-4 layout (Registration > Runtime > Aliases). Port shows 3020. verify.cmd exits 0 — every static smoke check is green.</done>
</task>

</tasks>

<verification>
Build succeeds. Help text section ordering matches D-4. Port drift fixed. verify.cmd is fully green — Wave 0 gate confirms all 5 static smoke checks pass against the final code.
</verification>

<success_criteria>

- [ ] Three sections in correct order: Registration, Runtime, Aliases
- [ ] Port 3020 (not 8080)
- [ ] Aliases section maps -i/-u to --task/--removetask
- [ ] verify.cmd exits 0

</success_criteria>

<output>
SUMMARY records: D-4 help layout confirmed; port drift fixed (or already fixed by Plan 03); verify.cmd green — Phase 3 build + static checks complete. Plan 07 (manual UAT) unblocked.
</output>
