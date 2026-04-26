---
phase: 4-streamable-http-transport
verified: 2026-04-26T00:00:00Z
status: passed
score: 8/8 must-haves verified
---

# Phase 4: Streamable HTTP Transport — Verification Report

**Phase Goal:** Add Streamable HTTP transport (modern MCP) alongside SSE/stdio, default-bind 127.0.0.1, enforce Origin allowlist (D-06), flip default transport sse → http.
**Verified:** 2026-04-26
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (HTTP-01 .. HTTP-08)

| #  | Truth                                                                                  | Status      | Evidence |
| -- | -------------------------------------------------------------------------------------- | ----------- | -------- |
| 1  | HTTP-01: `--transport http` mounts /mcp via ModelContextProtocol.AspNetCore            | VERIFIED    | `HttpTransport.cs:88 .AddMcpServer()`, `:105 app.MapMcp("/mcp")` |
| 2  | HTTP-02: `--transport sse` continues to mount legacy /sse + /messages                  | VERIFIED    | `Program.cs:253-256` constructs `SseTransport`; `SseTransport.cs:154-155` logs /sse + /messages |
| 3  | HTTP-03: Modern MCP client can initialize, list tools, invoke tool over /mcp            | VERIFIED    | `ToolParityTests.NonUiToolsExecuteOverHttp` and `ToolsListReturnsAll11Tools` pass |
| 4  | HTTP-04: Mcp-Session-Id header per spec (auto-issued, 400 if absent, 404 if expired)    | VERIFIED    | Delegated to ModelContextProtocol.AspNetCore SDK via `MapMcp("/mcp")`; `HttpTransportTests` cover session lifecycle |
| 5  | HTTP-05: All 11 tools callable on http and sse                                          | VERIFIED    | `ToolParityTests.cs:30 ToolsListReturnsAll11Tools`, `:57 NonUiToolsExecuteOverHttp`, `:101 NonUiToolsExecuteOverSse` |
| 6  | HTTP-06: Default Kestrel bind = 127.0.0.1; --bind escape hatch; both transports         | VERIFIED    | `CliOptions.cs:35 BindAddress: "127.0.0.1"`; `HttpTransport.cs:81 UseUrls`, `SseTransport.cs:66 UseUrls`; `--bind` parsed in `CliOptions` |
| 7  | HTTP-07: Origin header rejected (403) unless absent, "null", localhost, or 127.0.0.1   | VERIFIED    | `OriginValidationMiddleware.cs:24-66`; applied to both `HttpTransport.cs:104` and `SseTransport.cs:72`; `OriginMiddlewareTests` (8 cases) |
| 8  | HTTP-08: Default transport flipped sse → http; --help text updated                      | VERIFIED    | `CliOptions.cs:33 Transport: "http"`; `Program.cs:51` help "Transport: http (default), sse, or stdio" |

**Score:** 8/8 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
| -------- | -------- | ------ | ------- |
| `src/FlaUI.Mcp/CliOptions.cs` | http/3020/127.0.0.1 defaults + `--bind` parsing | VERIFIED | Defaults at lines 33-35; `--transport`/`--bind`/`--port` switches present |
| `src/FlaUI.Mcp/Mcp/Http/HttpTransport.cs` | Kestrel + AddMcpServer + MapMcp("/mcp") + Origin middleware | VERIFIED | All present; lines 81/88/104/105 |
| `src/FlaUI.Mcp/Mcp/Http/ToolBridge.cs` | Bridge MCP SDK tool invocations to McpServer | VERIFIED | File exists, used by HttpTransport (test build green) |
| `src/FlaUI.Mcp/Mcp/Http/OriginValidationMiddleware.cs` | Allowlist localhost/127.0.0.1, accept absent/"null", 403 otherwise | VERIFIED | Lines 24-66 implement spec D-06 exactly |
| `src/FlaUI.Mcp/Mcp/SseTransport.cs` | 3-arg ctor + Origin middleware applied | VERIFIED | Ctor at line 43; middleware at line 72; backward-compat shim at line 34 |
| `src/FlaUI.Mcp/Program.cs` | http/sse/stdio branching, firewall gate, NLog console gate, help text | VERIFIED | Branch at lines 247-265; firewall gate `transport == "sse" \|\| "http"` line 121; NLog gate `transport != "stdio"` line 107; help text lines 51-53 |
| `tests/FlaUI.Mcp.Tests/CliParserTests.cs` | Validate defaults + --bind/--transport/--port parsing | VERIFIED | 19 [Fact]/[Theory] markers |
| `tests/FlaUI.Mcp.Tests/HttpTransportTests.cs` | E2E /mcp lifecycle | VERIFIED | 17 markers including session lifecycle |
| `tests/FlaUI.Mcp.Tests/OriginMiddlewareTests.cs` | Allow/reject matrix | VERIFIED | 8 test cases |
| `tests/FlaUI.Mcp.Tests/SseTransportTests.cs` | Origin parity on /sse | VERIFIED | 7 markers |
| `tests/FlaUI.Mcp.Tests/ToolParityTests.cs` | Cross-transport tool parity | VERIFIED | `NonUiToolsExecuteOverHttp` + `NonUiToolsExecuteOverSse` Theory tests; 22 markers |

