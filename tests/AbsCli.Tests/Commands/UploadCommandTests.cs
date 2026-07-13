using System.CommandLine;
using AbsCli.Commands;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Commands;

public class UploadCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(UploadCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Theory]
    [InlineData(null, null, null, 1, false)]
    [InlineData(null, null, "m.json", 0, false)]
    [InlineData("S1", "1", null, 1, false)]
    public void ValidateUploadArgs_Valid_ReturnsNull(string? series, string? sequence, string? manifest, int fileCount, bool prefix)
    {
        Assert.Null(UploadCommand.ValidateUploadArgs(series, sequence, manifest, fileCount, prefix));
    }

    [Fact]
    public void ValidateUploadArgs_SequenceWithoutSeries()
    {
        Assert.Equal("--sequence requires --series.",
            UploadCommand.ValidateUploadArgs(null, "1", null, 1, false));
    }

    [Fact]
    public void ValidateUploadArgs_SequenceEmpty()
    {
        Assert.Equal("--sequence must be a non-empty string.",
            UploadCommand.ValidateUploadArgs("S1", "   ", null, 1, false));
    }

    [Fact]
    public void ValidateUploadArgs_FilesAndManifestExclusive()
    {
        Assert.Equal("--files and --files-manifest are mutually exclusive.",
            UploadCommand.ValidateUploadArgs(null, null, "m.json", 2, false));
    }

    [Fact]
    public void ValidateUploadArgs_PrefixAndManifestExclusive()
    {
        Assert.Equal("--prefix-source-dir and --files-manifest are mutually exclusive.",
            UploadCommand.ValidateUploadArgs(null, null, "m.json", 0, true));
    }

    [Fact]
    public void ValidateUploadArgs_NeitherSource()
    {
        Assert.Equal("Pass --files <path>... or --files-manifest <path|->.",
            UploadCommand.ValidateUploadArgs(null, null, null, 0, false));
    }

    [Fact]
    public void ValidateManifestEntries_NullOrEmpty()
    {
        var expected = "Manifest is empty or null. Provide a non-empty array of {src, as} entries.";
        Assert.Equal(expected, UploadCommand.ValidateManifestEntries(null));
        Assert.Equal(expected, UploadCommand.ValidateManifestEntries(new List<UploadManifestEntry>()));
    }

    [Fact]
    public void ValidateManifestEntries_MissingField()
    {
        var entries = new List<UploadManifestEntry> { new() { Src = "a.mp3", TargetName = "" } };
        Assert.Equal("Manifest entry missing 'src' or 'as'. Each entry must have both.",
            UploadCommand.ValidateManifestEntries(entries));
    }

    [Fact]
    public void ValidateManifestEntries_Valid()
    {
        var entries = new List<UploadManifestEntry> { new() { Src = "a.mp3", TargetName = "01.mp3" } };
        Assert.Null(UploadCommand.ValidateManifestEntries(entries));
    }

    [Fact]
    public void DetectDuplicates_NoneReturnsNull()
    {
        var list = new List<(string, string)> { ("/a/1.mp3", "1.mp3"), ("/a/2.mp3", "2.mp3") };
        Assert.Null(UploadCommand.DetectDuplicates(list));
    }

    [Fact]
    public void DetectDuplicates_CaseInsensitiveCollision()
    {
        var list = new List<(string, string)> { ("/a/1.mp3", "Track.mp3"), ("/b/1.mp3", "track.mp3") };
        var msg = UploadCommand.DetectDuplicates(list);
        Assert.NotNull(msg);
        Assert.Contains("Duplicate filenames", msg);
    }

    [Fact]
    public void Upload_Help_ShowsUploadPermissionAndOptions()
    {
        var output = RenderHelp("upload");
        Assert.Contains("Permission required:", output);
        Assert.Contains("upload", output);
        Assert.Contains("--files", output);
        Assert.Contains("--files-manifest", output);
    }
}
