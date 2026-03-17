# Codebase Concerns

**Analysis Date:** 2026-03-17

## Tech Debt

**Hardcoded timing delays in startup and interaction flows:**
- Issue: Application launch uses fixed `Thread.Sleep()` calls (1000ms in `LaunchApp`, 500ms loops, 30-50ms in tool interactions) instead of polling/wait strategies. These don't adapt to system performance and can cause unreliable behavior.
- Files: `src/FlaUI.Mcp/Core/SessionManager.cs` (lines 49, 74), `src/FlaUI.Mcp/Tools/BatchTool.cs` (lines 188, 219, 221, 229), `src/FlaUI.Mcp/Tools/TypeTools.cs` (lines 73, 168, 170)
- Impact: Tests fail on slow systems, automation becomes unreliable under load, performance is suboptimal
- Fix approach: Replace with event-driven polling or FlaUI's built-in wait mechanisms (e.g., WaitUntil patterns, retry logic with exponential backoff)

**Generic exception handling with swallowed errors:**
- Issue: Multiple `catch` blocks that silently swallow exceptions with `catch { }` (lines 47, 60, 142-148, 181-182 in SessionManager.cs) hide real failures, making debugging difficult
- Files: `src/FlaUI.Mcp/Core/SessionManager.cs` (lines 47, 142-148, 181-182), `src/FlaUI.Mcp/Mcp/SseTransport.cs` (line 53)
- Impact: Silent failures in window detection, process startup errors not propagated, makes production debugging nearly impossible
- Fix approach: Log errors before swallowing them, or re-throw specific exceptions with context; only catch when recovery is certain

**Broad `Exception` throws instead of specific types:**
- Issue: Code uses generic `throw new Exception()` for all error cases instead of domain-specific exception types
- Files: `src/FlaUI.Mcp/Core/SessionManager.cs` (lines 39, 96, 110, 161, 171), `src/FlaUI.Mcp/Mcp/McpServer.cs` (line 63)
- Impact: Callers cannot distinguish between recoverable errors and fatal ones; error handling becomes generic "catch all" instead of graceful
- Fix approach: Create specific exception types (`WindowNotFound`, `ProcessStartFailed`, `ElementNotFound`) and throw appropriately

**Element registry never evicts stale references:**
- Issue: `ElementRegistry` accumulates `AutomationElement` references indefinitely; only clears on snapshot refresh but never bounds growth
- Files: `src/FlaUI.Mcp/Core/ElementRegistry.cs` (lines 11-57)
- Impact: Long-running sessions with many snapshots cause memory creep; stale element references may point to disposed objects
- Fix approach: Add TTL/expiration for references, implement weak references for elements, add hard limit on registry size with LRU eviction

**Session/window accumulation with no cleanup policy:**
- Issue: `SessionManager` accumulates windows in `_windows` dict indefinitely; no policy for stale/closed windows
- Files: `src/FlaUI.Mcp/Core/SessionManager.cs` (lines 14-15, 99, 117-122)
- Impact: Long-running servers leak window handles; closed windows remain registered and consume memory
- Fix approach: Add reference counting or weak reference tracking; validate window is still valid before use; add cleanup on window close

## Known Bugs

**Window detection heuristic may incorrectly match windows:**
- Symptoms: `LaunchApp` may attach to wrong window if multiple apps with similar titles are running
- Files: `src/FlaUI.Mcp/Core/SessionManager.cs` (lines 72-92, particularly lines 82-84)
- Trigger: Launch two apps where title of first contains app name substring of second (e.g., "Editor" and "TextEditor")
- Workaround: Explicitly use `windows_list_windows` and `windows_focus` to target correct window rather than relying on `LaunchApp` detection

