using System.CommandLine;
using AbsCli.Commands;

namespace AbsCli.Tests.Commands;

public class HelpExtensionsTests
{
    private static string RenderHelp(Command command)
    {
        var root = new RootCommand { command };
        HelpExtensions.UseCustomHelpSections(root);
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        root.Parse(new[] { command.Name, "--help" }).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void TopSection_RendersBeforeOptions()
    {
        var cmd = new Command("demo", "Demo command");
        cmd.AddHelpSection("Notes", HelpSectionPosition.Top, "Top-placed content");
        var output = RenderHelp(cmd);
        var notesIdx = output.IndexOf("Notes:", StringComparison.Ordinal);
        var optionsIdx = output.IndexOf("Options:", StringComparison.Ordinal);
        Assert.True(notesIdx >= 0, "Notes section missing");
        Assert.True(optionsIdx >= 0, "Options section missing");
        Assert.True(notesIdx < optionsIdx, "Notes should render before Options");
    }

    [Fact]
    public void BottomSection_RendersAfterOptions()
    {
        var cmd = new Command("demo", "Demo command");
        cmd.AddHelpSection("Examples", HelpSectionPosition.Bottom, "abs-cli demo");
        var output = RenderHelp(cmd);
        var examplesIdx = output.IndexOf("Examples:", StringComparison.Ordinal);
        var optionsIdx = output.IndexOf("Options:", StringComparison.Ordinal);
        Assert.True(examplesIdx > optionsIdx, "Examples should render after Options");
    }

    [Fact]
    public void ExistingOverload_DefaultsToBottom()
    {
        var cmd = new Command("demo", "Demo command");
        cmd.AddHelpSection("Examples", "abs-cli demo");
        var output = RenderHelp(cmd);
        var examplesIdx = output.IndexOf("Examples:", StringComparison.Ordinal);
        var optionsIdx = output.IndexOf("Options:", StringComparison.Ordinal);
        Assert.True(examplesIdx > optionsIdx, "Default overload must remain Bottom-placed");
    }

    [Fact]
    public void AddResponseExample_Generic_RendersResponseShapeSection()
    {
        var cmd = new Command("demo", "Demo");
        cmd.AddResponseExample<AbsCli.Models.AuthorItem>();
        var output = RenderHelp(cmd);
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"numBooks\"", output);
    }

    [Fact]
    public void AddResponseExample_EnvelopeAndElement_SubstitutesResultsArray()
    {
        var cmd = new Command("demo", "Demo");
        cmd.AddResponseExample(
            typeof(AbsCli.Models.PaginatedResponse),
            typeof(AbsCli.Models.LibraryItemMinified));
        var output = RenderHelp(cmd);
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"results\"", output);
        Assert.Contains("\"mediaType\"", output);
    }
}
