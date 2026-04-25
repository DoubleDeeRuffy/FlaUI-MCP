---
phase: 2-service-hardening
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - src/FlaUI.Mcp/FlaUI.Mcp.csproj
  - src/FlaUI.Mcp/Program.cs
autonomous: true
requirements:
  - SVC-04
  - SVC-05

must_haves:
  truths:
    - "Running with -debug or -d sets a debug flag to true"
    - "Running with -console or -c sets a console flag to true"
    - "Running with -install or -i sets an install flag to true"
    - "Running with -uninstall or -u sets an uninstall flag to true"
    - "Running with -silent or -s sets a silent flag to true"
    - "Default transport is SSE when no --transport arg is given"
    - "Skoosoft NuGet packages are available for service and firewall operations"
  artifacts:
    - path: "src/FlaUI.Mcp/FlaUI.Mcp.csproj"
      provides: "Skoosoft NuGet package references"
      contains: "Skoosoft.ServiceHelperLib"
    - path: "src/FlaUI.Mcp/Program.cs"
      provides: "CLI flag parsing for all service flags"
      contains: "parameter.Contains"
  key_links:
    - from: "src/FlaUI.Mcp/Program.cs"
      to: "args"
      via: "string.Join parameter parsing"
      pattern: 'parameter\.Contains\("-install"\)'
---

<objective>
Add Skoosoft NuGet packages and implement unified CLI argument parsing for all service flags.

Purpose: Establishes the package dependencies and argument parsing foundation that Plan 02 builds the service lifecycle on top of.
Output: .csproj with Skoosoft packages, Program.cs with all CLI flags parsed and default transport changed to SSE.
</objective>

<execution_context>
@C:/Users/uhgde/.claude/get-shit-done/workflows/execute-plan.md
@C:/Users/uhgde/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.gsd/PROJECT.md
@.gsd/ROADMAP.md
@.gsd/STATE.md
@.gsd/phases/2-service-hardening/2-CONTEXT.md

<interfaces>
<!-- Current Program.cs uses top-level statements with a for-loop arg parser -->
<!-- Current arg parsing (lines 6-20 of Program.cs): -->
```csharp
var transport = "stdio";
var port = 8080;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--transport" when i + 1 < args.Length:
            transport = args[++i].ToLowerInvariant();
            break;
        case "--port" when i + 1 < args.Length:
            if (int.TryParse(args[++i], out var p)) port = p;
            break;
    }
}
```

<!-- Convention for CLI parsing from windows-service-conventions.md: -->
```csharp
var parameter = string.Join(" ", args).ToLower();
var silent = parameter.Contains("-silent") || parameter.Contains("-s");
var debug = parameter.Contains("-debug") || parameter.Contains("-d");
```
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Add Skoosoft NuGet packages to project</name>
  <files>src/FlaUI.Mcp/FlaUI.Mcp.csproj</files>
  <read_first>
    - src/FlaUI.Mcp/FlaUI.Mcp.csproj
  </read_first>
  <action>
Add two NuGet PackageReference entries to the existing ItemGroup that contains PackageReference elements in FlaUI.Mcp.csproj:

```xml
<PackageReference Include="Skoosoft.ServiceHelperLib" Version="*" />
<PackageReference Include="Skoosoft.Windows.Manager" Version="*" />
```

Use Version="*" to get the latest stable version. Add them in the same ItemGroup as the existing FlaUI.Core, FlaUI.UIA3, etc. references.

After adding, run `dotnet restore` to verify the packages resolve correctly.
  </action>
  <verify>
    <automated>cd "C:\Users\uhgde\source\repos\FlaUI-MCP\src\FlaUI.Mcp" && dotnet restore 2>&1 | tail -5</automated>
  </verify>
  <acceptance_criteria>
    - FlaUI.Mcp.csproj contains `PackageReference Include="Skoosoft.ServiceHelperLib"`
    - FlaUI.Mcp.csproj contains `PackageReference Include="Skoosoft.Windows.Manager"`
    - `dotnet restore` completes without errors
  </acceptance_criteria>
  <done>Both Skoosoft NuGet packages are referenced and restore successfully</done>
