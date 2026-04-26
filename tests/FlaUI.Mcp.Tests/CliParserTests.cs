using Xunit;
using FlaUI.Mcp;

namespace FlaUI.Mcp.Tests;

/// <summary>
/// Unit tests for <see cref="CliOptions"/>. Covers HTTP-08 (default-transport-is-http)
/// and HTTP-06 (default bind = 127.0.0.1, --bind escape hatch) without spinning up Kestrel.
/// </summary>
public class CliParserTests
{
    [Fact]
    public void DefaultTransportIsHttp() // HTTP-08
    {
        Assert.Equal("http", CliOptions.Default.Transport);
    }

    [Fact]
    public void ParseEmptyArgsYieldsDefaultTransport() // HTTP-08
    {
        Assert.Equal("http", CliOptions.Parse(System.Array.Empty<string>()).Transport);
    }

    [Fact]
    public void ParseTransportSseOverridesDefault()
    {
        Assert.Equal("sse", CliOptions.Parse(new[] { "--transport", "sse" }).Transport);
    }

    [Fact]
    public void ParseTransportStdioOverridesDefault()
    {
        Assert.Equal("stdio", CliOptions.Parse(new[] { "--transport", "stdio" }).Transport);
    }

    [Fact]
    public void DefaultPortIs3020()
    {
        Assert.Equal(3020, CliOptions.Default.Port);
    }

    [Fact]
    public void DefaultBindIsLoopback() // HTTP-06
    {
        Assert.Equal("127.0.0.1", CliOptions.Default.BindAddress);
    }

    [Fact]
    public void ParseBindFlagAcceptsAddress() // HTTP-06
    {
        Assert.Equal("0.0.0.0", CliOptions.Parse(new[] { "--bind", "0.0.0.0" }).BindAddress);
    }

    [Fact]
    public void ParseBindFlagAcceptsLoopback() // HTTP-06
    {
        Assert.Equal("127.0.0.1", CliOptions.Parse(new[] { "--bind", "127.0.0.1" }).BindAddress);
    }

    [Fact]
    public void ParsePortFlagYieldsCustomPort()
    {
        Assert.Equal(4000, CliOptions.Parse(new[] { "--port", "4000" }).Port);
    }
}
