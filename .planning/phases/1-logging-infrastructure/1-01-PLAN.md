---
phase: 1-logging-infrastructure
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - src/FlaUI.Mcp/FlaUI.Mcp.csproj
  - src/FlaUI.Mcp/Logging/LoggingConfig.cs
  - src/FlaUI.Mcp/Logging/LogArchiver.cs
autonomous: true
requirements:
  - LOG-01
  - LOG-04
  - LOG-05
  - LOG-06
  - LOG-09
  - LOG-10

must_haves:
  truths:
    - "NLog is configured programmatically with no XML config files"
    - "Error.log target exists at Error level with async writes"
    - "Debug.log target exists at Debug level with async writes, conditionally activated"
    - "Console target uses shortened layout with namespace stripping"
    - "File layout includes longdate, level, callsite, message, exception"
    - "On startup, existing .log files are zipped into a timestamped archive"
    - "Archives beyond 10 are deleted (oldest first)"
  artifacts:
    - path: "src/FlaUI.Mcp/Logging/LoggingConfig.cs"
      provides: "Programmatic NLog configuration with file and console targets"
      exports: ["ConfigureLogging"]
      contains: "LogManager.Setup().LoadConfiguration"
    - path: "src/FlaUI.Mcp/Logging/LogArchiver.cs"
      provides: "Log archive on startup and rotation"
      exports: ["CleanOldLogfiles"]
      contains: "ZipFile"
    - path: "src/FlaUI.Mcp/FlaUI.Mcp.csproj"
      provides: "NLog NuGet package references"
      contains: "NLog.Web.AspNetCore"
  key_links:
    - from: "src/FlaUI.Mcp/Logging/LoggingConfig.cs"
      to: "NLog"
      via: "LogManager.Setup().LoadConfiguration fluent API"
      pattern: "LogManager\\.Setup\\(\\)\\.LoadConfiguration"
    - from: "src/FlaUI.Mcp/Logging/LogArchiver.cs"
      to: "{AppBaseDirectory}\\Log"
      via: "System.IO.Compression.ZipFile"
      pattern: "ZipFile\\.CreateFromDirectory|ZipFile\\.Create"
---

<objective>
Create the NLog logging infrastructure: programmatic configuration with file/console targets, async writes, and log archive rotation on startup.

Purpose: Establishes the logging foundation that all subsequent integration depends on. After this plan, the project has a `LoggingConfig.ConfigureLogging()` and `LogArchiver.CleanOldLogfiles()` ready to be called from Program.cs.
Output: Two new files in `src/FlaUI.Mcp/Logging/`, NLog NuGet packages added to csproj.
</objective>

<execution_context>
@C:/Users/uhgde/.claude/get-shit-done/workflows/execute-plan.md
@C:/Users/uhgde/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/STATE.md
@.planning/phases/1-logging-infrastructure/1-CONTEXT.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Add NLog packages and create LogArchiver</name>
  <files>src/FlaUI.Mcp/FlaUI.Mcp.csproj, src/FlaUI.Mcp/Logging/LogArchiver.cs</files>
  <read_first>
    - src/FlaUI.Mcp/FlaUI.Mcp.csproj (current package references and project structure)
    - ~/.claude/knowledge/nlog-conventions.md (archive-on-startup pattern, section 6)
  </read_first>
  <action>
**Step 1: Add NLog NuGet packages to csproj.**

Add these PackageReferences to `src/FlaUI.Mcp/FlaUI.Mcp.csproj` inside an `<ItemGroup>`:
```xml
<PackageReference Include="NLog" Version="5.*" />
<PackageReference Include="NLog.Web.AspNetCore" Version="5.*" />
```

Run `dotnet restore` in `src/FlaUI.Mcp/` to confirm packages resolve.

**Step 2: Create `src/FlaUI.Mcp/Logging/LogArchiver.cs`.**

Create a static class `LogArchiver` in namespace `FlaUI.Mcp.Logging` with a single public static method:

```csharp
public static void CleanOldLogfiles(string logDirectory)
```

