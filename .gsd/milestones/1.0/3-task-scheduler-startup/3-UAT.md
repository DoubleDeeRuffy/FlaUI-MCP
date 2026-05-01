---
status: testing
phase: 3-task-scheduler-startup
source:
  - 3-00-SUMMARY.md
  - 3-01-SUMMARY.md
  - 3-02-SUMMARY.md
  - 3-03-SUMMARY.md
  - 3-04-SUMMARY.md
  - 3-05-SUMMARY.md
  - 3-06-SUMMARY.md
started: 2026-04-26T00:00:00Z
updated: 2026-04-26T00:00:00Z
---

## Current Test

number: 1
name: WinExe headless launch (TSK-01)
expected: |
  After `FlaUI.Mcp.exe --task` (elevated) and running it from Task Scheduler,
  Process Explorer shows no `conhost.exe` child under `FlaUI.Mcp.exe`.
awaiting: user response

## Tests

### 1. WinExe headless launch (TSK-01)
expected: After `FlaUI.Mcp.exe --task` and triggering the task in Task Scheduler, no `conhost.exe` child appears under `FlaUI.Mcp.exe` in Process Explorer.
result: [pending]

### 2. Task registers in user session (TSK-02)
expected: After `FlaUI.Mcp.exe --task`, log off/on, Task Manager → Details shows `FlaUI.Mcp.exe` running in the current user's interactive session (Session ID 1+, NOT 0).
result: [pending]

### 3. Skoosoft API used — schtasks visual (TSK-02)
expected: `schtasks /query /tn FlaUI-MCP /v /fo LIST` shows InteractiveToken Run As User, Run Level Highest, task marked hidden.
result: [pending]

### 4. Idempotent removetask (TSK-03)
expected: Running `FlaUI.Mcp.exe --removetask` twice in a row both exit 0 with no error on the second call.
result: [pending]

### 5. AttachConsole displays output (TSK-04)
expected: Running `FlaUI.Mcp.exe --help` from cmd.exe prints help text into that cmd window (output visible, not swallowed).
result: [pending]

### 6. Debugger guard kills stale procs (TSK-05)
expected: With a `FlaUI.Mcp.exe -c -d` already running, pressing F5 in Visual Studio kills the prior instance; the F5 instance survives with debugger attached.
result: [pending]

### 7. ConsoleTarget gated on -console (TSK-06)
expected: Scheduled task launch (no `-c`) writes no NLog ConsoleTarget warnings into `Log/Error.log`.
result: [pending]

### 8. WindowsServices package gone (TSK-07)
expected: `dotnet list src\FlaUI.Mcp\FlaUI.Mcp.csproj package` does not list `Microsoft.Extensions.Hosting.WindowsServices`.
result: [pending]

### 9. Sizing skipped headless (TSK-08)
expected: Task Scheduler launch produces no `IOException: The handle is invalid` (or similar Console buffer-handle exception) in `Log/Error.log`.
result: [pending]

### 10. Help layout (TSK-09)
expected: `FlaUI.Mcp.exe --help` shows sections in order — Registration (--task, --removetask), Runtime (-c, -d, -s, --transport, --port), Aliases (-i, -u). Default port 3020.
result: [pending]

### 11. Bonus — D-1 auto-migration from legacy Windows Service
expected: On a host with the legacy `FlaUI-MCP` Windows Service installed, elevated `FlaUI.Mcp.exe --task` removes the service and logs an info line: "Detected legacy FlaUI-MCP Windows Service — uninstalling before creating scheduled task". `sc query FlaUI-MCP` then reports the service is gone.
result: [pending]

## Summary

total: 11
passed: 0
issues: 0
pending: 11
skipped: 0

## Gaps

[none yet]
