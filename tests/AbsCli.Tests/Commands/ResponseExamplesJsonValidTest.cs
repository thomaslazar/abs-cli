using System.Text.Json;
using AbsCli.Commands;

namespace AbsCli.Tests.Commands;

public class ResponseExamplesJsonValidTest
{
    [Fact]
    public void EveryRegisteredSample_ParsesAsJson()
    {
        Assert.NotEmpty(ResponseExamples.All);
        foreach (var (type, json) in ResponseExamples.All)
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
