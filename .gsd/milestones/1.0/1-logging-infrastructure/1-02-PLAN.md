---
phase: 1-logging-infrastructure
plan: 02
type: execute
wave: 2
depends_on:
  - 1-01
files_modified:
  - src/FlaUI.Mcp/Program.cs
  - src/FlaUI.Mcp/Mcp/McpServer.cs
  - src/FlaUI.Mcp/Mcp/SseTransport.cs
autonomous: true
requirements:
  - LOG-02
  - LOG-03
  - LOG-07
  - LOG-08
  - LOG-11
  - LOG-12

must_haves:
  truths:
    - "Error.log is always created when the server runs"
    - "Debug.log is created only when -debug or -d flag is passed"
    - "Framework noise from System.* and Microsoft.* does not appear below Warn level"
    - "ASP.NET Core logging is routed through NLog in SSE mode"
    - "Each class uses a static NLog Logger field"
    - "LogManager.Shutdown() is called in the finally block on exit"
    - "All Console.Error.WriteLine calls are replaced with NLog logger calls"
  artifacts:
    - path: "src/FlaUI.Mcp/Program.cs"
      provides: "Startup sequence: CleanOldLogfiles -> ConfigureLogging -> run server, with -debug flag and LogManager.Shutdown()"
      contains: "CleanOldLogfiles"
    - path: "src/FlaUI.Mcp/Mcp/McpServer.cs"
      provides: "NLog logger replacing Console.Error.WriteLine"
      contains: "private static readonly Logger Logger"
    - path: "src/FlaUI.Mcp/Mcp/SseTransport.cs"
      provides: "NLog logger + ASP.NET Core NLog integration"
      contains: "ClearProviders"
  key_links:
    - from: "src/FlaUI.Mcp/Program.cs"
      to: "src/FlaUI.Mcp/Logging/LogArchiver.cs"
      via: "LogArchiver.CleanOldLogfiles() call before ConfigureLogging"
      pattern: "LogArchiver\\.CleanOldLogfiles"
    - from: "src/FlaUI.Mcp/Program.cs"
      to: "src/FlaUI.Mcp/Logging/LoggingConfig.cs"
      via: "LoggingConfig.ConfigureLogging() call"
      pattern: "LoggingConfig\\.ConfigureLogging"
    - from: "src/FlaUI.Mcp/Mcp/SseTransport.cs"
      to: "NLog.Web.AspNetCore"
      via: "builder.Host.UseNLog()"
      pattern: "UseNLog"
    - from: "src/FlaUI.Mcp/Program.cs"
      to: "NLog"
      via: "LogManager.Shutdown() in finally block"
      pattern: "LogManager\\.Shutdown"
---

<objective>
Wire NLog into the existing codebase: add -debug flag parsing, call CleanOldLogfiles/ConfigureLogging at startup, integrate NLog with ASP.NET Core, replace all Console.Error.WriteLine calls with NLog loggers, and add LogManager.Shutdown() to the finally block.

Purpose: Completes the logging infrastructure by connecting the foundation (Plan 01) to the actual application code. After this plan, the server produces structured log files.
Output: Modified Program.cs, McpServer.cs, and SseTransport.cs with full NLog integration.
</objective>

<execution_context>
@C:/Users/uhgde/.claude/get-shit-done/workflows/execute-plan.md
@C:/Users/uhgde/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.gsd/PROJECT.md
@.gsd/ROADMAP.md
@.gsd/STATE.md
@.gsd/phases/1-logging-infrastructure/1-CONTEXT.md
@.gsd/phases/1-logging-infrastructure/1-01-SUMMARY.md

<interfaces>
<!-- From Plan 01 outputs — the executor needs these contracts -->

From src/FlaUI.Mcp/Logging/LoggingConfig.cs:
```csharp
namespace FlaUI.Mcp.Logging;

public static class LoggingConfig
{
    public static string LogDirectory => Path.Combine(AppContext.BaseDirectory, "Log");
    public static void ConfigureLogging(bool debug, string logDirectory, bool enableConsoleTarget);
}
```

From src/FlaUI.Mcp/Logging/LogArchiver.cs:
```csharp
namespace FlaUI.Mcp.Logging;

public static class LogArchiver
{
    public static void CleanOldLogfiles(string logDirectory);
}
```
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Wire logging startup into Program.cs with -debug flag</name>
  <files>src/FlaUI.Mcp/Program.cs</files>
  <read_first>
    - src/FlaUI.Mcp/Program.cs (current full content — top-level statements, CLI parsing, try/finally block)
    - src/FlaUI.Mcp/Logging/LoggingConfig.cs (confirm method signature and LogDirectory property)
    - src/FlaUI.Mcp/Logging/LogArchiver.cs (confirm method signature)
    - ~/.claude/knowledge/nlog-conventions.md (startup/shutdown order, debug parameter detection)
  </read_first>
  <action>