</task>

<task type="auto">
  <name>Task 2: Implement unified CLI argument parsing with all service flags</name>
  <files>src/FlaUI.Mcp/Program.cs</files>
  <read_first>
    - src/FlaUI.Mcp/Program.cs
    - C:\Users\uhgde\.claude\knowledge\windows-service-conventions.md
    - .gsd/phases/2-service-hardening/2-CONTEXT.md
  </read_first>
  <action>
Replace the existing for-loop arg parser in Program.cs with a two-phase approach that supports both styles:

**Phase A — Boolean flags via joined parameter string (convention pattern):**
```csharp
var parameter = string.Join(" ", args).ToLower();
var silent = parameter.Contains("-silent") || parameter.Contains("-s");
var debug = parameter.Contains("-debug") || parameter.Contains("-d");
var install = parameter.Contains("-install") || parameter.Contains("-i");
var uninstall = parameter.Contains("-uninstall") || parameter.Contains("-u");
var console = parameter.Contains("-console") || parameter.Contains("-c");
```

**Phase B — Value args via existing for-loop (keep existing pattern for --transport and --port):**
```csharp
var transport = "sse"; // Changed from "stdio" to "sse" per CONTEXT.md decision
var port = 8080;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--transport" when i + 1 < args.Length:
            transport = args[++i].ToLowerInvariant();
            break;
        case "--port" when i + 1 < args.Length:
            if (int.TryParse(args[++i], out var p)) port = p;
            break;
    }
}
```

Place boolean flag parsing BEFORE the for-loop (both remain at the top of Program.cs, before any service creation code).

The flags (install, uninstall, silent, debug, console) are just parsed here — they will be consumed by startup sequence code in Plan 02. For now they are local variables that exist but aren't used yet (compiler warnings are acceptable at this stage).

Keep everything else in Program.cs unchanged (sessionManager, toolRegistry, server creation, CTS, try/finally block).
  </action>
  <verify>
    <automated>cd "C:\Users\uhgde\source\repos\FlaUI-MCP\src\FlaUI.Mcp" && dotnet build --no-restore 2>&1 | tail -10</automated>
  </verify>
  <acceptance_criteria>
    - Program.cs contains `var parameter = string.Join(" ", args).ToLower();`
    - Program.cs contains `parameter.Contains("-install")` and `parameter.Contains("-i")`
    - Program.cs contains `parameter.Contains("-uninstall")` and `parameter.Contains("-u")`
    - Program.cs contains `parameter.Contains("-silent")` and `parameter.Contains("-s")`
    - Program.cs contains `parameter.Contains("-debug")` and `parameter.Contains("-d")`
    - Program.cs contains `parameter.Contains("-console")` and `parameter.Contains("-c")`
    - Program.cs contains `var transport = "sse";` (NOT "stdio")
    - Program.cs still contains `case "--transport"` and `case "--port"` in the for-loop
    - `dotnet build` compiles successfully (warnings OK, no errors)
  </acceptance_criteria>
  <done>All 5 boolean CLI flags parsed per convention, default transport changed to SSE, value args preserved via for-loop</done>
</task>

</tasks>

<verification>
- `dotnet build` in src/FlaUI.Mcp compiles without errors
- .csproj has both Skoosoft package references
- Program.cs has all 5 boolean flags and both value args
- Default transport is "sse"
</verification>

<success_criteria>
- Skoosoft.ServiceHelperLib and Skoosoft.Windows.Manager are in the project
- All CLI flags (-install/-i, -uninstall/-u, -silent/-s, -debug/-d, -console/-c) are parsed
- Default transport is SSE
- Project compiles
</success_criteria>

<output>
After completion, create `.gsd/phases/2-service-hardening/2-01-SUMMARY.md`
</output>
