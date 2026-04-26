---
phase: 4
slug: streamable-http-transport
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-26
---

# Phase 4 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit (none — Wave 0 installs) |
| **Config file** | none — Wave 0 adds `tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj` |
| **Quick run command** | `dotnet test tests/FlaUI.Mcp.Tests --no-build --filter Category=Quick` |
| **Full suite command** | `dotnet test tests/FlaUI.Mcp.Tests` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build src/FlaUI.Mcp` + quick test command
- **After every plan wave:** Run full suite command
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| TBD — populated by gsd-planner | | | | | | | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj` — xunit + Microsoft.AspNetCore.Mvc.Testing test project
- [ ] `tests/FlaUI.Mcp.Tests/HttpTransportTests.cs` — `WebApplicationFactory<Program>` fixture stubs
- [ ] `tests/FlaUI.Mcp.Tests/SseTransportTests.cs` — legacy `/sse` + `/messages` regression stubs
- [ ] xunit + Mvc.Testing package install (no test framework currently in repo)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live MCP client init/list/call against `/mcp` | HTTP-03 (live client interop) | Requires real Claude Code or comparable client process; not feasible in unit harness | Configure `.mcp.json` with `"type": "http", "url": "http://localhost:3020/mcp"`; restart Claude Code; confirm tools listed and a Launch + Snapshot round-trip succeeds |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
