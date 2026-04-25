---
status: complete
phase: 2-service-hardening
source: [2-01-SUMMARY.md, 2-02-SUMMARY.md]
started: 2026-03-18T00:00:00Z
updated: 2026-03-18T22:10:00Z
---

## Tests

### 1. Cold Start Smoke Test
expected: Build and run FlaUI.Mcp.exe from a clean state. The server boots without errors on the SSE transport (now default). Console shows startup log output. No crash, no missing assembly errors.
result: [pass] Server boots on SSE transport, NLog console output visible, no crashes after fixing missing Skoosoft.CmdProcessLib.dll

### 2. CLI Flag Parsing
expected: Running `FlaUI.Mcp.exe --debug --console` parses both flags correctly. Running with `--transport stdio` overrides the SSE default. Running with `--port 9090` changes the listening port. All flag combinations work without errors.
result: [pass] Replaced fragile string.Join+Contains parsing with switch/case. All flags work with -- prefix.

### 3. Service Install
expected: Running `FlaUI.Mcp.exe --install` (as admin) registers a Windows Service named "FlaUI-MCP" in services.msc. A firewall rule is created. The process exits immediately with code 0.
result: [pass] Service installs after adding Skoosoft.ProcessLib package (provides CmdProcessLib.dll). Required AddWindowsService() for SCM reporting.

### 4. Service Uninstall
expected: Running `FlaUI.Mcp.exe --uninstall` (as admin) removes the "FlaUI-MCP" service. The process exits immediately with code 0.
result: [pass] Service uninstalls correctly.

### 5. Silent Install/Uninstall
expected: Running with `--silent` completes without prompts.
result: [pass] Silent mode works for both install and uninstall.

### 6. Stop Running Service Before Console
expected: With FlaUI-MCP service running, launching interactively stops the existing service first.
result: [pass] ServiceController.Stop is called before server start in interactive mode.

### 7. Console Window Sizing
expected: When running interactively, console resizes to 180x50.
result: [pass] Console sizing works when not under debugger and UserInteractive is true.

### 8. Startup Sequence Order
expected: Canonical order: CLI parse, Console sizing, CleanOldLogfiles, ConfigureLogging, Crash handler, Firewall, Stop service, Install/Uninstall, Run.
result: [pass] Verified in Program.cs — all sections numbered and in correct order.

### 9. Crash Handler
expected: AppDomain.UnhandledException logs to Error.log before process terminates.
result: [pass] Handler registered at step 6, main try-catch also logs via logger?.Error.

### 10. Scheduled Task (added during UAT)
expected: Running `--task` creates a Windows Scheduled Task that runs at user logon in the user's session (not Session 0). FlaUI can see desktop windows. `--removetask` removes it.
result: [pass] Scheduled task created, FlaUI-MCP running in user session can list windows and click elements on remote server via MCP SSE.

## Summary

total: 10
passed: 10
issues: 0
pending: 0
skipped: 0

## Issues Found & Fixed

### 1. Missing Skoosoft.CmdProcessLib.dll
- **Root cause**: Transitive dependency Skoosoft.ServiceHelperLib → Skoosoft.ProcessLib not copied to output
- **Fix**: Added explicit `Skoosoft.ProcessLib` package reference to csproj
- **Commit**: 15e6ffa

### 2. Fragile CLI arg parsing
- **Root cause**: `string.Join(" ", args).Contains("-i")` substring matching was unreliable
- **Fix**: Replaced with proper `switch (args[i].ToLowerInvariant())` in for-loop
- **Commit**: 15e6ffa

### 3. Service start timeout (30s)
- **Root cause**: SSE host didn't report to Windows Service Control Manager
- **Fix**: Added `builder.Services.AddWindowsService()` in SseTransport.cs + `Microsoft.Extensions.Hosting.WindowsServices` NuGet package
- **Commit**: 15e6ffa

### 4. Service can't see desktop windows (Session 0 isolation)
- **Root cause**: Windows Services run in Session 0, isolated from user desktop
- **Fix**: Added `--task`/`--removetask` flags for scheduled task registration (runs in user session at logon). Updated Setup.iss to use tasks instead of services.
- **Commit**: 15e6ffa

## Gaps

[none]