Implementation:
1. If `logDirectory` does not exist, call `Directory.CreateDirectory(logDirectory)` and return (nothing to archive).
2. Get all `.zip` files in `logDirectory`, order by `LastWriteTime` descending. If count exceeds 10, delete the oldest files beyond 10 (keep newest 10).
3. Get all `.log` files in `logDirectory`. If any exist:
   a. Create a temp directory inside `logDirectory` named `_archive_temp`.
   b. Move all `.log` files into the temp directory.
   c. Create zip: `ZipFile.CreateFromDirectory(tempDir, Path.Combine(logDirectory, $"Logs-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.zip"))`.
   d. Delete the temp directory recursively.
4. Use `System.IO.Compression` namespace. Add `using System.IO.Compression;` at top.

Important: Do NOT use NLog built-in archiving. This is a manual pre-NLog step per convention.
  </action>
  <verify>
    <automated>cd src/FlaUI.Mcp &amp;&amp; dotnet restore &amp;&amp; dotnet build --no-restore 2>&amp;1 | tail -5</automated>
  </verify>
  <acceptance_criteria>
    - src/FlaUI.Mcp/FlaUI.Mcp.csproj contains `PackageReference Include="NLog"`
    - src/FlaUI.Mcp/FlaUI.Mcp.csproj contains `PackageReference Include="NLog.Web.AspNetCore"`
    - src/FlaUI.Mcp/Logging/LogArchiver.cs exists
    - LogArchiver.cs contains `public static void CleanOldLogfiles(string logDirectory)`
    - LogArchiver.cs contains `ZipFile.CreateFromDirectory`
    - LogArchiver.cs contains `Directory.GetFiles(logDirectory, "*.zip")`  or equivalent
    - LogArchiver.cs contains the max 10 zip rotation logic
    - LogArchiver.cs contains `DateTime.Now:yyyy-MM-dd_HH-mm-ss` in the zip filename format
    - `dotnet build` succeeds with no errors
  </acceptance_criteria>
  <done>NLog packages restore successfully, LogArchiver compiles, archive method handles zip creation and 10-file rotation</done>
</task>

<task type="auto">
  <name>Task 2: Create LoggingConfig with programmatic NLog setup</name>
  <files>src/FlaUI.Mcp/Logging/LoggingConfig.cs</files>
  <read_first>
    - ~/.claude/knowledge/nlog-conventions.md (programmatic config pattern, file layout, console layout, framework suppression, async writes)
    - src/FlaUI.Mcp/Logging/LogArchiver.cs (just created — confirm namespace)
    - .planning/phases/1-logging-infrastructure/1-CONTEXT.md (console target behavior: SSE only, disabled in stdio)
  </read_first>
  <action>
**Create `src/FlaUI.Mcp/Logging/LoggingConfig.cs`.**

Static class `LoggingConfig` in namespace `FlaUI.Mcp.Logging` with:

```csharp
public static void ConfigureLogging(bool debug, string logDirectory, bool enableConsoleTarget)
```

Implementation using `LogManager.Setup().LoadConfiguration(c => { ... })`:

1. **Log directory constant:**
   ```csharp
   public static string LogDirectory => Path.Combine(AppContext.BaseDirectory, "Log");
   ```

2. **File layout** (exact string):
   ```
   ${longdate} | ${pad:padding=5:inner=${level:uppercase=true}} | ${callsite} | ${message} ${exception:format=tostring}
   ```

3. **Console layout** (exact string, with FlaUI.Mcp namespace stripping):
   ```
   ${time} | ${pad:padding=-5:inner=${level:uppercase=true}} | ${pad:padding=-80:inner=${replace:inner=${callsite}:searchFor=FlaUI\\.Mcp\\.:replaceWith=:regex=true}} | ${message} ${exception:format=tostring}
   ```

4. **Error.log target** — always active:
   ```csharp
   var errorTarget = new NLog.Targets.FileTarget("errorFile")
   {
       FileName = Path.Combine(logDirectory, "Error.log"),
       Layout = fileLayout
   };
   c.ForLogger().FilterMinLevel(NLog.LogLevel.Error).WriteTo(errorTarget).WithAsync();
   ```

