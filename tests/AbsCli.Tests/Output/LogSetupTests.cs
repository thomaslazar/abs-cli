using AbsCli.Output;
using NLog;
using NLog.Layouts;
using NLog.Targets;
using Xunit;

namespace AbsCli.Tests.Output;

public class LogSetupTests
{
    [Fact]
    public void Configure_DefaultLevel_AllowsWarnAndError_BlocksDebug()
    {
        LogSetup.Configure(debugEnabled: false, logJson: false);
        var rule = LogManager.Configuration!.LoggingRules[0];
        Assert.Contains(LogLevel.Warn, rule.Levels);
        Assert.Contains(LogLevel.Error, rule.Levels);
        Assert.Contains(LogLevel.Fatal, rule.Levels);
        Assert.DoesNotContain(LogLevel.Debug, rule.Levels);
        Assert.DoesNotContain(LogLevel.Info, rule.Levels);
    }

    [Fact]
    public void Configure_DebugLevel_AllowsAllLevels()
    {
        LogSetup.Configure(debugEnabled: true, logJson: false);
        var rule = LogManager.Configuration!.LoggingRules[0];
        Assert.Contains(LogLevel.Debug, rule.Levels);
        Assert.Contains(LogLevel.Info, rule.Levels);
        Assert.Contains(LogLevel.Warn, rule.Levels);
        Assert.Contains(LogLevel.Error, rule.Levels);
        Assert.Contains(LogLevel.Fatal, rule.Levels);
    }

    [Fact]
    public void Configure_TextLayout_UsesSimpleLayout()
    {
        LogSetup.Configure(debugEnabled: false, logJson: false);
        var target = (ConsoleTarget)LogManager.Configuration!.LoggingRules[0].Targets[0];
        Assert.IsType<SimpleLayout>(target.Layout);
        var layout = (SimpleLayout)target.Layout;
        Assert.Contains("yyyy-MM-ddTHH", layout.Text);
        Assert.Contains(".fffZ", layout.Text);
        Assert.Contains("${level:uppercase=true:padding=-5}", layout.Text);
        Assert.Contains("${message}", layout.Text);
    }

    [Fact]
    public void Configure_JsonLayout_HasThreeAttributes()
    {
        LogSetup.Configure(debugEnabled: false, logJson: true);
        var target = (ConsoleTarget)LogManager.Configuration!.LoggingRules[0].Targets[0];
        Assert.IsType<JsonLayout>(target.Layout);
        var layout = (JsonLayout)target.Layout;
        Assert.Equal(3, layout.Attributes.Count);
        Assert.Contains(layout.Attributes, a => a.Name == "timestamp");
        Assert.Contains(layout.Attributes, a => a.Name == "level");
        Assert.Contains(layout.Attributes, a => a.Name == "message");
    }

    [Fact]
    public void Configure_TargetIsConsoleStdErr()
    {
        LogSetup.Configure(debugEnabled: false, logJson: false);
        var target = (ConsoleTarget)LogManager.Configuration!.LoggingRules[0].Targets[0];
        Assert.Equal("True", target.StdErr!.ToString()!);
    }
}
