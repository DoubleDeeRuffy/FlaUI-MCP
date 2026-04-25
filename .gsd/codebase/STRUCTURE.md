# Codebase Structure

**Analysis Date:** 2026-03-17

## Directory Layout

```
FlaUI-MCP/
├── src/
│   └── FlaUI.Mcp/              # Main MCP server project
│       ├── Program.cs           # Entry point - CLI parsing, service instantiation, transport setup
│       ├── FlaUI.Mcp.csproj     # .NET 8 project file with NuGet dependencies
│       ├── Core/                # State management and UI tree traversal
│       │   ├── SessionManager.cs
│       │   ├── ElementRegistry.cs
│       │   └── SnapshotBuilder.cs
│       ├── Mcp/                 # Protocol, server, and transport implementations
│       │   ├── Protocol.cs
│       │   ├── McpServer.cs
│       │   ├── SseTransport.cs
│       │   └── ToolRegistry.cs
│       └── Tools/               # Tool implementations (automation capabilities)
│           ├── LaunchTool.cs
│           ├── SnapshotTool.cs
│           ├── ClickTool.cs
│           ├── TypeTools.cs
│           ├── FillTool.cs
│           ├── GetTextTool.cs
│           ├── ScreenshotTool.cs
│           ├── WindowTools.cs
│           └── BatchTool.cs
├── .gsd/
│   └── codebase/               # Analysis documents (this directory)
├── README.md                    # Main project documentation
└── LICENSE                      # MIT License
```

## Directory Purposes

**`src/FlaUI.Mcp/`:**
- Purpose: Main MCP server implementation
- Contains: C# source files organized by concern (Core, Mcp, Tools)
- Key files: `Program.cs` is the entry point; `FlaUI.Mcp.csproj` defines build/runtime config

**`Core/`:**
- Purpose: State management for Windows sessions and element references
- Contains: SessionManager (window lifecycle), ElementRegistry (ref mapping), SnapshotBuilder (UI tree serialization)
- Dependency: FlaUI.Core, FlaUI.UIA3 for UI Automation access
- Exports to: All tool implementations

**`Mcp/`:**
- Purpose: MCP protocol implementation and request routing
- Contains: JSON-RPC types (Protocol.cs), server orchestration (McpServer.cs), transport implementations (SseTransport.cs), tool registry (ToolRegistry.cs)
- Dependency: System.Text.Json for serialization
- Exports to: Transport layer and entry point

**`Tools/`:**
- Purpose: Individual MCP tool implementations (user-facing automation capabilities)
- Contains: 11 tool classes, each inheriting from ToolBase, each implementing ITool interface
- Dependency: Core/ for session and element access, Mcp/ for protocol types
- Pattern: Each tool is independent; no inter-tool dependencies; all stateless except state changes via FlaUI

## Key File Locations

**Entry Points:**
- `src/FlaUI.Mcp/Program.cs`: Process entry point. Parses CLI args, instantiates services, starts transport (stdio or SSE).

**Configuration:**
- `src/FlaUI.Mcp/FlaUI.Mcp.csproj`: Build configuration. Defines .NET version (net8.0-windows), dependencies (FlaUI.Core 5.0.0, FlaUI.UIA3 5.0.0), assembly metadata.

**Core Logic:**
- `src/FlaUI.Mcp/Core/SessionManager.cs`: Manages Windows application launches and window lifecycle. Central state holder for all active windows and FlaUI Automation instance.
- `src/FlaUI.Mcp/Core/ElementRegistry.cs`: Maps element refs (e.g., `w1e5`) to AutomationElement instances. Scoped per window.
- `src/FlaUI.Mcp/Core/SnapshotBuilder.cs`: Traverses AutomationElement tree, generates semantic refs, builds text snapshot with filtering.
- `src/FlaUI.Mcp/Mcp/McpServer.cs`: Routes JSON-RPC requests to tool handlers. Implements MCP initialize/tools/call methods.
- `src/FlaUI.Mcp/Mcp/ToolRegistry.cs`: Registry for tools. Contains ITool interface and ToolBase class used by all tool implementations.
- `src/FlaUI.Mcp/Mcp/Protocol.cs`: JSON-RPC and MCP types. Serialization configuration.
- `src/FlaUI.Mcp/Mcp/SseTransport.cs`: HTTP transport with Server-Sent Events. ASP.NET Core based.

**Tools:**
- `src/FlaUI.Mcp/Tools/LaunchTool.cs`: windows_launch
- `src/FlaUI.Mcp/Tools/SnapshotTool.cs`: windows_snapshot (primary tool for understanding UI)
- `src/FlaUI.Mcp/Tools/ClickTool.cs`: windows_click
- `src/FlaUI.Mcp/Tools/TypeTools.cs`: windows_type, windows_get_text
- `src/FlaUI.Mcp/Tools/FillTool.cs`: windows_fill
- `src/FlaUI.Mcp/Tools/ScreenshotTool.cs`: windows_screenshot
- `src/FlaUI.Mcp/Tools/WindowTools.cs`: windows_list_windows, windows_focus, windows_close
- `src/FlaUI.Mcp/Tools/BatchTool.cs`: windows_batch (multi-action execution)

