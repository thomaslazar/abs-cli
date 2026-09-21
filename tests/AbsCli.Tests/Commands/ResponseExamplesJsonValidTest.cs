using System.Text.Json;
using AbsCli.Commands;

namespace AbsCli.Tests.Commands;

public class ResponseExamplesJsonValidTest
{
    [Fact]
    public void EveryRegisteredSample_ParsesAsJson()
    {
        Assert.NotEmpty(JsonExamples.All);
        foreach (var (type, json) in JsonExamples.All)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                Assert.Fail($"Sample for {type.FullName} is not valid JSON: {ex.Message}\n{json}");
            }
        }
    }

    [Fact]
    public void BatchUpdateEntrySample_WrapsThePayloadInMediaPayload()
    {
        // The generated --help-full block is what callers copy. ABS reads the
        // media payload from a "mediaPayload" key (LibraryItemController.js:665);
        // a sample without it produces a body that crashes the server, which is
        // how #93 happened. Pin the wrapper here, not just on the type.
        var sample = JsonExamples.For(typeof(List<AbsCli.Models.ItemsBatchUpdateEntry>));
        Assert.Contains("\"mediaPayload\"", sample);
        Assert.Contains("\"id\"", sample);
    }
}
