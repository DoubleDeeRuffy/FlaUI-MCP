using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using PlaywrightWindows.Mcp;

namespace FlaUI.Mcp.Mcp.Http;

/// <summary>
/// Adapts the existing hand-rolled <see cref="ITool"/> registry instances to SDK
/// <see cref="McpServerTool"/> instances at runtime, so all 11 FlaUI tools are
/// reachable over the SDK's Streamable HTTP <c>/mcp</c> endpoint without rewriting
/// any tool with <c>[McpServerTool]</c> attributes (D-01 adapter path).
/// </summary>
/// <remarks>
/// The real <see cref="ITool"/> surface differs from the planning assumption:
/// tools expose <c>GetDefinition() : McpTool</c> (carrying Name/Description/InputSchema)
/// and <c>ExecuteAsync(JsonElement?) : Task&lt;McpToolResult&gt;</c>. The bridge
/// projects each tool's <c>InputSchema</c> (an arbitrary CLR object that serializes to
/// a JSON-Schema document) through <c>JsonSerializer</c> into a <see cref="JsonElement"/>
/// expected by <see cref="McpServerTool.Create"/>, and converts the
/// <see cref="McpToolResult"/> back into the SDK's <see cref="CallToolResult"/>
/// shape (text + image content blocks).
/// </remarks>
public static class ToolBridge
{
    public static IEnumerable<McpServerTool> CreateAll(ToolRegistry registry)
    {
        foreach (var def in registry.GetToolDefinitions())
        {
            yield return CreateOne(def, registry);
        }
    }

    private static McpServerTool CreateOne(McpTool def, ToolRegistry registry)
    {
        var name = def.Name;
        var description = def.Description;
        var inputSchema = ToJsonElement(def.InputSchema);

        // Capture by closure: each invocation routes through the existing registry
        // which preserves error handling, ElementRegistry/SessionManager DI, and the
        // McpToolResult shape used over the legacy /sse path.
        // SDK 1.2.0 binds handler parameters from the JSON-RPC `arguments` dictionary by
        // parameter name. Accepting `RequestContext<CallToolRequestParams>` is auto-injected
        // by the SDK and gives us the raw arguments dictionary so the bridge can pass it
        // through unchanged to the existing ITool surface.
        Delegate handler = async (RequestContext<CallToolRequestParams> ctx, CancellationToken ct) =>
        {
            try
            {
                JsonElement? args = null;
                var raw = ctx.Params?.Arguments;
                if (raw is { Count: > 0 })
                {
                    var obj = new JsonObject();
                    foreach (var kvp in raw)
                    {
                        obj[kvp.Key] = JsonNode.Parse(kvp.Value.GetRawText());
                    }
                    args = JsonDocument.Parse(obj.ToJsonString()).RootElement.Clone();
                }

                var result = await registry.ExecuteToolAsync(name, args);
                return ConvertToCallToolResult(result);
            }
            catch (Exception ex)
            {
                return new CallToolResult
                {
                    Content = new List<ContentBlock> { new TextContentBlock { Text = $"{ex.GetType().Name}: {ex.Message}" } },
                    IsError = true,
                };
            }
        };

        return McpServerTool.Create(handler, new McpServerToolCreateOptions
        {
            Name = name,
            Description = description,
        });
    }

    private static JsonElement ToJsonElement(object schema)
    {
        if (schema is JsonElement je) return je;
        var json = JsonSerializer.Serialize(schema, McpProtocol.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static CallToolResult ConvertToCallToolResult(McpToolResult result)
    {
        var contents = new List<ContentBlock>();
        foreach (var c in result.Content)
        {
            if (c.Type == "image" && c.Data != null)
            {
                contents.Add(new ImageContentBlock
                {
                    Data = Convert.FromBase64String(c.Data),
                    MimeType = c.MimeType ?? "image/png",
                });
            }
            else
            {
                contents.Add(new TextContentBlock { Text = c.Text ?? string.Empty });
            }
        }

        return new CallToolResult
        {
            Content = contents,
            IsError = result.IsError ?? false,
        };
    }
}