5. **Debug.log target** — only when `debug == true`:
   ```csharp
   if (debug)
   {
       var debugTarget = new NLog.Targets.FileTarget("debugFile")
       {
           FileName = Path.Combine(logDirectory, "Debug.log"),
           Layout = fileLayout
       };
       c.ForLogger().FilterMinLevel(NLog.LogLevel.Debug).WriteTo(debugTarget).WithAsync();
   }
   ```

6. **Console target** — only when `enableConsoleTarget == true` (SSE mode):
   ```csharp
   if (enableConsoleTarget)
   {
       var consoleTarget = new NLog.Targets.ConsoleTarget("console")
       {
           Layout = consoleLayout
       };
       var consoleMinLevel = debug ? NLog.LogLevel.Debug : NLog.LogLevel.Info;
       c.ForLogger().FilterMinLevel(consoleMinLevel).WriteTo(consoleTarget).WithAsync();
   }
   ```

7. **Framework noise suppression:**
   ```csharp
   c.ForLogger("System.*").WriteToNil(NLog.LogLevel.Warn);
   c.ForLogger("Microsoft.*").WriteToNil(NLog.LogLevel.Warn);
   c.ForLogger("Microsoft.Hosting.Lifetime*").WriteToNil(NLog.LogLevel.Info);
   ```

Required usings: `NLog`, `NLog.Config`, `NLog.Targets`.

The `enableConsoleTarget` parameter allows Program.cs to pass `transport == "sse"` to conditionally enable console output (stdio mode must not pollute stdout).
  </action>
  <verify>
    <automated>cd src/FlaUI.Mcp &amp;&amp; dotnet build --no-restore 2>&amp;1 | tail -5</automated>
  </verify>
  <acceptance_criteria>
    - src/FlaUI.Mcp/Logging/LoggingConfig.cs exists
    - LoggingConfig.cs contains `public static void ConfigureLogging(bool debug, string logDirectory, bool enableConsoleTarget)`
    - LoggingConfig.cs contains `public static string LogDirectory`
    - LoggingConfig.cs contains `LogManager.Setup().LoadConfiguration`
    - LoggingConfig.cs contains `${longdate}` in file layout string
    - LoggingConfig.cs contains `${callsite}` in file layout string
    - LoggingConfig.cs contains `${exception:format=tostring}` in layout strings
    - LoggingConfig.cs contains `FlaUI\\.Mcp\\.` in console layout (namespace stripping)
    - LoggingConfig.cs contains `FileTarget("errorFile")` or equivalent error target
    - LoggingConfig.cs contains `if (debug)` guarding debug target creation
    - LoggingConfig.cs contains `if (enableConsoleTarget)` guarding console target
    - LoggingConfig.cs contains `WriteToNil(NLog.LogLevel.Warn)` for System.* and Microsoft.*
    - LoggingConfig.cs contains `.WithAsync()` on all target rules
    - `dotnet build` succeeds with no errors
  </acceptance_criteria>
  <done>LoggingConfig compiles with programmatic NLog setup, Error.log always active, Debug.log conditional, console conditional, framework noise suppressed, all targets async</done>
</task>

</tasks>

<verification>
- `dotnet build src/FlaUI.Mcp/` succeeds
- `src/FlaUI.Mcp/Logging/LogArchiver.cs` exists with `CleanOldLogfiles` method
- `src/FlaUI.Mcp/Logging/LoggingConfig.cs` exists with `ConfigureLogging` method
- NLog and NLog.Web.AspNetCore packages present in csproj
- No NLog.config XML file exists anywhere in the project
</verification>

<success_criteria>
- Project compiles with NLog packages
- LogArchiver.CleanOldLogfiles() handles zip creation and 10-file rotation
- LoggingConfig.ConfigureLogging() sets up Error.log (always), Debug.log (conditional), console (conditional)
- All file targets use async writes
- Framework noise suppression configured for System.* and Microsoft.*
- File layout includes longdate, level, callsite, message, exception
- Console layout strips FlaUI.Mcp namespace prefix
</success_criteria>

<output>
After completion, create `.planning/phases/1-logging-infrastructure/1-01-SUMMARY.md`
</output>
