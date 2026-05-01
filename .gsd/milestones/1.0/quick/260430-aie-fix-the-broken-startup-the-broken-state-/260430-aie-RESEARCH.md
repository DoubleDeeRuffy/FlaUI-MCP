---
name: 260430-aie-RESEARCH
quick_id: 260430-aie
researched: 2026-05-01
domain: .NET 8 Process API + Kestrel port-binding on Windows
confidence: HIGH
---

# Quick Task 260430-aie — Research

## User Constraints (from CONTEXT.md)

### Locked Decisions
- Kill stale `FlaUI.Mcp` processes on **every** startup (excluding own PID), skipping only when `--help` was passed.
- `WaitForExit(2000)` per stale process.
- ConsoleTarget gating remains `transport != "stdio"` (no change).
- Stale-kill block moves out of `if (Debugger.IsAttached)` into its own top-level block; the Debugger branch keeps `console = true; debug = true` overrides only.

### Claude's Discretion
- Helper extraction (e.g. `KillStaleInstances()`) for readability — inline if top-level layout conflicts.
- Ordering: after `--help` short-circuit, before Debugger flag-override, before logging configuration.
- Diagnostics: `Console.Error.WriteLine` inside try/catch (NLog not yet configured); failures must not abort startup.

### Deferred Ideas (OUT OF SCOPE)
- ConsoleTarget `-console`-gating refactor (Phase-3 TSK-06) — leave logic untouched.
- LoggingConfig.cs — unchanged.
- New CLI flags or behaviour.

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| FIX-01 | Stale-kill runs on every startup (Task Scheduler / headless / `-c` / Debugger) | Q1 below confirms `Process.GetProcessesByName` + PID exclusion is the correct primitive on .NET 8 |
| FIX-02 | Skip stale-kill only when `--help` is passed | Trivial — `helpRequested` is parsed at Program.cs:27 |
| FIX-03 | `WaitForExit(2000)` per process; failures non-fatal | Q1 confirms `Kill` is async and a separate `WaitForExit` is required; Q3 confirms 2 s window is ample for releasing a LISTEN socket after termination |
| FIX-04 | No regression to existing CliOptions tests | Q4 — kill code is untestable in xunit; adjacent CliOptions parsing remains pure and unaffected |

---

## 1. `Process.GetProcessesByName("FlaUI.Mcp")` — gotchas in WinExe / .NET 8

**Anchor:** `src/FlaUI.Mcp/Program.cs:69` (current call).

### 1a. Does it return the current process?
**Yes — it returns every process matching the friendly name, including the caller.** Microsoft Learn's example explicitly contrasts it with `GetCurrentProcess()` and shows `localByName` containing all matching instances ([GetProcessesByName remarks][gpbn]). The current code's PID exclusion (`p.Id != currentPid` at Program.cs:70) is **mandatory**, not defensive — without it the process kills itself on startup. **Confidence: HIGH** (official docs).

### 1b. Race condition — is try/catch sufficient?
**Yes, with the caveat that `WaitForExit` should also be inside the try/catch.** `Process.Kill()` is documented as **asynchronous** ("After calling the `Kill` method, call the `WaitForExit` method to wait for the process to exit") ([Kill remarks][kill]). On .NET 8 (Core, not Framework) calling `Kill()` on a target that has already exited is a **silent no-op** — the `InvalidOperationException` "process has already exited" only applies to .NET Framework per the Exceptions table. So the realistic exception surface for this code is:
- `Win32Exception` — could-not-terminate (rare, e.g. protected process) or, if `Kill(true)` were used, "process is terminating".
- `InvalidOperationException` — "no process associated with this Process object" (only if the `Process` instance was never properly bound, which `GetProcessesByName` does not produce).

The current `catch { }` swallows both cleanly. **Recommendation:** keep `WaitForExit(2000)` inside the try block — if the process is already gone, `WaitForExit` returns `true` immediately; if Kill threw, we skip the wait. **Confidence: HIGH** (Microsoft Learn).

### 1c. Permissions — non-admin user killing own previous instances
**Safe.** A user can always `TerminateProcess` a process they own — no admin privilege required. The Task-Scheduler-launched process (running as the same logged-on user under the `--task` design — registers at user logon, sees the desktop) will be running under the same security context as the `-c`-launched instance, so `Win32Exception` AccessDenied is **not expected** in normal operation.

**Edge case to flag (not a blocker):** if the prior instance was launched elevated (UAC) and the new one isn't, `Kill()` will throw `Win32Exception` AccessDenied. The try/catch handles it — port-bind will then fail and the user sees the bind error, which is the correct and visible failure mode. **Confidence: HIGH** (Win32 ACL semantics; Process.Kill() docs list `Win32Exception` for "could not be terminated").

