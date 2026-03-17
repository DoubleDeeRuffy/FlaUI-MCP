# Coding Conventions

**Analysis Date:** 2026-03-17

## Naming Patterns

**Files:**
- Pascal case with descriptive tool/component names
- Examples: `LaunchTool.cs`, `ClickTool.cs`, `SessionManager.cs`, `ElementRegistry.cs`
- Tool files follow pattern: `[ActionName]Tool.cs` or `[ActionName]Tools.cs` (for tool groups)
- Supporting classes: `[Concept]Manager.cs`, `[Concept]Registry.cs`, `[Concept]Builder.cs`

**Functions:**
- Pascal case for public methods
- Examples: `LaunchApp()`, `GetWindow()`, `RegisterElement()`, `BuildSnapshot()`
- Descriptive verb-noun pattern: `GetElementName()`, `GetStateIndicators()`, `ShouldSkipElement()`
- Protected/private helpers: Pascal case (C# convention)
- Async methods use `Async` suffix: `ExecuteAsync()`, `RunAsync()`, `SendEventAsync()`

**Variables:**
- camelCase for local variables and parameters
- Examples: `windowHandle`, `elementName`, `refId`, `elementRegistry`
- Private fields use underscore prefix with camelCase: `_elementRegistry`, `_sessionManager`, `_tools`
- Read-only collections: `_elements = new()`, `_windows = new()`

**Types:**
- Pascal case for classes, interfaces, records
- Classes: `SessionManager`, `ElementRegistry`, `SnapshotBuilder`
- Interfaces: `ITool`
- Records: `JsonRpcRequest`, `JsonRpcResponse`, `McpServerInfo`
- No prefixes for classes; interfaces use `I` prefix only

**Constants:**
- Pascal case for public constants (namespaces)
- Magic numbers avoided—extracted to parameters with defaults
- Example: `int maxDepth = 10` in `SnapshotBuilder`

## Code Style

**Formatting:**
- Target: .NET 8.0 with implicit usings enabled
- Line length: No hard limit observed; code adapts to content
- Indentation: 4 spaces (inferred from codebase)
- Brace style: Allman (opening brace on new line) for classes/methods
- Inline braces for single-statement blocks: `if (x) return null;`

**Linting:**
- No explicit linter config detected; following C# conventions
- Enabled features: Implicit usings (`ImplicitUsings`), Nullable reference types (`Nullable enable`)
- Error handling: Exceptions caught and wrapped in `McpToolResult` with `IsError = true`

**Null Handling:**
- Nullable reference types enabled: properties use `?` when optional
- Pattern: Check `TryGetValue()` before use or inline null checks
- Example from `ElementRegistry.cs`: `_elements.TryGetValue(refId, out var element) ? element : null`

## Import Organization

**Order:**
1. Standard library imports (`System.*`)
2. Third-party NuGet packages (FlaUI, Microsoft.AspNetCore, etc.)
3. Application namespace imports

**Examples:**
```csharp
using System.Text.Json;
using System.Collections.Concurrent;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using PlaywrightWindows.Mcp.Core;
using PlaywrightWindows.Mcp.Tools;
```

**Path Aliases:**
- None detected; full namespace paths used
- Root namespace: `PlaywrightWindows.Mcp` (Note: namespace differs from project name `FlaUI.Mcp`)

## Error Handling

**Patterns:**
- Try-catch with specific error reporting
- Errors wrapped in `McpToolResult` with `IsError = true` flag
- Error messages include context (element ref, app path, window handle)
- Silent catches for UI Automation edge cases (some operations throw on certain elements)

**Examples:**
- `SessionManager.cs` line 47: `catch { /* Some processes don't support this */ }` for non-critical failures
- `SnapshotBuilder.cs` lines 59-62: `catch { }` when accessing children of elements that don't expose them
- `McpServer.cs` line 46-48: Errors logged to stderr with request context

**Resilience:**
- Fallback patterns implemented (e.g., `ClickTool.cs`: Try Invoke → Try Toggle → Try SelectionItem → Mouse click)
- Validation checks before operations
- Window/element existence verified before use

## Logging

**Framework:** Console stderr (standard output reserved for JSON-RPC)

**Patterns:**
- Infrastructure messages to stderr: `Console.Error.WriteLine()`
- Request errors to stderr: `Console.Error.WriteLine($"Error processing request: {ex.Message}")`
- Tools log via return results, not console
- Example from `SseTransport.cs`: Startup messages written to stderr

**Severity Levels:**
- Info: Server startup, transport listeners
- Error: Request processing failures, exception messages
- No explicit warning or debug levels observed

**When to Log:**
- Server startup events and configuration
- Request processing exceptions
- Not on tool execution (results embedded in MCP response)

## Comments

**When to Comment:**
- XML doc comments for public members
- Inline comments for non-obvious logic
- Why-comments for workarounds and edge cases

**JSDoc/TSDoc:**
- XML documentation comments used (C# style)
- Format: `/// <summary>[description]</summary>`
- Applied to classes, interfaces, and public methods
- Applied to key private methods for understanding

**Examples:**
```csharp
/// <summary>
/// Maps element refs (like "w1e5") to AutomationElements
/// Refs are scoped to windows and regenerated on each snapshot
/// </summary>
public class ElementRegistry

/// <summary>
/// Click an element by ref
/// </summary>
public class ClickTool : ToolBase
```

**Inline Comments:**
- Explain edge cases: `// Some elements throw when accessing children`
- Document fallback strategies: `// Try Invoke pattern first (most reliable for buttons)`
- Clarify intent: `// Keep the connection alive until cancelled`

## Function Design

**Size:**
- Small focused functions (typically 5-30 lines)
- Larger methods break work into logical phases
- Example: `SnapshotBuilder.BuildElementSnapshot()` is ~30 lines and handles recursion setup

**Parameters:**
- Minimal; typically 2-4 parameters
- Use objects for related parameters (e.g., `ref`, `button`, `doubleClick` as separate params—could use a command object)
- Nullable params marked with `?`
- Default parameters used sparingly: `public SnapshotBuilder(ElementRegistry elementRegistry, int maxDepth = 10)`

**Return Values:**
- Async methods return `Task<T>` or `Task`
- Nullable returns marked with `?`
- Error results returned as special `McpToolResult` instances (not exceptions from tools)
- Example: `GetElement()` returns `AutomationElement?` (null when not found)

## Module Design

**Exports:**
- Public classes are concrete implementations
- Interfaces used for abstraction: `ITool` for plugin pattern
- Base classes provide shared functionality: `ToolBase` for common tool utilities
- Singletons managed in `Program.cs`: `SessionManager`, `ElementRegistry`, `ToolRegistry`

**Barrel Files:**
- Not used; each file contains single primary type
- Namespace organization: `PlaywrightWindows.Mcp.Tools`, `PlaywrightWindows.Mcp.Core`, `PlaywrightWindows.Mcp`

**Dependency Injection:**
- Manual constructor injection (no DI container)
- Services passed to tool constructors: `LaunchTool(SessionManager sessionManager)`
- Allows easy composition in `Program.cs`

## Design Patterns

**Tool Pattern:**
- Abstract base `ToolBase` defines contract
- Each tool implements `Name`, `Description`, `InputSchema` properties
- `ExecuteAsync()` processes arguments and returns `McpToolResult`
- Registry pattern: `ToolRegistry` manages tool instances and dispatch
- Example tools: `LaunchTool`, `ClickTool`, `SnapshotTool`, `TypeTool`, `FillTool`

**Session/State Management:**
- `SessionManager` owns application lifecycle and window tracking
- `ElementRegistry` maintains element snapshot state (cleared per snapshot)
- Handles are string identifiers: `w1` (window 1), `w1e5` (window 1, element 5)
- Session isolated via closure over shared manager instances

**Transport Abstraction:**
- `McpServer` decoupled from I/O (handles JSON-RPC logic)
- `SseTransport` provides HTTP + SSE transport layer
- Stdio transport in `Program.cs` (default)
- Swappable via `--transport` flag

---

*Convention analysis: 2026-03-17*
