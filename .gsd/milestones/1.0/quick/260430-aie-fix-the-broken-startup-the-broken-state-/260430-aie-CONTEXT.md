---
name: 260430-aie-CONTEXT
quick_id: 260430-aie
description: fix the broken startup, the broken state if not started with "-c", taskkill old instances on startup
gathered: 2026-04-30
status: Ready for planning
---

# Quick Task 260430-aie: fix broken startup + stale-process kill — Context

<domain>
## Task Boundary

Fix the broken startup path — specifically the "broken state when not started with `-c`" — by killing stale `FlaUI.Mcp` processes on every startup. The current behavior already kills stale processes, but only inside the `if (Debugger.IsAttached)` branch (Program.cs:63-82). When the binary is launched headlessly (Task Scheduler at logon, or no flags), the kill step is skipped and a previous instance can still hold port 3020 — the new instance then fails silently to bind, giving the appearance of "broken" startup without any visible error (no console attached → no diagnostic output).

Scope:
- `src/FlaUI.Mcp/Program.cs` only — startup ordering and the stale-kill block.
- Tests project (Phase 4 introduced) — verify CliOptions parsing untouched; the kill itself is OS-level and not mocked.

Out of scope:
- ConsoleTarget gating logic (decision 3c — keep current `transport != "stdio"` behaviour).
- LoggingConfig.cs — unchanged.
- New CLI flags or behaviour.
</domain>

<decisions>
## Implementation Decisions

### Stale-instance kill scope (Q1 → 1a)
- **Always kill stale `FlaUI.Mcp` processes on every startup**, excluding own PID.
- Matches the user's "taskkill old instances on startup" requirement literally.
- Prevents port-conflict + duplicate-binding failures on Task Scheduler relaunches at logon.
- Skip the kill ONLY for `--help` (which Environment.Exits before any binding) — help should be a passive no-op.

### Stale-kill wait timeout (Q2 → 2a)
- `WaitForExit(2000)` per stale process — down from current 5000.
- Keeps startup snappy if multiple zombies pile up.

### ConsoleTarget gating (Q3 → 3c)
- **Leave existing logic untouched** — `enableConsoleTarget: transport != "stdio"`.
- Phase-3 TSK-06 said "gate on `-console`", but the user has chosen to defer that; the current quick task does not address it.

### Claude's Discretion
- **Helper extraction:** The stale-kill code is a small block (~10 lines) — extract into a private static helper `KillStaleInstances()` in Program.cs (top-level statements file) for readability and testability. Inline if a helper conflicts with top-level statement layout.
- **Ordering:** Run stale-kill AFTER `--help` short-circuit but BEFORE `Debugger.IsAttached` flag-override and BEFORE logging configuration. The Debugger branch should still set `console = true; debug = true` but no longer perform the kill (it's now redundant).
- **Logging:** Stale-kill happens before NLog is configured, so use `Console.Error.WriteLine` with try/catch for diagnostics; failures must not abort startup.
</decisions>

<specifics>
## Specific Ideas

- Extract logic from `Program.cs:63-82` (Debugger.IsAttached branch) into a separate top-level block.
- Implementation pattern (top-level statements):
  ```csharp
  // Always kill stale FlaUI.Mcp instances to avoid port-bind conflicts on Task Scheduler relaunch
  if (!helpRequested)
  {
      var currentPid = Environment.ProcessId;
      foreach (var stale in Process.GetProcessesByName("FlaUI.Mcp")
                                   .Where(p => p.Id != currentPid))
      {
          try
          {
              stale.Kill();
              stale.WaitForExit(2000);
          }
          catch
          {
              // Race: process may have exited between enumeration and kill
          }
      }
  }
  ```
- The Debugger.IsAttached branch keeps `console = true; debug = true` only.
</specifics>

<canonical_refs>
## Canonical References

- Phase 3 plan 3-04-PLAN.md: original Debugger.IsAttached + stale-kill design (TSK-05).
- Phase 4 streamable-http-transport: introduced http transport on port 3020 (default) — the port that conflicts when stale instances persist.
- TSK-05 success criterion: "`Debugger.IsAttached` auto-enables `-c -d` flags and kills stale FlaUI.Mcp processes (excluding own PID)" — the kill behavior is being **broadened**, not removed.
</canonical_refs>
