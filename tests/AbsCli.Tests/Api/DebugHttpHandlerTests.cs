using System.Net;
using System.Text;
using AbsCli.Api;
using NLog;
using NLog.Layouts;
using NLog.Targets;
using Xunit;

namespace AbsCli.Tests.Api;

[Collection("NLog")]
public class DebugHttpHandlerTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public string ResponseBody { get; init; } = "";

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(Status)
            {
                Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private static MemoryTarget ConfigureMemoryTarget(bool debugEnabled)
    {
        var config = new NLog.Config.LoggingConfiguration();
        var target = new MemoryTarget("memory")
        {
            Layout = new SimpleLayout("${level:uppercase=true} ${message}")
        };
        config.AddTarget(target);
        config.AddRule(debugEnabled ? LogLevel.Debug : LogLevel.Warn, LogLevel.Fatal, target);
        LogManager.Configuration = config;
        return target;
    }

    [Fact]
    public async Task DebugOff_NoLines()
    {
        var target = ConfigureMemoryTarget(debugEnabled: false);
        var handler = new DebugHttpHandler(new StubHandler { Status = HttpStatusCode.OK, ResponseBody = "{}" });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
        await client.GetAsync("foo");
        Assert.Empty(target.Logs);
    }

    [Fact]
    public async Task DebugOn_2xx_OneLineWithMethodUrlStatus()
    {
        var target = ConfigureMemoryTarget(debugEnabled: true);
        var handler = new DebugHttpHandler(new StubHandler { Status = HttpStatusCode.OK, ResponseBody = "{}" });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/sub/") };
        await client.GetAsync("api/items?expanded=1");
        Assert.Single(target.Logs);
        Assert.Equal("DEBUG GET https://example.com/sub/api/items?expanded=1 200", target.Logs[0]);
    }

    [Fact]
    public async Task DebugOn_Non2xx_TwoLinesIncludingResponseBody()
    {
        var target = ConfigureMemoryTarget(debugEnabled: true);
        var handler = new DebugHttpHandler(new StubHandler { Status = HttpStatusCode.BadRequest, ResponseBody = "{\"error\":\"nope\"}" });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
        await client.PatchAsync("api/items/foo", new StringContent(""));
        Assert.Equal(2, target.Logs.Count);
        Assert.Equal("DEBUG PATCH https://example.com/api/items/foo 400", target.Logs[0]);
        Assert.Equal("DEBUG response body: {\"error\":\"nope\"}", target.Logs[1]);
    }

    [Fact]
    public async Task DebugOn_Non2xx_LongBody_TruncatedAt500()
    {
        var target = ConfigureMemoryTarget(debugEnabled: true);
        var longBody = new string('x', 600);
        var handler = new DebugHttpHandler(new StubHandler { Status = HttpStatusCode.InternalServerError, ResponseBody = longBody });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
        await client.GetAsync("api/items");
        Assert.Equal(2, target.Logs.Count);
        Assert.Equal($"DEBUG response body: {new string('x', 500)}...", target.Logs[1]);
    }
}
