# Testing Patterns

**Analysis Date:** 2026-03-17

## Test Framework

**Status:** No test framework detected

**Current State:**
- No xUnit, NUnit, MSTest dependencies in `.csproj`
- No test project files found in repository
- No `*.Test.cs`, `*.Tests.cs`, or `[Test]` directories
- No test runners configured in CI/CD pipeline

**Implications:**
- Code is tested manually against live Windows UI Automation targets
- No automated regression suite
- Tools are integration-tested via MCP client (testing against real applications)

## Recommended Testing Approach

**For Future Implementation:**

**Test Framework Recommendation:** xUnit + Moq
- Reason: Modern, async-first, ideal for .NET 8.0 integration tests
- Alternative: NUnit if attribute-based approach preferred

**Test Structure:**
```
src/
├── FlaUI.Mcp/
├── FlaUI.Mcp.Tests/
│   ├── Tools/
│   │   ├── ClickToolTests.cs
│   │   ├── TypeToolTests.cs
│   │   └── LaunchToolTests.cs
│   ├── Core/
│   │   ├── SessionManagerTests.cs
│   │   ├── ElementRegistryTests.cs
│   │   └── SnapshotBuilderTests.cs
│   ├── Mcp/
│   │   ├── McpServerTests.cs
│   │   └── ToolRegistryTests.cs
│   └── Fixtures/
│       ├── MockAutomationElements.cs
│       └── TestWindowFixture.cs
```

## What Could Be Tested (Current Gaps)

**Core Logic — High Priority:**

**ElementRegistry:**
- `Register()` generates unique refs scoped to windows
- `ClearWindow()` removes all elements for a window
- `GetElement()` retrieves registered elements
- `HasElement()` checks existence

*Example test structure:*
```csharp
public class ElementRegistryTests
{
    [Fact]
    public void Register_GeneratesUniqueScopedRefs()
    {
        var registry = new ElementRegistry();
        var element1 = Mock.Of<AutomationElement>();
        var element2 = Mock.Of<AutomationElement>();

        var ref1 = registry.Register("w1", element1);
        var ref2 = registry.Register("w1", element2);

        Assert.Equal("w1e1", ref1);
        Assert.Equal("w1e2", ref2);
    }

    [Fact]
    public void ClearWindow_RemovesOnlyWindowElements()
    {
        var registry = new ElementRegistry();
        var elem1 = Mock.Of<AutomationElement>();
        var elem2 = Mock.Of<AutomationElement>();

        registry.Register("w1", elem1);
        registry.Register("w2", elem2);
        registry.ClearWindow("w1");

        Assert.Null(registry.GetElement("w1e1"));
        Assert.NotNull(registry.GetElement("w2e1"));
    }
}
```

**SnapshotBuilder:**
- Role mapping: ControlType → accessibility role names
- State detection: checked, disabled, readonly, selected, expanded
- Element filtering: skips decorative elements, keeps actionable ones
- Name escaping: handles special characters in element names

*Example test structure:*
```csharp
public class SnapshotBuilderTests
{
    [Theory]
    [InlineData(ControlType.Button, "button")]
    [InlineData(ControlType.Edit, "textbox")]
    [InlineData(ControlType.CheckBox, "checkbox")]
    public void GetElementRole_MapsControlTypesToRoles(ControlType controlType, string expectedRole)
    {
        var builder = new SnapshotBuilder(new ElementRegistry());
        // Would need reflection or internal accessor to test private method
        // Or refactor to public for testability
    }

    [Fact]
    public void EscapeName_HandlesSpecialCharacters()
    {
        var builder = new SnapshotBuilder(new ElementRegistry());
        var escaped = builder.EscapeName("Quote\" and\nNewline");
        Assert.Equal("Quote\\\" and\\nNewline", escaped);
    }
}
```

**ToolRegistry:**
- Tool registration and retrieval
- Error handling for unknown tools
- Async execution with exception wrapping

*Example test structure:*
```csharp
public class ToolRegistryTests
{
    [Fact]
    public async Task ExecuteToolAsync_ReturnsErrorForUnknownTool()
    {
        var registry = new ToolRegistry();
        var result = await registry.ExecuteToolAsync("unknown_tool", null);

        Assert.True(result.IsError);
        Assert.Contains("Unknown tool", result.Content[0].Text);
    }

    [Fact]
    public async Task ExecuteToolAsync_WrapsToolExceptions()
    {
        var registry = new ToolRegistry();
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("test_tool");
        mockTool.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement?>()))
            .ThrowsAsync(new InvalidOperationException("Tool failed"));

        registry.RegisterTool(mockTool.Object);

        var result = await registry.ExecuteToolAsync("test_tool", null);
        Assert.True(result.IsError);
        Assert.Contains("Tool failed", result.Content[0].Text);
    }
}
```