### 1d. Process instance disposal
**Yes — `Process` implements `IDisposable` and should be disposed.** Each `Process` object holds an OS handle to the target process; without disposal the handle leaks until GC finalizes it. With ≤ a handful of stale instances per startup the leak is harmless, but the idiomatic pattern is:

```csharp
foreach (var stale in Process.GetProcessesByName("FlaUI.Mcp")
                             .Where(p => p.Id != currentPid))
{
    using (stale)
    {
        try
        {
            stale.Kill();
            stale.WaitForExit(2000);
        }
        catch
        {
            // Race: process exited between enumeration and kill, or AccessDenied.
        }
    }
}
```

The `using` ensures the handle is released even if the catch fires. The current code at Program.cs:69-81 omits this; the rewrite should add it. **Confidence: HIGH** (Process is `IDisposable` since .NET Framework 1.1; pattern is standard).

---

## 2. Self-targeting risk — does `"FlaUI.Mcp"` match `FlaUI.Mcp.exe`?

**Anchor:** `src/FlaUI.Mcp/FlaUI.Mcp.csproj:9` (`<AssemblyName>FlaUI.Mcp</AssemblyName>`).

**Yes — `"FlaUI.Mcp"` is correct and matches the actual exe.** Microsoft Learn states ([ProcessName remarks][procname]):

> The `ProcessName` property holds an executable file name, such as Outlook, that does not include the `.exe` extension or the path.