**Focused element may not be retrievable in some UWP/Store apps:**
- Symptoms: `FocusedElement()` returns null in UWP apps, snapshot fails with "No window found" error
- Files: `src/FlaUI.Mcp/Tools/SnapshotTool.cs` (lines 63-78), `src/FlaUI.Mcp/Tools/ScreenshotTool.cs` (lines 84-103)
- Trigger: Call snapshot/screenshot without explicit handle when focused app is UWP app
- Workaround: Pass explicit `handle` parameter from `windows_list_windows` result; do not rely on focus detection

**Stale element references cause crashes:**
- Symptoms: "Element not found" after window has been minimized/restored or during rapid interactions
- Files: `src/FlaUI.Mcp/Tools/ClickTool.cs` (line 62), `src/FlaUI.Mcp/Core/ElementRegistry.cs` (line 46)
- Trigger: Snapshot references become invalid if window changes state or tree restructures
- Workaround: Always call fresh `windows_snapshot` before using element refs; assume refs are single-use

## Security Considerations

**No validation of executable paths in `windows_launch`:**
- Risk: Malicious client could launch arbitrary executables from any system path
- Files: `src/FlaUI.Mcp/Tools/LaunchTool.cs` (lines 43-62)
- Current mitigation: None; relies on MCP transport-level access control
- Recommendations: Add whitelist of allowed executables/paths; validate paths don't traverse outside approved directories; log all launches

**No input sanitization for text input in typing tools:**
- Risk: Keyboard input could contain special sequences that unintentionally trigger system commands
- Files: `src/FlaUI.Mcp/Tools/TypeTools.cs` (lines 51-86), `src/FlaUI.Mcp/Tools/BatchTool.cs` (lines 171-193)
- Current mitigation: None; all text passed directly to keyboard
- Recommendations: Validate/escape special key combinations; add allowlist for permitted input characters; log all keyboard input

**SSE transport has no authentication or TLS configuration:**
- Risk: HTTP SSE endpoint on network-accessible port 8080 could allow remote clients to execute tools
- Files: `src/FlaUI.Mcp/Mcp/SseTransport.cs` (line 30)
- Current mitigation: Listens only when explicitly configured with `--transport sse`; uses HTTP not HTTPS
- Recommendations: Add TLS/SSL support; require bearer token authentication; document security implications; bind to localhost only by default

**Session isolation not enforced:**
- Risk: SSE clients could interfere with each other's windows if session management inadequate
- Files: `src/FlaUI.Mcp/Mcp/SseTransport.cs` (lines 41-42), `src/FlaUI.Mcp/Core/SessionManager.cs` (lines 14-15)
- Current mitigation: Session IDs are generated but not validated across requests
- Recommendations: Bind windows to session IDs; validate session ownership before tool execution

## Performance Bottlenecks

**Snapshot generation with deep element trees:**
- Problem: `BuildElementSnapshot` recursively walks entire tree to `_maxDepth=10` even for large windows, creating thousands of elements
- Files: `src/FlaUI.Mcp/Core/SnapshotBuilder.cs` (lines 31-63, 257)
- Cause: No early termination for large branches; depth limit is global not per-branch; all elements registered even if not actionable
- Improvement path: Implement smart pruning (skip decorative-only subtrees), configurable depth per branch, lazy element registration, caching of unchanged subtrees

**Full tree re-registration on every snapshot:**
- Problem: `ClearWindow` + full re-register happens on each snapshot call even if tree unchanged
- Files: `src/FlaUI.Mcp/Core/SnapshotBuilder.cs` (lines 23-24), `src/FlaUI.Mcp/Core/ElementRegistry.cs` (lines 17-26)
- Cause: No tree diffing; treats every snapshot as completely new
- Improvement path: Implement incremental snapshot updates, track tree hash to detect changes, maintain stable refs across snapshots

**String-based element matching in window detection:**
- Problem: `LaunchApp` scans all windows and does case-insensitive title matching for every app (potentially 100+ windows)
- Files: `src/FlaUI.Mcp/Core/SessionManager.cs` (lines 72-92)
- Cause: No process-to-window cache; loops through all windows repeatedly with substring matching
- Improvement path: Build process ID → window handle cache, use process directly instead of title scan

