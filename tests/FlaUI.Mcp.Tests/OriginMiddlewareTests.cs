using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Xunit;

namespace FlaUI.Mcp.Tests;

/// <summary>
/// HTTP-07: Origin-header allowlist enforced for /mcp. External origins → 403;
/// localhost / 127.0.0.1 / "null" / absent → not 403.
/// </summary>
public class OriginMiddlewareTests : IClassFixture<HttpTransportFixture>
{
    private readonly HttpTransportFixture _fx;

    public OriginMiddlewareTests(HttpTransportFixture fx) => _fx = fx;

    [Fact]
    public async Task RejectsExternalOrigin()
    {
        Assert.Equal(HttpStatusCode.Forbidden, await PostInitializeWithOriginAsync("https://evil.example.com"));
        Assert.NotEqual(HttpStatusCode.Forbidden, await PostInitializeWithOriginAsync("http://127.0.0.1:3020"));
        Assert.NotEqual(HttpStatusCode.Forbidden, await PostInitializeWithOriginAsync("http://localhost:3020"));
        Assert.NotEqual(HttpStatusCode.Forbidden, await PostInitializeWithOriginAsync(originHeader: null));
        Assert.NotEqual(HttpStatusCode.Forbidden, await PostInitializeWithOriginAsync("null"));
    }

    private async Task<HttpStatusCode> PostInitializeWithOriginAsync(string? originHeader)
    {
        using var client = new HttpClient { BaseAddress = new Uri(_fx.BaseUrl) };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var body = """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"origin-test","version":"1"}}}
            """;
        var req = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (originHeader != null)
        {
            req.Headers.Add("Origin", originHeader);
        }

        using var resp = await client.SendAsync(req);
        return resp.StatusCode;
    }
}