The csproj sets `<AssemblyName>FlaUI.Mcp</AssemblyName>`, producing `FlaUI.Mcp.exe`. Windows reports the friendly name as `FlaUI.Mcp` (extension stripped). The current call at Program.cs:69 is therefore correct and should not be changed. Do **not** change it to `"FlaUI.Mcp.exe"` — that would silently match zero processes. **Confidence: HIGH** (official docs + verified against the project's csproj).

---

## 3. Port-bind verification — TIME_WAIT after killing the listener

**Anchor:** Phase-4 streamable HTTP transport binds Kestrel to `127.0.0.1:3020` (CliOptions.cs:34-35).

### 3a. Does the OS release the port immediately after `Kill()`?
**Yes for the listening socket — TIME_WAIT does not apply.** The TIME_WAIT state in TCP is per-connection (4-tuple), not per-listening-socket: it applies to fully-established connections that closed gracefully. A **listening socket** (LISTEN state) is bound to a local port via `bind()` and released when the owning process is terminated and its handles are reclaimed by the kernel. `taskkill /F` (and `Process.Kill()`, which calls `TerminateProcess`) free the port immediately for re-bind once the kernel finalizes the process exit — that finalization is what `WaitForExit` waits on.

### 3b. Is killing-then-binding-immediately reliable?
**Yes — provided we wait for the kill to complete.** Two requirements:
1. **Always call `WaitForExit(2000)` after `Kill()`** — `Kill` is asynchronous; without the wait, the new Kestrel `bind()` can race the old process's socket teardown and trip a transient `EADDRINUSE` ([Kill remarks][kill]; corroborated by the Kestrel issue tracker showing transient bind failures during recycling races).
2. **Bind happens later in startup** — after this kill block we still run NLog config, firewall config, host builder construction. That sequential work easily exceeds the kernel's socket-cleanup window in practice, so even in the unlikely case `WaitForExit` returns `true` slightly before the kernel fully releases the socket, the subsequent startup work serves as additional buffer.

There is no known need for an explicit retry loop around Kestrel bind on Windows for this scenario. The Kestrel "address already in use" issues in the GitHub tracker (`dotnet/aspnetcore#2272`, `aspnet/KestrelHttpServer#2250`) all involve graceful service recycling races on Azure — a different failure mode than killing-then-binding from the same process. **Confidence: HIGH** (TCP semantics + Microsoft Learn + corroborating community evidence). **Recommendation:** no retry logic needed; `WaitForExit(2000)` is sufficient.

---

## 4. Best test approach — can we unit-test the stale-kill?

**Anchor:** Phase-4 introduced `tests/FlaUI.Mcp.Tests/` (xunit) for `CliOptions` parsing.

### 4a. Unit-testing in xunit — not feasible cleanly
- `Process.GetProcessesByName` and `Process.Kill` are **static / sealed** APIs with direct OS calls. Not mockable without an abstraction layer (e.g. an `IProcessHost` interface) — and CONTEXT.md does not authorize that refactor.
- Spawning a real `FlaUI.Mcp.exe` from a test would require a built artifact, port allocation, and cleanup orchestration — heavyweight and flaky.
- The kill block is straight-line code with one observable side effect (the targeted processes are gone) — limited unit-test value vs. cost.

**Recommendation:** **do not add a unit test for the kill itself.** Keep CliOptions tests untouched (FIX-04), and verify via manual UAT.

### 4b. What CAN be unit-tested
A test asserting the kill block is **not invoked when `--help` is passed** would require either (a) the abstraction refactor above, or (b) inspecting program control flow via integration tests. Neither is justified for a quick fix. The `--help` short-circuit is already covered by the existing path that calls `Environment.Exit(0)` at Program.cs:59 *before* the kill block runs — code review is sufficient.

### 4c. Manual UAT steps for the validator
The validator should run these on a Windows host with the project published or `dotnet run`-able:

1. **Headless Task-Scheduler-style relaunch (the failing scenario):**
   - Start instance A: `FlaUI.Mcp.exe` (no flags — defaults to http transport, port 3020).
   - Confirm A is listening: `netstat -ano | findstr :3020` → expect a `LISTENING` row with A's PID.
   - Start instance B with no flags. **Expected:** B kills A, B binds 3020.
   - Verify: `Get-Process -Name FlaUI.Mcp` returns one row (B's PID); `netstat -ano | findstr :3020` shows B's PID.

2. **Explicit `-c` second launch (the previously broken case):**
   - Start instance A headless (no flags).
   - Start instance B with `-c`. **Expected:** B kills A, B's console attaches and shows ConsoleTarget output, port 3020 binds.
   - Verify console output appears in B's terminal; A is gone from `tasklist`.

3. **`--help` does NOT kill:**
   - Start instance A: `FlaUI.Mcp.exe`.
   - Run `FlaUI.Mcp.exe --help` from another shell. **Expected:** help prints, exits 0; A remains running (verify via `Get-Process -Name FlaUI.Mcp`).

4. **No stale processes (clean start):**
   - Ensure no `FlaUI.Mcp` is running.
   - Start instance A. **Expected:** clean startup, no errors on stderr from the kill block, port 3020 bound. The kill block iterating an empty array is a no-op.

5. **Debugger F5 path (regression check for Phase-3 TSK-05):**
   - From Visual Studio / Rider, F5-launch a Debug build with no args.
   - **Expected:** `console = true; debug = true` are still applied; any prior `FlaUI.Mcp` instance is killed (now via the new top-level block, not the Debugger branch).

**Confidence: HIGH** for the test-strategy recommendation; this matches the Phase-3 TSK-05 manual-validation pattern already established in the project.

---

## Sources

### Primary (HIGH confidence)
- [`Process.GetProcessesByName` — Microsoft Learn (.NET 8)][gpbn] — return semantics, includes current process, exception list.
- [`Process.Kill` — Microsoft Learn (.NET 8)][kill] — asynchronous behavior, `Win32Exception` surface, `InvalidOperationException` only on .NET Framework for already-exited.
- [`Process.ProcessName` — Microsoft Learn (.NET 8)][procname] — `.exe` extension is stripped from friendly name.
- `src/FlaUI.Mcp/FlaUI.Mcp.csproj` line 9 — confirms `AssemblyName = "FlaUI.Mcp"`.

### Secondary (MEDIUM confidence)
- Kestrel "address already in use" issue tracker ([dotnet/aspnetcore#2272][k1], [aspnet/KestrelHttpServer#2250][k2]) — confirms the failure mode arises from graceful-recycle races, not from kill-then-bind. Used as corroborating evidence for "no retry loop needed."

[gpbn]: https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.getprocessesbyname?view=net-8.0
[kill]: https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill?view=net-8.0
[procname]: https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.processname?view=net-8.0
[k1]: https://github.com/dotnet/aspnetcore/issues/2272
[k2]: https://github.com/aspnet/KestrelHttpServer/issues/2250

## Metadata

**Confidence breakdown:**
- Process API behavior (Q1, Q2): HIGH — Microsoft Learn primary sources, verified against project csproj.
- Port release on kill (Q3): HIGH — TCP semantics + Microsoft Learn + tracker corroboration.
- Test strategy (Q4): HIGH — pragmatic recommendation aligned with existing project conventions; no refactor authorized by CONTEXT.md.

**Research date:** 2026-05-01
**Valid until:** 2026-06-01 (.NET 8 LTS — stable through Nov 2026).
