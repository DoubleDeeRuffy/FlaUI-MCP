---
phase: 3-task-scheduler-startup
plan: 02
type: execute
wave: 2
depends_on: [3-01]
files_modified: [src/FlaUI.Mcp/Program.cs]
autonomous: true
requirements: [TSK-02, TSK-03]
must_haves:
  truths:

    - --task / -install / -i registers a LogonTrigger task via WinTaskSchedulerManager.CreateOnLogon (not raw schtasks.exe)
    - --removetask / -uninstall / -u deletes via WinTaskSchedulerManager.Delete (idempotent)
    - --task auto-uninstalls legacy v0.x FlaUI-MCP Windows Service before CreateOnLogon (D-1)
    - raw schtasks.exe Process.Start blocks fully removed from Program.cs
    - --removetask does NOT touch the Windows Service (D-1 reverse rule)
  artifacts:

    - [object Object]
  key_links:

    - [object Object]
    - [object Object]

---

<objective>
Replace the raw `schtasks.exe` Process.Start blocks (current Program.cs lines 165-235) with `Skoosoft.Windows.Manager.WinTaskSchedulerManager.CreateOnLogon` and `.Delete` (TSK-02, TSK-03). Implement D-1 auto-migration: `--task` (and `-install`/`-i` aliases per D-2) auto-uninstalls any pre-existing v0.x `FlaUI-MCP` Windows Service BEFORE creating the scheduled task, idempotently. `--removetask` (and `-uninstall`/`-u` aliases) only removes the scheduled task — does NOT touch the service.

Purpose: Eliminate locale-fragile schtasks shell-out. Get clean idempotent task management via the Skoosoft wrapper. Enforce migration sequencing so the user can never end up with both service + task registered.

Output: Program.cs with `WinTaskSchedulerManager.CreateOnLogon`/`Delete` calls; D-1 service-uninstall sequenced before CreateOnLogon; `-install`/`-i` aliases route to the same code path as `--task`; `-uninstall`/`-u` aliases route to the same code path as `--removetask`; raw schtasks.exe Process.Start blocks deleted; `dotnet build -c Release` succeeds.
</objective>

<execution_context>
@$HOME/.claude-account2/get-shit-done/workflows/execute-plan.md
@$HOME/.claude-account2/get-shit-done/templates/summary.md
</execution_context>

<context>
@.gsd/PROJECT.md
@.gsd/milestones/1.0/3-task-scheduler-startup/3-CONTEXT.md
@.gsd/milestones/1.0/3-task-scheduler-startup/3-RESEARCH.md
@src/FlaUI.Mcp/Program.cs

<interfaces>
Skoosoft API surface (verify via goToDefinition post-restore):

```csharp
namespace Skoosoft.Windows.Manager;
public static class WinTaskSchedulerManager
{
    public static void CreateOnLogon(string name, string description, string execFilePath, string execArguments);
    public static void Delete(string name);  // idempotent — no-op if absent
}
public static class ServiceManager
{
    public static bool DoesServiceExist(string name);
    public static void Install(string name, string exePath, bool silent);
    public static void Uninstall(string name, bool silent);
}
```

Current Program.cs constants (line 12-13):

```csharp
const string ServiceName = "FlaUI-MCP";
const string FirewallRuleName = "FlaUI-MCP";
```

Current Program.cs flag locals (lines 17-22): `install`, `uninstall`, `task`, `removeTask` — all bool. `silent`, `debug`, `console` also present.

Current ServiceManager.Install/Uninstall calls (lines 150-163) — KEEP for now; this plan deprecates `install` semantics by repurposing the flag, not by deleting the calls. The `install` branch transforms from "install service" to "install task" (D-2 alias). Same for `uninstall`.
</interfaces>
</context>

<tasks>

<task>
  <name>Task 1: Replace schtasks blocks with Skoosoft API + implement D-1/D-2</name>
  <files>
    <file>src/FlaUI.Mcp/Program.cs</file>
  </files>
  <action>
  Make the following precise changes to `src/FlaUI.Mcp/Program.cs`:

**Change 1 (line ~166):** Add a `const string TaskName = "FlaUI-MCP";` near the existing `ServiceName` constant (line 12) so it's a top-level constant, not buried mid-file. Move the existing inline `const string TaskName = "FlaUI-MCP";` (currently at line 166) up to the constants block.

**Change 2 — replace `if (install)` block (lines 150-157), `if (uninstall)` block (lines 159-163), `if (task)` block (lines 167-205), and `if (removeTask)` block (lines 207-235) with this consolidated structure:**

