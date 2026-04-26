---
phase: 3-task-scheduler-startup
plan: 01
type: execute
wave: 1
depends_on: [3-00]
files_modified: [src/FlaUI.Mcp/FlaUI.Mcp.csproj]
autonomous: true
requirements: [TSK-01, TSK-07]
must_haves:
  truths:

    - csproj has <OutputType>WinExe</OutputType>
    - csproj has no PackageReference to Microsoft.Extensions.Hosting.WindowsServices
    - Skoosoft.Windows package is referenced (precondition for Plan 02)
  artifacts:

    - [object Object]
  key_links:

    - [object Object]

---

<objective>
Surgically modify `src/FlaUI.Mcp/FlaUI.Mcp.csproj` for the WinExe subsystem switch (TSK-01) and remove the now-dead `Microsoft.Extensions.Hosting.WindowsServices` package reference (TSK-07). Verify Skoosoft.Windows reference resolves to >= 8.0.7 (precondition for Plan 02's `WinTaskSchedulerManager.CreateOnLogon` / `Delete` calls).

Purpose: Establish the build foundation. WinExe ensures no console window allocates when launched headless by Task Scheduler (TSK-01). Removing WindowsServices kills a dead reference (Phase 2 already migrated off `Host.UseWindowsService`). Skoosoft.Windows must already be present (it is — Phase 2 used `ServiceManager` from same package).

Output: csproj with `<OutputType>WinExe</OutputType>`, no `Microsoft.Extensions.Hosting.WindowsServices` line, Skoosoft.Windows reference confirmed. `dotnet restore` resolves Skoosoft.Windows to >= 8.0.7.
</objective>

<execution_context>
@$HOME/.claude-account2/get-shit-done/workflows/execute-plan.md
@$HOME/.claude-account2/get-shit-done/templates/summary.md
</execution_context>

<context>
@.gsd/PROJECT.md
@.gsd/milestones/1.0/3-task-scheduler-startup/3-RESEARCH.md
@.gsd/milestones/1.0/3-task-scheduler-startup/3-VALIDATION.md
@src/FlaUI.Mcp/FlaUI.Mcp.csproj

<interfaces>
Current csproj (relevant items):

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>          <!-- BECOMES WinExe -->
  <TargetFramework>net8.0-windows</TargetFramework>
  ...
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" Version="8.*" />  <!-- DELETE -->
  <PackageReference Include="Skoosoft.Windows" Version="*" />  <!-- KEEP -->
  ...
</ItemGroup>
```

</interfaces>
</context>

<tasks>

<task>
  <name>Task 1: Flip OutputType to WinExe and remove WindowsServices package</name>
  <files>
    <file>src/FlaUI.Mcp/FlaUI.Mcp.csproj</file>
  </files>
  <action>
  Edit `src/FlaUI.Mcp/FlaUI.Mcp.csproj` with TWO precise changes:

1. Replace `<OutputType>Exe</OutputType>` with `<OutputType>WinExe</OutputType>` (per TSK-01 and RESEARCH §Standard Stack).
2. Delete the entire line `<PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" Version="8.*" />` (per TSK-07).

Do NOT touch:

- TargetFramework (`net8.0-windows` stays)
- UseWindowsForms, ImplicitUsings, Nullable, AssemblyName, RootNamespace, Version, Authors, etc.
- Any other PackageReference (FlaUI.Core, FlaUI.UIA3, NLog, NLog.Web.AspNetCore, Skoosoft.ProcessLib, Skoosoft.Windows, System.Drawing.Common, System.Text.Encoding.CodePages, System.Text.Json)
- The CompileInnoSetup Target

Do NOT add new PackageReferences. Skoosoft.Windows is already present at `Version="*"` and floats to latest, which RESEARCH confirms resolves to >= 8.0.7 (introduced CreateOnLogon/Delete on WinTaskSchedulerManager).

After the edit, run `dotnet restore src/FlaUI.Mcp/FlaUI.Mcp.csproj` (do NOT build yet — Plan 02 hasn't replaced the schtasks block; build will fail for unrelated reasons until Plan 02 completes; restore is sufficient signal that the package set resolves).

Then verify Skoosoft.Windows resolved to >= 8.0.7 by inspecting `obj/project.assets.json`. If it resolved lower, change the PackageReference to `Version="[8.0.7,)"` and re-restore.
  </action>
  <verify>
  <verify>
  <automated>findstr /C:"<OutputType>WinExe</OutputType>" src\FlaUI.Mcp\FlaUI.Mcp.csproj && (findstr /C:"Microsoft.Extensions.Hosting.WindowsServices" src\FlaUI.Mcp\FlaUI.Mcp.csproj && exit 1) || dotnet restore src\FlaUI.Mcp\FlaUI.Mcp.csproj</automated>
</verify>

First findstr must match (WinExe present). Second findstr must NOT match (package removed). dotnet restore must succeed.
  </verify>
  <done>csproj contains `<OutputType>WinExe</OutputType>`. csproj does NOT contain `Microsoft.Extensions.Hosting.WindowsServices`. `dotnet restore` succeeds. `Skoosoft.Windows` PackageReference still present.</done>
</task>

</tasks>

<verification>
csproj passes both must-match (WinExe, Skoosoft.Windows) and must-NOT-match (WindowsServices) static checks. Restore succeeds, package versions resolve correctly. Build is NOT yet expected to succeed because Plan 02 hasn't replaced the raw schtasks block which references types that may shift; that's fine — Plan 02 makes build green.
</verification>

<success_criteria>

- [ ] OutputType is WinExe
- [ ] Microsoft.Extensions.Hosting.WindowsServices package removed
- [ ] Skoosoft.Windows >= 8.0.7 resolves on restore
- [ ] No other csproj content changed

</success_criteria>

<output>
SUMMARY records: csproj surgically modified for WinExe + WindowsServices removal. Skoosoft.Windows resolved to specific version (record actual). Plan 02 unblocked — can call WinTaskSchedulerManager.CreateOnLogon/Delete.
</output>