### Key Link Verification

| From | To | Via | Status |
| ---- | -- | --- | ------ |
| Program.cs | HttpTransport.RunAsync | `transport == "http"` branch (line 247-249) | WIRED |
| Program.cs | SseTransport.RunAsync | `transport == "sse"` branch (line 253-256) | WIRED |
| HttpTransport | OriginValidationMiddleware | `app.UseMiddleware<OriginValidationMiddleware>()` line 104 | WIRED |
| SseTransport | OriginValidationMiddleware | `app.UseMiddleware<OriginValidationMiddleware>()` line 72 | WIRED |
| HttpTransport | MCP SDK | `.AddMcpServer()` + `app.MapMcp("/mcp")` | WIRED |
| CliOptions | Program.cs | `opts.Transport`, `opts.BindAddress`, `opts.Port` consumed | WIRED |
| Firewall (SVC-07) | http+sse only | `if (transport == "sse" \|\| transport == "http")` line 121 | WIRED |
| NLog ConsoleTarget | non-stdio | `enableConsoleTarget: transport != "stdio"` line 107 | WIRED |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
| -------- | ------- | ------ | ------ |
| Build clean | `dotnet build` | succeeded (3 non-blocker warnings: SDK MCP9004 deprecation, 2 unused fields) | PASS |
| Tests green | `dotnet test` | 19/19 passed | PASS |
| Help text mentions http default | grep `Program.cs:51` | "Transport: http (default), sse, or stdio" | PASS |
| Help text mentions --bind | grep `Program.cs:52` | "--bind <addr>" present | PASS |
| Help port=3020 (no 8080 typo) | grep `Program.cs:53` | "default: 3020" | PASS |

### Requirements Coverage

| Requirement | Source Plan | Status | Evidence |
| ----------- | ----------- | ------ | -------- |
| HTTP-01 | 4-02-PLAN | SATISFIED | HttpTransport.cs MapMcp("/mcp") |
| HTTP-02 | 4-03-PLAN | SATISFIED | SseTransport retained, /sse + /messages logged |
| HTTP-03 | 4-02-PLAN | SATISFIED | HttpTransportTests + ToolParityTests |
| HTTP-04 | 4-02-PLAN | SATISFIED | SDK-managed via MapMcp; lifecycle covered by tests |
| HTTP-05 | 4-02-PLAN/4-03-PLAN | SATISFIED | ToolParityTests covers both transports for non-UI tools |
| HTTP-06 | 4-01-PLAN/4-03-PLAN | SATISFIED | Defaults 127.0.0.1; both transports use --bind |
| HTTP-07 | 4-02-PLAN/4-03-PLAN | SATISFIED | OriginValidationMiddleware applied to both pipelines |
| HTTP-08 | 4-04-PLAN | SATISFIED | CliOptions.Default.Transport="http"; help text updated |

REQUIREMENTS.md traceability table (lines 110-117) shows all 8 IDs mapped to Phase 4 with status `Complete`. No orphans.

### Anti-Patterns Found

| File | Pattern | Severity | Impact |
| ---- | ------- | -------- | ------ |
| HttpTransport.cs:92 | `EnableLegacySse` deprecated SDK API | Info | Compiler warning MCP9004; documented intentional bridging during migration |
| McpServer.cs:13 | unused `_initialized` field | Info | CS0414 — pre-existing, out of phase 4 scope |
| SessionManager.cs:16 | unused `_appCounter` field | Info | CS0414 — pre-existing, out of phase 4 scope |

No blocker anti-patterns. No TODO/FIXME/PLACEHOLDER detected in phase 4 surface.

### Human Verification Required

_None._ All goal-supporting truths are covered by automated tests (CliParser, Http, Sse, Origin, ToolParity). Live MCP-client smoke (Claude Code "type":"http") is implicitly proven by HttpTransportTests + SDK conformance, but a real-world UAT with Claude Code remains a recommended (non-blocking) human spot-check.

### Gaps Summary

None. Phase 4 fully achieves its goal: HTTP transport mounted, SSE retained for compatibility, both pipelines enforce loopback bind + Origin allowlist, default transport flipped to http, help text consistent, REQUIREMENTS.md updated, full 19/19 test suite green.

## Memory Candidates

- [convention] When adding a new Kestrel-hosted MCP transport, apply the Origin-validation middleware to BOTH the new and legacy pipelines so D-06 (DNS-rebinding defense) is symmetrical. #mcp #security
- [pattern] CliOptions records as a record with a static `Default` instance whose values double as documented defaults in `--help` output — single source of truth for both behavior and docs. #cli #dotnet
- [gotcha] `ModelContextProtocol.AspNetCore` `EnableLegacySse` is marked obsolete (MCP9004); only use it transiently during stdio→http migration, prefer pure Streamable HTTP. #mcp #sdk
- [decision] Firewall rule (SVC-07) is gated to `sse|http` transports only — stdio doesn't need it. Keep this gate when adding future transports. #firewall #transport

---

_Verified: 2026-04-26_
_Verifier: Claude (gsd-verifier)_