**McpServer:**
- Request parsing and method dispatch
- Response formatting
- Error response generation

*Example test structure:*
```csharp
public class McpServerTests
{
    [Fact]
    public async Task HandleRequestAsync_DispatchesToCorrectMethod()
    {
        var registry = new Mock<ToolRegistry>();
        var server = new McpServer(registry.Object);

        var request = new JsonRpcRequest
        {
            Id = JsonSerializer.SerializeToElement(1),
            Method = "tools/list",
            Params = null
        };

        var response = await server.HandleRequestAsync(request);

        Assert.NotNull(response);
        Assert.Equal(1, response.Id.GetInt32());
    }
}
```

**Tool Tests:**
- `LaunchTool`: Launches applications, handles path resolution
- `ClickTool`: Pattern fallback (Invoke → Toggle → SelectionItem → Mouse)
- `TypeTool`: Text input and focus
- `SnapshotTool`: Window capture and element registration

## Integration Testing Approach

**Current Testing Method:** Manual against live UI

**Recommended Approach:**
- Test against mock `AutomationElement` instances
- Mock patterns (Invoke, Toggle, Value, SelectionItem)
- Mock window hierarchy
- Test error paths (missing elements, unsupported patterns)

**Example Mock Factory:**
```csharp
public static class MockElementFactory
{
    public static Mock<AutomationElement> CreateButton(string name = "Test Button")
    {
        var mock = new Mock<AutomationElement>();
        mock.Setup(e => e.Properties.Name.ValueOrDefault).Returns(name);
        mock.Setup(e => e.Properties.ControlType.ValueOrDefault)
            .Returns(ControlType.Button);
        mock.Setup(e => e.Patterns.Invoke.IsSupported).Returns(true);
        mock.Setup(e => e.Patterns.Invoke.Pattern).Returns(
            new Mock<IInvokePattern>().Object
        );
        return mock;
    }
}
```

## Test Organization

**Location Strategy (Not Yet Implemented):**
- Separate test project: `FlaUI.Mcp.Tests`
- Co-locate tests near implementation for navigation
- One test class per production class
- Fixture directory for shared test utilities

**Naming Convention (Recommended):**
- Test class: `[ClassUnderTest]Tests`
- Test method: `[MethodName]_[Condition]_[ExpectedResult]`
- Examples: `Register_GeneratesUniqueScopedRefs()`, `ExecuteToolAsync_ReturnsErrorForUnknownTool()`

## Coverage Gaps (High Risk)

**SessionManager.LaunchApp():**
- Complex window discovery logic with fallbacks
- Timing-dependent (Process.WaitForInputIdle, Thread.Sleep)
- UWP app handling (spawns different process)
- Currently all manual testing

**SnapshotBuilder:**
- 250+ lines with deep UI tree traversal
- Role mapping for 30+ ControlTypes
- State detection (8+ state types)
- Element filtering heuristics
- No coverage for malformed/nested trees

**ErrorHandling:**
- Silent catches hide real issues
- Exception wrapping in tools needs validation
- Transport layer (SSE) concurrent access patterns untested

**Current Workarounds (Fragile):**
- `Thread.Sleep(1000)` for window detection
- `catch { }` blocks that swallow errors
- No retry logic for transient failures

## Recommended Phased Testing Strategy

**Phase 1: Unit Tests (High ROI)**
- ElementRegistry (simple, isolated)
- ToolRegistry (straightforward dispatch logic)
- Protocol types (JSON serialization)
- Snapshot role mapping

**Phase 2: Integration Tests**
- McpServer with mocked tools
- Tool base class utilities (ErrorResult, TextResult, ImageResult)
- Transport layer with in-memory endpoints

**Phase 3: End-to-End Tests**
- SSE transport with real HTTP client
- Session lifecycle (launch, snapshot, interact, close)
- Multi-window scenarios

---

*Testing analysis: 2026-03-17*

**Note:** This codebase is in early stage (v0.1.0) with no test infrastructure. The patterns above represent best-practice recommendations for adding tests incrementally.