**Modify `src/FlaUI.Mcp/Program.cs` with these changes:**

1. **Add usings at top:**
   ```csharp
   using NLog;
   using FlaUI.Mcp.Logging;
   ```

2. **Add debug flag variable** after `var port = 8080;`:
   ```csharp
   var debug = false;
   ```

3. **Add case to CLI parsing switch** (inside the `for` loop, after the `--port` case):
   ```csharp
   case "-debug":
   case "-d":
       debug = true;
       break;
   ```

4. **Add logging startup BEFORE `var sessionManager = new SessionManager();`** (after CLI parsing, before any service creation):
   ```csharp
   // Logging startup sequence
   var logDirectory = LoggingConfig.LogDirectory;
   LogArchiver.CleanOldLogfiles(logDirectory);
   LoggingConfig.ConfigureLogging(debug, logDirectory, enableConsoleTarget: transport == "sse");
   var logger = LogManager.GetCurrentClassLogger();
   logger.Info("FlaUI-MCP starting (transport={transport}, debug={debug})", transport, debug);
   ```

5. **Add `LogManager.Shutdown()` to the finally block**, BEFORE `sessionManager.Dispose()`:
   ```csharp
   finally
   {
       LogManager.Shutdown();
       sessionManager.Dispose();
   }
   ```

6. **Remove the `Console.SetError` redirect** from `McpServer.RunAsync` is not in Program.cs — but note: do NOT add any Console.Error.WriteLine to Program.cs. All diagnostic output goes through the logger.

The startup order is: parse CLI args -> CleanOldLogfiles -> ConfigureLogging -> create services -> run server. The shutdown order is: LogManager.Shutdown() -> sessionManager.Dispose().
  </action>
  <verify>
    <automated>cd src/FlaUI.Mcp &amp;&amp; dotnet build --no-restore 2>&amp;1 | tail -5</automated>
  </verify>
  <acceptance_criteria>
    - Program.cs contains `using NLog;`
    - Program.cs contains `using FlaUI.Mcp.Logging;`
    - Program.cs contains `var debug = false;`
    - Program.cs contains `case "-debug":` and `case "-d":`
    - Program.cs contains `LogArchiver.CleanOldLogfiles(logDirectory);`
    - Program.cs contains `LoggingConfig.ConfigureLogging(debug, logDirectory, enableConsoleTarget: transport == "sse")`
    - Program.cs contains `LogManager.GetCurrentClassLogger()`
    - Program.cs contains `LogManager.Shutdown()` inside the finally block
    - Program.cs contains `LogManager.Shutdown()` appearing BEFORE `sessionManager.Dispose()`
    - Program.cs does NOT contain `Console.Error.WriteLine`
    - `dotnet build` succeeds
  </acceptance_criteria>
  <done>Program.cs has complete logging startup sequence (archive, configure, logger), -debug flag parsing, and LogManager.Shutdown() in finally block</done>
</task>

<task type="auto">
  <name>Task 2: Replace Console.Error with NLog in McpServer and SseTransport, add ASP.NET Core integration</name>
  <files>src/FlaUI.Mcp/Mcp/McpServer.cs, src/FlaUI.Mcp/Mcp/SseTransport.cs</files>
  <read_first>
    - src/FlaUI.Mcp/Mcp/McpServer.cs (current content — find Console.Error.WriteLine on line 48)
    - src/FlaUI.Mcp/Mcp/SseTransport.cs (current content — find Console.Error.WriteLine on lines 113-115, find builder creation on line 29)
    - ~/.claude/knowledge/nlog-conventions.md (static logger per class pattern section 8, ASP.NET Core integration section 9)
  </read_first>
  <action>
**Modify `src/FlaUI.Mcp/Mcp/McpServer.cs`:**

1. Add `using NLog;` at top.

2. Add static logger field as first line inside the class body:
   ```csharp
   private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
   ```

3. Replace line 48 (`Console.Error.WriteLine($"Error processing request: {ex.Message}");`) with:
   ```csharp
   Logger.Error(ex, "Error processing request");
   ```

