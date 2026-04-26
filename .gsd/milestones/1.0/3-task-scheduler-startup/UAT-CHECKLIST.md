# Phase 3 — Task Scheduler & Startup: Manual UAT Checklist

Run after all phase plans (01..06) complete. Each scenario maps to one or more TSK-* requirements. Tick the box once the pass criterion is met on a real Windows host.

---

### 1. WinExe headless launch (TSK-01)

- [ ] **Goal:** Confirm `FlaUI.Mcp.exe` runs without spawning a console host when launched via Task Scheduler.
- [ ] **Steps:**
  1. Register the scheduled task: `FlaUI.Mcp.exe --task` (elevated cmd).
  2. In Task Scheduler, locate `FlaUI-MCP` and click **Run**.
  3. Open Process Explorer (Sysinternals) and locate the running `FlaUI.Mcp.exe`.
  4. Expand its child-process tree.
- [ ] **Pass criteria:** No `conhost.exe` child appears under `FlaUI.Mcp.exe`.

---

### 2. Task registers in user session (TSK-02)

- [ ] **Goal:** Verify the scheduled task runs in the interactive user session, not Session 0.
- [ ] **Steps:**
  1. Run elevated cmd: `FlaUI.Mcp.exe --task`.
  2. Log off the current Windows user, then log back in.
  3. Open Task Manager → **Details** tab → enable the **Session** column (View → Select columns).
  4. Locate `FlaUI.Mcp.exe`.
- [ ] **Pass criteria:** `Session` column shows the current user's session ID (typically 1, 2, …) — **NOT** 0.

---

### 3. Skoosoft API used — visual schtasks check (TSK-02)

- [ ] **Goal:** Confirm the task is created via the TaskScheduler COM API (Skoosoft `WinTaskSchedulerManager`) with the expected security profile.
- [ ] **Steps:**
  1. After `FlaUI.Mcp.exe --task` succeeds, run: `schtasks /query /tn FlaUI-MCP /v /fo LIST`.
  2. Inspect the output.
- [ ] **Pass criteria:** `Run As User` reflects an InteractiveToken context, `Run Level` shows `Highest`, and the task is marked hidden.

---

### 4. Idempotent removetask (TSK-03)

- [ ] **Goal:** `--removetask` is safe to run repeatedly.
- [ ] **Steps:**
  1. Run `FlaUI.Mcp.exe --removetask` (elevated cmd).
  2. Immediately run `FlaUI.Mcp.exe --removetask` again.
- [ ] **Pass criteria:** Both invocations exit with code 0; no error message printed on the second run (already-absent task is not an error).

---

### 5. AttachConsole displays output (TSK-04)

- [ ] **Goal:** When launched from an existing cmd.exe, `FlaUI.Mcp.exe` attaches to that console and prints output.
- [ ] **Steps:**
  1. Open `cmd.exe`.
  2. Run `FlaUI.Mcp.exe --help`.
- [ ] **Pass criteria:** Help text is printed to the cmd.exe window (output is visible, not silently swallowed).

---

### 6. Debugger guard kills stale procs (TSK-05)

- [ ] **Goal:** Starting a Visual Studio debug session evicts any prior running instance so the debugger attaches cleanly.
- [ ] **Steps:**
  1. From cmd, run `FlaUI.Mcp.exe -c -d`.
  2. Open the FlaUI-MCP solution in Visual Studio and press **F5**.
- [ ] **Pass criteria:** The first cmd-launched instance is terminated automatically; the F5 instance survives; the debugger remains attached.

---

### 7. ConsoleTarget gated on -console (TSK-06)

- [ ] **Goal:** NLog ConsoleTarget is only enabled when `-c` / `--console` is specified, preventing startup warnings under Task Scheduler.
- [ ] **Steps:**
  1. Run `FlaUI.Mcp.exe` via the scheduled task (no `-c` flag).
  2. Open `Log/Error.log`.
- [ ] **Pass criteria:** No NLog internal warnings about ConsoleTarget write failures appear in the log.

---

### 8. WindowsServices package gone (TSK-07)

- [ ] **Goal:** The legacy `Microsoft.Extensions.Hosting.WindowsServices` NuGet package is fully removed.
- [ ] **Steps:**
  1. Run `dotnet list src\FlaUI.Mcp\FlaUI.Mcp.csproj package`.
- [ ] **Pass criteria:** `Microsoft.Extensions.Hosting.WindowsServices` is **not** listed.

---

### 9. Sizing skipped headless (TSK-08)

- [ ] **Goal:** Headless launch (no console window) skips terminal-sizing logic that would otherwise throw.
- [ ] **Steps:**
  1. Launch `FlaUI.Mcp.exe` via Task Scheduler.
  2. Tail `Log/Error.log` immediately after startup.
- [ ] **Pass criteria:** No `IOException: The handle is invalid` (or similar Console buffer-handle exception) appears.

---

### 10. Help layout (TSK-09)

- [ ] **Goal:** `--help` output is grouped logically and shows the correct default port.
- [ ] **Steps:**
  1. Open cmd and run `FlaUI.Mcp.exe --help`.
- [ ] **Pass criteria:** Sections appear in this order:
  1. **Registration** — `--task`, `--removetask`
  2. **Runtime** — `-c`, `-d`, `-s`, `--transport`, `--port`
  3. **Aliases** — `-i`, `-u`

  Default port shown is **3020** (not 8080).

---

### Bonus (optional). D-1 auto-migration from legacy Windows Service

- [ ] **Goal:** If the legacy v0.x `FlaUI-MCP` Windows Service is installed, `--task` silently uninstalls it before creating the scheduled task.
- [ ] **Steps:**
  1. On a host that still has the legacy `FlaUI-MCP` Windows Service registered, run elevated `FlaUI.Mcp.exe --task`.
  2. Inspect `Log/Error.log` and check `sc query FlaUI-MCP`.
- [ ] **Pass criteria:** Service is gone; one info-level NLog line reads:
  `Detected legacy FlaUI-MCP Windows Service — uninstalling before creating scheduled task`.

---

## Results Summary

| Scenario | Status | Notes |
|----------|--------|-------|
| 1. WinExe headless (TSK-01) |  |  |
| 2. Task registers in user session (TSK-02) |  |  |
| 3. Skoosoft API used — schtasks visual (TSK-02) |  |  |
| 4. Idempotent removetask (TSK-03) |  |  |
| 5. AttachConsole displays output (TSK-04) |  |  |
| 6. Debugger guard kills stale procs (TSK-05) |  |  |
| 7. ConsoleTarget gated on -console (TSK-06) |  |  |
| 8. WindowsServices package gone (TSK-07) |  |  |
| 9. Sizing skipped headless (TSK-08) |  |  |
| 10. Help layout (TSK-09) |  |  |
| Bonus. D-1 auto-migration |  |  |
