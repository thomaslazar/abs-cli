using System.CommandLine;
using AbsCli.Commands;
using AbsCli.Configuration;
using Xunit;

namespace AbsCli.Tests.Commands;

public class ConfigCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(ConfigCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void ApplyConfigSet_Server()
    {
        var config = new AppConfig();
        Assert.Null(ConfigCommand.ApplyConfigSet(config, "server", "https://abs.example.com"));
        Assert.Equal("https://abs.example.com", config.Server);
    }

    [Fact]
    public void ApplyConfigSet_DefaultLibrary()
    {
        var config = new AppConfig();
        Assert.Null(ConfigCommand.ApplyConfigSet(config, "defaultLibrary", "lib_abc"));
        Assert.Equal("lib_abc", config.DefaultLibrary);
    }

    [Fact]
    public void ApplyConfigSet_UnknownKey_ReturnsErrorAndLeavesConfig()
    {
        var config = new AppConfig { Server = "orig" };
        var err = ConfigCommand.ApplyConfigSet(config, "bogus", "x");
        Assert.Equal("Unknown config key: 'bogus'. Valid keys: server, defaultLibrary", err);
        Assert.Equal("orig", config.Server);
    }

    [Fact]
    public void Config_HasGetAndSet()
    {
        var verbs = ConfigCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "get", "set" }, verbs);
    }

    [Fact]
    public void ConfigSet_Help_ShowsPositionalArgs()
    {
        var output = RenderHelp("config", "set");
        Assert.Contains("key", output);
        Assert.Contains("value", output);
    }
}