4. Remove line 26 (`Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });`) — NLog handles all logging now, no need to redirect stderr.

**Modify `src/FlaUI.Mcp/Mcp/SseTransport.cs`:**

1. Add these usings at top:
   ```csharp
   using NLog;
   using NLog.Web;
   using Microsoft.Extensions.Logging;
   ```

2. Add static logger field as first line inside the class body:
   ```csharp
   private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
   ```

3. **Add ASP.NET Core NLog integration** after `var builder = WebApplication.CreateBuilder();` (line 29), before `builder.WebHost.UseUrls(...)`:
   ```csharp
   builder.Logging.ClearProviders();
   builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
   builder.Host.UseNLog();
   ```

4. Replace the three `Console.Error.WriteLine` calls (lines 113-115) with NLog:
   ```csharp
   Logger.Info("FlaUI-MCP SSE server listening on http://0.0.0.0:{Port}", _port);
   Logger.Info("  SSE endpoint:     GET  http://localhost:{Port}/sse", _port);
   Logger.Info("  Message endpoint:  POST http://localhost:{Port}/messages?sessionId=<id>", _port);
   ```
   Note: Use NLog structured logging with `{Port}` placeholder, not string interpolation `$"..."`.

After these changes, **zero** `Console.Error.WriteLine` calls should remain in the entire `src/` directory.
  </action>
  <verify>
    <automated>cd src/FlaUI.Mcp &amp;&amp; dotnet build --no-restore 2>&amp;1 | tail -5 &amp;&amp; grep -r "Console.Error.WriteLine" src/ &amp;&amp; echo "FAIL: Console.Error.WriteLine still exists" || echo "PASS: No Console.Error.WriteLine remaining"</automated>
  </verify>
  <acceptance_criteria>
    - McpServer.cs contains `using NLog;`
    - McpServer.cs contains `private static readonly Logger Logger = LogManager.GetCurrentClassLogger();`
    - McpServer.cs contains `Logger.Error(ex,` replacing the Console.Error.WriteLine
    - McpServer.cs does NOT contain `Console.Error.WriteLine`
    - McpServer.cs does NOT contain `Console.SetError`
    - SseTransport.cs contains `using NLog;`
    - SseTransport.cs contains `using NLog.Web;`
    - SseTransport.cs contains `private static readonly Logger Logger = LogManager.GetCurrentClassLogger();`
    - SseTransport.cs contains `builder.Logging.ClearProviders();`
    - SseTransport.cs contains `builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);`
    - SseTransport.cs contains `builder.Host.UseNLog();`
    - SseTransport.cs contains `Logger.Info(` replacing the Console.Error.WriteLine calls
    - SseTransport.cs does NOT contain `Console.Error.WriteLine`
    - `dotnet build` succeeds
    - grep for `Console.Error.WriteLine` across src/ returns zero matches
  </acceptance_criteria>
  <done>All Console.Error.WriteLine calls replaced with NLog loggers, ASP.NET Core integrated via ClearProviders+UseNLog, static Logger field in both classes, zero Console.Error.WriteLine remaining</done>
</task>

</tasks>

<verification>
- `dotnet build src/FlaUI.Mcp/` succeeds
- `grep -r "Console.Error.WriteLine" src/` returns zero matches
- `grep -r "LogManager.GetCurrentClassLogger" src/` returns matches in McpServer.cs, SseTransport.cs, and Program.cs
- `grep "LogManager.Shutdown" src/FlaUI.Mcp/Program.cs` returns a match
- `grep "CleanOldLogfiles" src/FlaUI.Mcp/Program.cs` returns a match
- `grep "ClearProviders" src/FlaUI.Mcp/Mcp/SseTransport.cs` returns a match
- `grep "UseNLog" src/FlaUI.Mcp/Mcp/SseTransport.cs` returns a match
- `grep "\-debug" src/FlaUI.Mcp/Program.cs` returns a match
</verification>

<success_criteria>
- Server compiles and builds successfully with full NLog integration
- Program.cs follows startup order: CleanOldLogfiles -> ConfigureLogging -> create services -> run
- -debug/-d flag enables Debug.log target
- ASP.NET Core routes logging through NLog in SSE mode
- Every class with logging uses static Logger field pattern
- LogManager.Shutdown() called in finally block before dispose
- Zero Console.Error.WriteLine calls remain in codebase
</success_criteria>

<output>
After completion, create `.gsd/phases/1-logging-infrastructure/1-02-SUMMARY.md`
</output>