```csharp
// === 9. Register / unregister scheduled task (TSK-02, TSK-03; aliases per D-2) ===
// -install/-i alias --task; -uninstall/-u alias --removetask
if (task || install)
{
    // D-1 auto-migration: silently uninstall any legacy FlaUI-MCP Windows Service
    // before creating the scheduled task. Idempotent — no-op if absent.
    if (ServiceManager.DoesServiceExist(ServiceName))
    {
        logger?.Info("Detected legacy FlaUI-MCP Windows Service \u2014 uninstalling before creating scheduled task");
        try
        {
            ServiceManager.Uninstall(ServiceName, silent: true);
        }
        catch (Exception ex)
        {
            logger?.Error(ex, "Failed to auto-uninstall legacy service during --task migration");
            Console.WriteLine($"WARNING: Failed to auto-uninstall legacy service: {ex.Message}");
            Environment.Exit(1);
        }
    }

    // Defensive idempotency: delete pre-existing task with same name (Open Question 1 in RESEARCH)
    try { WinTaskSchedulerManager.Delete(TaskName); } catch { /* idempotent — ignore */ }

    try
    {
        WinTaskSchedulerManager.CreateOnLogon(
            name: TaskName,
            description: "Starts FlaUI-MCP at user logon (runs in user desktop session)",
            execFilePath: exeFilePath,
            execArguments: "");
        Console.WriteLine($"Scheduled task '{TaskName}' registered. Will start at next user logon.");
        logger?.Info("Scheduled task '{Task}' registered", TaskName);
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        logger?.Error(ex, "Failed to create scheduled task");
        Console.WriteLine($"Failed to create scheduled task: {ex.Message}");
        Environment.Exit(1);
    }
}

if (removeTask || uninstall)
{
    // D-1 reverse rule: do NOT touch the service. The service should be gone
    // already from a prior --task call, or never existed. Only remove the task.
    try
    {
        WinTaskSchedulerManager.Delete(TaskName);  // idempotent
        Console.WriteLine($"Scheduled task '{TaskName}' removed.");
        logger?.Info("Scheduled task '{Task}' removed", TaskName);
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        logger?.Error(ex, "Failed to remove scheduled task");
        Console.WriteLine($"Failed to remove scheduled task: {ex.Message}");
        Environment.Exit(1);
    }
}
```

**Change 3:** Delete the now-unused firewall-rule-during-install block from the old `if (install)` body. The firewall rule is already created earlier (lines 113-126) for SSE transport, so it's already covered.

**Change 4:** Remove the unused `using System.ServiceProcess;` directive only if NO other code still uses ServiceController. Check: the existing service-stop block (lines 128-147) uses `ServiceController` directly. KEEP that block intact (it's idempotent if no service exists, per Pitfall 5 — leaving it doesn't break anything; removal is Plan 04's potential cleanup but not in this plan's scope). Therefore KEEP `using System.ServiceProcess;`.

**Important guards:**

- Do NOT call `ServiceManager.Install` anywhere — `install` flag now means "install task", not "install service".
- Do NOT call `ServiceManager.Uninstall` from the `removeTask`/`uninstall` branch — D-1 reverse rule.
- The `if (install)` and `if (uninstall)` blocks (lines 150-163) must be FULLY DELETED; the new consolidated `if (task || install)` and `if (removeTask || uninstall)` blocks replace them entirely.
- The new task/removetask blocks must execute BEFORE the existing `if (install)` and `if (uninstall)` were placed (around line 150, after the service-stop block at line 128-147), so the migration order is: stop-running-service (line 128-147 idempotent, leave as-is) → if (task || install) → if (removeTask || uninstall) → main webapp boot.

After the edit, run `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Release` — must compile clean.

Note: the German em-dash in the log message is encoded as `\u2014` (Unicode escape) per CLAUDE.md German-umlaut guidance — but since this string is English, the em-dash is purely stylistic. If the executor prefers ASCII `--`, that's also fine. Do NOT use raw bytes that could be Mojibake'd.
  </action>
  <verify>
  <verify>
  <automated>dotnet build src\FlaUI.Mcp\FlaUI.Mcp.csproj -c Release && findstr /C:"WinTaskSchedulerManager.CreateOnLogon" /C:"WinTaskSchedulerManager.Delete" src\FlaUI.Mcp\Program.cs && (findstr /C:"schtasks" src\FlaUI.Mcp\Program.cs && exit 1 || exit 0)</automated>
</verify>

Build succeeds. Both Skoosoft API calls present. Raw `schtasks` string fully removed.
  </verify>
  <done>Build green in Release. CreateOnLogon and Delete calls present. No `schtasks` string in Program.cs. ServiceManager.DoesServiceExist + ServiceManager.Uninstall called inside the `if (task || install)` branch BEFORE CreateOnLogon. `if (removeTask || uninstall)` branch contains only WinTaskSchedulerManager.Delete (no ServiceManager.* calls).</done>
</task>

</tasks>

<verification>
Build succeeds. The 4 static greps above all green. Manual code review confirms D-1 sequencing (service-uninstall BEFORE CreateOnLogon) and D-1 reverse rule (no service touch in removetask).

Manual UAT (deferred to Plan 07): run `--task` on a clean machine, verify task appears via `schtasks /query /tn FlaUI-MCP /v /fo LIST` with InteractiveToken + Highest + Hidden. Run `--removetask` twice — both exit 0.
</verification>

<success_criteria>

- [ ] Skoosoft API used for both create and delete
- [ ] Raw schtasks.exe blocks fully deleted
- [ ] D-1 auto-migration sequenced correctly (service-uninstall before task-create)
- [ ] D-1 reverse rule honored (removetask does not touch service)
- [ ] D-2 aliases (-install/-uninstall) route to task code path

</success_criteria>

<output>
SUMMARY records: schtasks.exe shell-out replaced with WinTaskSchedulerManager. D-1 auto-migration implemented with NLog info-line on legacy service detection. D-2 aliases in place. Build green.
</output>
