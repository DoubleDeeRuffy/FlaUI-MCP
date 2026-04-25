---
status: testing
phase: 1-logging-infrastructure
source: 1-01-SUMMARY.md, 1-02-SUMMARY.md
started: 2026-03-18T12:00:00Z
updated: 2026-03-18T07:50:00Z
---

## Current Test

number: 3
name: Debug.log Only With -debug Flag
expected: |
  Start the server WITHOUT `-debug` or `-d` flag — no `Debug.log` file in the Log directory. Stop the server. Start again WITH `-debug` or `-d` flag — `Debug.log` file is now created in the Log directory with detailed log entries.
awaiting: user response

## Tests

### 1. Cold Start Smoke Test
expected: Kill any running FlaUI-MCP server. Start the application from scratch. Server boots without errors and a `Log` directory is created under the app's base directory.
result: issue
reported: "the server is not booting without --transport parameter"
severity: major
fix-applied: "Added System.Text.Encoding.CodePages NuGet package, registered CodePagesEncodingProvider early in startup, wrapped FirewallManager call in try-catch. Root cause: FirewallManager.CheckRule crashed with NotSupportedException for encoding 850 (German Windows codepage) — crash happened before main try-catch block."

### 2. Error.log Always Created
expected: After starting the server (any mode), check the `Log` directory. An `Error.log` file exists (may be empty if no errors occurred, but the file should be present or created on first error).
result: issue
reported: "the log directory does not exist, neither a logfile"
severity: major
fix-applied: "Same root cause as Test 1 — server crashed before logging setup could run. After fix, Log directory is created but Error.log only appears on first error-level event (standard NLog behavior — FileTarget creates file lazily on first matching write)."

### 3. Debug.log Only With -debug Flag
expected: Start the server WITHOUT `-debug` or `-d` flag — no `Debug.log` file in the Log directory. Stop the server. Start again WITH `-debug` or `-d` flag — `Debug.log` file is now created in the Log directory with detailed log entries.
result: [pending]

### 4. Log Archive on Restart
expected: With existing .log files in the Log directory from a previous run, restart the server. The old .log files are zipped into a timestamped archive file in the Log directory. Max 10 zip archives are retained.
result: [pending]

### 5. Console Logging in SSE Mode
expected: Start the server in SSE transport mode. Log messages appear on the console/terminal with readable formatting (no FlaUI.Mcp namespace prefix clutter). Timestamps, log levels, and messages are visible.
result: issue
reported: "Server crashed before reaching SSE transport startup due to FirewallManager encoding exception. After fix: console logging works — timestamps, log levels, callsites, and messages visible. Minor issue: `find: 'Aktiviert'` warning from FirewallManager German locale parsing leaks to stderr."
severity: minor
fix-applied: "FirewallManager wrapped in try-catch. Console logging confirmed working after fix."

### 6. Clean stdio Output
expected: Start the server in stdio transport mode. No log messages appear on stdout — stdout remains clean for JSON-RPC protocol communication. Logs go only to files.
result: [pending]

## Summary

total: 6
passed: 0
issues: 3
pending: 3
skipped: 0

## Gaps

- truth: "Server boots without errors on cold start"
  status: failed
  reason: "User reported: the server is not booting without --transport parameter"
  severity: major
  test: 1
  root_cause: "FirewallManager.CheckRule() throws NotSupportedException for encoding 850 on German Windows. .NET 8 does not include CodePages by default. Crash at Program.cs:76, before main try-catch block."
  artifacts:
    - path: "src/FlaUI.Mcp/Program.cs"
      issue: "Missing CodePages encoding provider registration; FirewallManager call not wrapped in try-catch"
    - path: "src/FlaUI.Mcp/FlaUI.Mcp.csproj"
      issue: "Missing System.Text.Encoding.CodePages NuGet package"
  missing:
    - "Register CodePagesEncodingProvider.Instance before FirewallManager calls"
    - "Wrap FirewallManager in try-catch so firewall failure doesn't prevent server startup"
  fix-applied: true

- truth: "Log directory and Error.log exist after server start"
  status: failed
  reason: "User reported: the log directory does not exist, neither a logfile"
  severity: major
  test: 2
  root_cause: "Same crash as Test 1 prevented LogArchiver.CleanOldLogfiles from running. After fix, directory is created but Error.log only appears on first error (NLog lazy file creation)."
  artifacts:
    - path: "src/FlaUI.Mcp/Program.cs"
      issue: "Crash before logging setup"
  missing: []
  fix-applied: true

- truth: "Console shows log messages in SSE mode with readable formatting"
  status: failed
  reason: "Server crashed before reaching SSE startup. After fix: works, but FirewallManager German locale parsing leaks 'find: Aktiviert' to stderr."
  severity: minor
  test: 5
  root_cause: "Same crash as Test 1. After fix, minor stderr noise from FirewallManager netsh German output parsing."
  artifacts:
    - path: "src/FlaUI.Mcp/Program.cs"
      issue: "FirewallManager stderr leak"
  missing: []
  fix-applied: true
