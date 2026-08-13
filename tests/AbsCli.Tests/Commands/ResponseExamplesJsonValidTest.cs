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
}