## Naming Conventions

**Files:**
- Pattern: `{ToolName}Tool.cs` for tool implementations (e.g., ClickTool.cs, LaunchTool.cs)
- Pattern: `{Concept}.cs` for non-tool files (e.g., SessionManager.cs, SnapshotBuilder.cs, Protocol.cs)
- Convention: PascalCase for all C# files

**Directories:**
- Pattern: lowercase plural for categories (Core, Mcp, Tools)
- Convention: Organize by concern/layer, not by feature

**Namespaces:**
- Pattern: `PlaywrightWindows.Mcp` (root), `PlaywrightWindows.Mcp.Core`, `PlaywrightWindows.Mcp.Mcp`, `PlaywrightWindows.Mcp.Tools`
- Note: Namespace still references "PlaywrightWindows" (legacy naming before "FlaUI-MCP"); project folder and assembly are FlaUI.Mcp

**Classes & Interfaces:**
- Pattern: `{Noun}` for concrete implementations (SessionManager, ElementRegistry, SnapshotBuilder)
- Pattern: `I{Interface}` for interfaces (ITool)
- Pattern: `{Noun}Tool` for tool implementations (ClickTool, LaunchTool, SnapshotTool)

**Methods:**
- Pattern: `{Verb}{Noun}` for public methods (LaunchApp, GetWindow, BuildSnapshot, ExecuteToolAsync)
- Pattern: Private methods use same pattern (BuildElementSnapshot, GetElementRole, ShouldSkipElement)

**Properties & Variables:**
- Pattern: camelCase for private fields (e.g., `_sessionManager`, `_elementRegistry`)
- Pattern: PascalCase for public properties (e.g., `Name`, `Description`, `Automation`)
- Pattern: Single letter or descriptive for loop/temp variables (e.g., `result`, `index`, `handle`, `window`)

## Where to Add New Code

**New Tool/Capability:**
- Implementation: `src/FlaUI.Mcp/Tools/{NewFeatureName}Tool.cs`
- Base class: Inherit from `ToolBase` (in `src/FlaUI.Mcp/Mcp/ToolRegistry.cs`)
- Required overrides: `Name { get; }`, `Description { get; }`, `InputSchema { get; }`, `ExecuteAsync(JsonElement? arguments)`
- Registration: Add registration call in `Program.cs` (e.g., `toolRegistry.RegisterTool(new MyNewTool(...))`)
- Dependencies: Inject SessionManager, ElementRegistry, or other services as needed
- Example: See `src/FlaUI.Mcp/Tools/ClickTool.cs` for minimal single-action tool

**New Core State Management Feature:**
- Implementation: Add to existing Core class or create new file in `src/FlaUI.Mcp/Core/`
- If new class: Follow naming pattern `{Concept}.cs`, use namespace `PlaywrightWindows.Mcp.Core`
- Access: Inject through Program.cs, pass to tools that need it
- Example: If adding element cache persistence, extend `ElementRegistry.cs`

**New Transport:**
- Implementation: Create `src/FlaUI.Mcp/Mcp/{TransportName}Transport.cs`
- Pattern: Implement async `RunAsync(CancellationToken)` method; handle JSON-RPC request parsing; call `McpServer.HandleRequestAsync()`
- Integration: Add CLI arg handling in `Program.cs` to select transport
- Example: See `src/FlaUI.Mcp/Mcp/SseTransport.cs` for HTTP SSE implementation

**New Protocol Message Type:**
- Location: `src/FlaUI.Mcp/Mcp/Protocol.cs`
- Pattern: Add `record` type with `[JsonPropertyName]` attributes; use camelCase property names
- Use case: If extending MCP spec support beyond tools/initialize
- Example: See existing records like `McpToolResult`, `JsonRpcResponse`

**Utilities/Helpers:**
- Location: Depends on usage scope
- Global helpers: Create new file in `src/FlaUI.Mcp/` (e.g., `Extensions.cs`, `Utilities.cs`)
- Tool-specific: Keep in tool file or in a `{Tool}Helpers.cs` file in Tools/
- Core-specific: Add to relevant Core/ class or create `src/FlaUI.Mcp/Core/Helpers.cs`

## Special Directories

**`.gsd/codebase/`:**
- Purpose: Architecture and structure documentation generated by GSD workflow
- Generated: No, manually written by map-codebase command
- Committed: Yes, tracked in git for team reference
- Contents: ARCHITECTURE.md, STRUCTURE.md, and other analysis docs

**`bin/` and `obj/`:**
- Purpose: Build outputs and intermediate files
- Generated: Yes, by `dotnet build`
- Committed: No, excluded by .gitignore
- Note: Safe to delete; will be regenerated on next build