**Screenshot memory allocation without streaming:**
- Problem: Screenshots loaded entirely into memory as byte arrays before encoding
- Files: `src/FlaUI.Mcp/Tools/ScreenshotTool.cs` (lines 105-107)
- Cause: MemoryStream and bitmap in memory simultaneously
- Improvement path: Stream directly to base64 encoder if possible; implement screenshot compression options; add size limits

## Fragile Areas

**Window/element lifetime management:**
- Files: `src/FlaUI.Mcp/Core/SessionManager.cs`, `src/FlaUI.Mcp/Core/ElementRegistry.cs`
- Why fragile: No ref-counting; manual cleanup via `Dispose()` can fail if window closes unexpectedly; stale references silently fail
- Safe modification: Wrap window/element access in try-catch for disposal errors; use `using` statements for AutomationElement where possible; validate element is still valid before operations
- Test coverage: No tests for window close/reopen scenarios; snapshot refresh with stale refs; concurrent access patterns

**Batch tool action executor:**
- Files: `src/FlaUI.Mcp/Tools/BatchTool.cs` (lines 89-273)
- Why fragile: Sequential execution with fixed timing; if one action fails element refs become invalid for subsequent actions; stopOnError flag doesn't clean up state
- Safe modification: Add state rollback on failure; validate element refs before each action; implement action isolation (don't reuse stale refs)
- Test coverage: No tests for mixed success/failure sequences; no tests for missing elements mid-batch

**Focus-based window detection fallback:**
- Files: `src/FlaUI.Mcp/Tools/SnapshotTool.cs` (lines 62-83), `src/FlaUI.Mcp/Tools/ScreenshotTool.cs` (lines 84-102)
- Why fragile: Relies on `FocusedElement()` which may return null or misleading element in some apps; parent traversal assumes window ancestor exists
- Safe modification: Add validation that `FocusedElement()` returned valid result; handle case where no Window ancestor found; add logging for failures
- Test coverage: No tests for apps without focus support; no tests for UWP/Store apps

**SnapshotBuilder state assumptions:**
- Files: `src/FlaUI.Mcp/Core/SnapshotBuilder.cs` (lines 31-63)
- Why fragile: Assumes children remain stable during tree walk; catches all exceptions silently (line 60); no context about what failed
- Safe modification: Validate element validity before accessing children; log caught exceptions for diagnostics; add timeout for tree walk
- Test coverage: No tests for trees that change during iteration; no tests for performance degradation on complex UIs

## Scaling Limits

**Element registry unbounded growth:**
- Current capacity: Elements never evicted; registry only cleared when window cleared
- Limit: In production with many snapshots, registry could accumulate 100,000+ references (10 snapshots × 10,000 elements)
- Scaling path: Implement weak references, TTL-based eviction (e.g., 30-min expiry), max-size overflow protection, session-level cleanup

**Window registry accumulation:**
- Current capacity: No limit on _windows dict; server can register unlimited windows
- Limit: After many `windows_list_windows` calls, registry may register hundreds of system windows
- Scaling path: Add configurable max windows (e.g., 100), implement LRU eviction, add window validity checks before use

**Snapshot depth on large applications:**
- Current capacity: 10-level depth walk on every snapshot; can traverse 10,000+ elements
- Limit: For deeply nested modern UIs (nested virtualized lists), this becomes O(n) expensive and generates huge snapshots
- Scaling path: Implement lazy loading (only expand nodes on demand), context-sensitive depth, element count limits

**SSE client accumulation:**
- Current capacity: SSE clients held in ConcurrentDictionary indefinitely
- Limit: If clients disconnect ungracefully without cleanup, stale sessions accumulate
- Scaling path: Implement connection timeout/heartbeat, automatic session cleanup after inactivity, max client limit

## Dependencies at Risk

**FlaUI.Core 5.0.0:**
- Risk: Tied to Windows UI Automation API; if app breaks accessibility tree contract, snapshot fails silently
- Impact: No snapshot available means automation blind; no fallback to alternate accessibility method
- Migration plan: Consider adding accessibility tree sanitizer to make failures more visible; potentially add OCR fallback for critical scenarios

**System.Drawing.Common 10.0.2:**
- Risk: Uses legacy GDI+ for bitmap operations; may have performance/memory issues with large screenshots
- Impact: Screenshots slow on high-res displays; memory pressure in batch operations
- Migration plan: Consider ImageSharp or SkiaSharp for better performance and async support

## Missing Critical Features

**No error recovery/retry logic:**
- Problem: If a tool fails, client must manually retry; no exponential backoff or circuit breaker
- Blocks: Complex multi-step automation scenarios need resilience
- Recommendation: Implement retry decorator on tool execution with configurable backoff

**No timeout enforcement:**
- Problem: Snapshot walk or window search could hang indefinitely on frozen apps
- Blocks: Server can hang waiting for unresponsive UI Automation provider
- Recommendation: Add per-tool timeout, async operation with cancellation tokens

**No observability/diagnostics:**
- Problem: When tools fail, no logs of what happened; no metrics on performance
- Blocks: Production debugging impossible; cannot identify slow tools or problematic apps
- Recommendation: Add structured logging with correlation IDs, trace tool execution time, log element counts

**No snapshot diffing/incremental updates:**
- Problem: Every snapshot is full tree dump; no way to understand what changed
- Blocks: Agents cannot efficiently track UI changes; snapshot messages are unnecessarily large
- Recommendation: Implement delta snapshots showing only changed elements

## Test Coverage Gaps

**No tests for window lifecycle:**
- What's not tested: App launch failure; process exits while registered; window closes and reopens
- Files: `src/FlaUI.Mcp/Core/SessionManager.cs` (LaunchApp, Dispose)
- Risk: Window cleanup bugs go unnoticed; stale references crash unpredictably
- Priority: High - lifecycle bugs are production-critical

**No tests for element registry stale references:**
- What's not tested: Using refs after snapshot refresh; refs after window state changes; concurrent snapshot calls
- Files: `src/FlaUI.Mcp/Core/ElementRegistry.cs`
- Risk: Silent failures or crashes when agents reuse stale refs
- Priority: High - core interaction pattern

**No tests for batch tool mixed success/failure:**
- What's not tested: Batch with 5 actions where 3 fails; stopOnError=false with element not found; action isolation
- Files: `src/FlaUI.Mcp/Tools/BatchTool.cs`
- Risk: Unpredictable behavior in error cases; unsafe state transitions
- Priority: High - batch is performance-critical tool

**No tests for snapshot depth/complexity:**
- What's not tested: Large nested UI trees (1000+ elements); deeply nested virtualized lists; performance characteristics
- Files: `src/FlaUI.Mcp/Core/SnapshotBuilder.cs`
- Risk: Agent hits performance wall on certain apps without warning
- Priority: Medium - affects usability but not correctness

**No tests for SSE transport session isolation:**
- What's not tested: Concurrent SSE clients; client disconnects during request; malformed messages
- Files: `src/FlaUI.Mcp/Mcp/SseTransport.cs`
- Risk: Cross-contamination between sessions; ungraceful failures
- Priority: Medium - affects remote deployment scenarios

**No tests for focus-based detection fallbacks:**
- What's not tested: Apps without focus support; missing window ancestor; FocusedElement returns null
- Files: `src/FlaUI.Mcp/Tools/SnapshotTool.cs`, `src/FlaUI.Mcp/Tools/ScreenshotTool.cs`
- Risk: Silent failures in UWP/Store apps
- Priority: Medium - affects certain app classes

---

*Concerns audit: 2026-03-17*
