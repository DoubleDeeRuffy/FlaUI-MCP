using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using FlaUI.Mcp;

namespace FlaUI.Mcp.Tests;

/// <summary>
/// Tests for <see cref="ConfigResolver"/> — verifies precedence:
/// defaults &lt; appsettings.json &lt; FLAUI_MCP_ env &lt; CLI.
/// All tests are pure-function — no real filesystem next to the exe, no real env vars.
/// File-based tests use <see cref="Path.GetTempPath"/>; env-based tests use
/// <c>AddInMemoryCollection</c> via the <c>envOverride</c> dict (already-translated keys).
/// </summary>
public class ConfigResolverTests
{
    private static string WriteTempAppsettings(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"flaui-mcp-appsettings-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact] // Scenario 1: defaults
    public void NoFileNoEnvNoCli_YieldsDefaults()
    {
        var (bind, port) = ConfigResolver.Resolve(
            defaults: CliOptions.Default,
            appsettingsPath: null,
            envOverride: new Dictionary<string, string?>(),
            cli: new CliOverrides(BindAddress: null, Port: null));
        Assert.Equal("127.0.0.1", bind);
        Assert.Equal(3020, port);
    }

    [Fact] // Scenario 2: file only
    public void FileOnly_AppliesFileValues()
    {
        var path = WriteTempAppsettings("""{"Server":{"BindAddress":"10.0.0.5","Port":4040}}""");
        try
        {
            var (bind, port) = ConfigResolver.Resolve(
                defaults: CliOptions.Default,
                appsettingsPath: path,
                envOverride: new Dictionary<string, string?>(),
                cli: new CliOverrides(null, null));
            Assert.Equal("10.0.0.5", bind);
            Assert.Equal(4040, port);
        }
        finally { File.Delete(path); }
    }

    [Fact] // Scenario 3: CLI only, no file
    public void CliOnly_NoFile_AppliesCliValues()
    {
        var (bind, port) = ConfigResolver.Resolve(
            defaults: CliOptions.Default,
            appsettingsPath: null,
            envOverride: new Dictionary<string, string?>(),
            cli: new CliOverrides(BindAddress: "0.0.0.0", Port: 5000));
        Assert.Equal("0.0.0.0", bind);
        Assert.Equal(5000, port);
    }

    [Fact] // Scenario 4: CLI beats file — CRITICAL CONTEXT requirement
    public void CliBeatsFile_EvenWhenCliMatchesDefault()
    {
        // --bind 127.0.0.1 (matches default) MUST still beat appsettings.json: 0.0.0.0
        // because precedence is "flag was passed", not "value differs from default".
        var path = WriteTempAppsettings("""{"Server":{"BindAddress":"0.0.0.0","Port":4040}}""");
        try
        {
            var (bind, port) = ConfigResolver.Resolve(
                defaults: CliOptions.Default,
                appsettingsPath: path,
                envOverride: new Dictionary<string, string?>(),
                cli: new CliOverrides(BindAddress: "127.0.0.1", Port: null));
            Assert.Equal("127.0.0.1", bind); // CLI wins
            Assert.Equal(4040, port);         // file wins (CLI didn't pass --port)
        }
        finally { File.Delete(path); }
    }

    [Fact] // Scenario 5: env beats file but loses to CLI
    public void EnvBeatsFileButLosesToCli()
    {
        var path = WriteTempAppsettings("""{"Server":{"Port":2000}}""");
        try
        {
            var (_, port) = ConfigResolver.Resolve(
                defaults: CliOptions.Default,
                appsettingsPath: path,
                envOverride: new Dictionary<string, string?> { ["Server:Port"] = "3000" },
                cli: new CliOverrides(BindAddress: null, Port: 4000));
            Assert.Equal(4000, port); // CLI > env > file
        }
        finally { File.Delete(path); }
    }

    [Fact] // Scenario 5b: env beats file with no CLI
    public void EnvBeatsFile_NoCli()
    {
        var path = WriteTempAppsettings("""{"Server":{"Port":2000}}""");
        try
        {
            var (_, port) = ConfigResolver.Resolve(
                defaults: CliOptions.Default,
                appsettingsPath: path,
                envOverride: new Dictionary<string, string?> { ["Server:Port"] = "3000" },
                cli: new CliOverrides(null, null));
            Assert.Equal(3000, port);
        }
        finally { File.Delete(path); }
    }

    [Fact] // Edge: malformed port falls through silently
    public void MalformedFilePort_FallsThroughToDefault()
    {
        var path = WriteTempAppsettings("""{"Server":{"Port":"not-a-number"}}""");
        try
        {
            var (_, port) = ConfigResolver.Resolve(
                defaults: CliOptions.Default,
                appsettingsPath: path,
                envOverride: new Dictionary<string, string?>(),
                cli: new CliOverrides(null, null));
            Assert.Equal(3020, port); // falls back to default, no exception
        }
        finally { File.Delete(path); }
    }

    [Fact] // Edge: missing file silent fallback (optional:true behavior)
    public void MissingFile_SilentFallback()
    {
        var bogusPath = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.json");
        var (bind, port) = ConfigResolver.Resolve(
            defaults: CliOptions.Default,
            appsettingsPath: bogusPath,
            envOverride: new Dictionary<string, string?>(),
            cli: new CliOverrides(null, null));
        Assert.Equal("127.0.0.1", bind);
        Assert.Equal(3020, port);
    }
}
