using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;

namespace AbsCli.Output;

public static class LogSetup
{
    public static void Configure(bool debugEnabled, bool logJson)
    {
        var config = new LoggingConfiguration();

        Layout layout = logJson
            ? new JsonLayout
            {
                Attributes =
                {
                    new JsonAttribute("timestamp", "${date:format=yyyy-MM-ddTHH\\:mm\\:ss.fffZ:universalTime=true}"),
                    new JsonAttribute("level", "${level}"),
                    new JsonAttribute("message", "${message}")
                }
            }
            : new SimpleLayout(
                "${date:format=yyyy-MM-ddTHH\\:mm\\:ss.fffZ:universalTime=true} ${level:uppercase=true:padding=-5} ${message}");

        var target = new ConsoleTarget("stderr")
        {
            StdErr = true,
            Layout = layout
        };
        config.AddTarget(target);

        var minLevel = debugEnabled ? LogLevel.Debug : LogLevel.Warn;
        config.AddRule(minLevel, LogLevel.Fatal, target);

        LogManager.Configuration = config;
    }
}
