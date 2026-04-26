---
phase: 3-task-scheduler-startup
plan: 05
type: execute
wave: 5
depends_on: [3-04]
files_modified: [src/FlaUI.Mcp/Program.cs]
autonomous: true
requirements: [TSK-06]
must_haves:
  truths:

    - ConfigureLogging is called with enableConsoleTarget gated on the `console` flag, not on transport == 'sse'
    - When running headless (Task Scheduler, no -console), ConsoleTarget is NOT attached
  artifacts:

    - [object Object]
  key_links:

    - [object Object]

---

<objective>
Implement TSK-06: switch the NLog ConsoleTarget gate predicate from `transport == "sse"` (the current Program.cs:101 wiring) to the explicit `console` flag.

Why: under Phase 3, the default transport is `sse` AND the default execution context is headless (Task Scheduler launch, no console attached). The current predicate would attempt to attach a ConsoleTarget under headless conditions — NLog writes silently fail or warn, log entries can be lost (RESEARCH Pitfall 4). The correct semantic is: ConsoleTarget should attach IFF the user explicitly asked for console mode (-c / --console).

Purpose: Decouple log-target choice from transport choice. Console output is a UX preference, not a transport implementation detail.

Output: Program.cs:101 (the `LoggingConfig.ConfigureLogging` call site) updated to pass `enableConsoleTarget: console` instead of `enableConsoleTarget: transport == "sse"`.
</objective>

<execution_context>
@$HOME/.claude-account2/get-shit-done/workflows/execute-plan.md
@$HOME/.claude-account2/get-shit-done/templates/summary.md
</execution_context>

<context>
@.gsd/milestones/1.0/3-task-scheduler-startup/3-RESEARCH.md
@src/FlaUI.Mcp/Program.cs

<interfaces>
Current call site (Program.cs:101):

```csharp
LoggingConfig.ConfigureLogging(debug, logDirectory, enableConsoleTarget: transport == "sse");
```

Target call site:

```csharp
LoggingConfig.ConfigureLogging(debug, logDirectory, enableConsoleTarget: console);
```

`LoggingConfig.ConfigureLogging` signature already accepts `bool enableConsoleTarget` per RESEARCH §Pattern 3 — this is purely a caller-side change. No edits to `src/FlaUI.Mcp/Logging/LoggingConfig.cs` required.
</interfaces>
</context>

<tasks>

<task>
  <name>Task 1: Switch ConfigureLogging predicate from transport-equality to console-flag</name>
  <files>
    <file>src/FlaUI.Mcp/Program.cs</file>
  </files>
  <action>
  In `src/FlaUI.Mcp/Program.cs`, locate the line:

```csharp
LoggingConfig.ConfigureLogging(debug, logDirectory, enableConsoleTarget: transport == "sse");
```

(currently at line ~101, may shift due to Plan 03 / 04 additions above it)

Change the third argument from `enableConsoleTarget: transport == "sse"` to `enableConsoleTarget: console`.

After the edit:

```csharp
LoggingConfig.ConfigureLogging(debug, logDirectory, enableConsoleTarget: console);
```

**Do NOT touch:**

- The `LoggingConfig.ConfigureLogging` method definition in `src/FlaUI.Mcp/Logging/LoggingConfig.cs` — its signature is correct as-is.
- Any other use of the `transport` variable elsewhere in Program.cs (it's still needed for the SSE-vs-stdio runtime branch at line 267).
- The `console` flag declaration or any other site that reads it.

**Sequencing rationale:** Plan 04's Debugger.IsAttached block sets `console = true` BEFORE this ConfigureLogging call — meaning F5 debug correctly enables ConsoleTarget. Production headless launch leaves `console = false`, ConsoleTarget is skipped, no Pitfall-4 warnings.

After the edit: `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Release` must succeed.
  </action>
  <verify>
  <verify>
  <automated>dotnet build src\FlaUI.Mcp\FlaUI.Mcp.csproj -c Release && findstr /C:"enableConsoleTarget: console" src\FlaUI.Mcp\Program.cs && (findstr /C:"enableConsoleTarget: transport" src\FlaUI.Mcp\Program.cs && exit 1 || exit 0)</automated>
</verify>

New predicate present. Old `enableConsoleTarget: transport == "sse"` predicate gone.
  </verify>
  <done>Build green. ConfigureLogging called with `enableConsoleTarget: console`. The old `transport == "sse"` predicate string is no longer present at this call site.</done>
</task>

</tasks>

<verification>
Build succeeds. Static greps confirm predicate switch. Manual UAT (Plan 07 Scenario 7): run as scheduled task with no `-c` flag → tail Error.log → no NLog ConsoleTarget warnings; no Pitfall-4 lost log entries.
</verification>

<success_criteria>

- [ ] ConfigureLogging called with enableConsoleTarget: console
- [ ] Old transport-based predicate at this call site removed
- [ ] Build green

</success_criteria>

<output>
SUMMARY records: TSK-06 implemented as a single-call-site predicate switch. ConsoleTarget now correctly tracks user intent (-c flag), not transport choice.
</output>
