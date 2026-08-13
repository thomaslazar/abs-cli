using System.CommandLine;
using System.Text.Json;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class ItemsCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(ItemsCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void ValidateInputSource_Stdin_Ok()
    {
        Assert.Null(ItemsCommand.ValidateInputSource(null, stdin: true, inputIsExistingFile: false));
    }

    [Fact]
    public void ValidateInputSource_ExistingFile_Ok()
    {
        Assert.Null(ItemsCommand.ValidateInputSource("body.json", stdin: false, inputIsExistingFile: true));
    }

    [Fact]
    public void ValidateInputSource_InputNotAFile()
    {
        Assert.Equal("--input must be a file path (got '{\"x\":1}'). For inline JSON, pipe via --stdin.",
            ItemsCommand.ValidateInputSource("{\"x\":1}", stdin: false, inputIsExistingFile: false));
    }

    [Fact]
    public void ValidateInputSource_NeitherProvided()
    {
        Assert.Equal("Provide --input <file> or --stdin",
            ItemsCommand.ValidateInputSource(null, stdin: false, inputIsExistingFile: false));
    }

    [Fact]
    public void Items_HasBaseVerbs()
    {
        var verbs = ItemsCommand.Create().Subcommands.Select(c => c.Name).ToList();
        foreach (var v in new[] { "list", "get", "update", "batch-update", "batch-get", "delete", "batch-delete", "scan" })
            Assert.Contains(v, verbs);
    }

    [Fact]
    public void ItemsUpdate_RequiresUpdatePermission()
    {
        var output = RenderHelp("items", "update").Replace("\r\n", "\n");
        Assert.Contains("Permission required:\n  update", output);
    }

    [Fact]
    public void ItemsScan_RequiresAdminPermission()
    {
        var output = RenderHelp("items", "scan").Replace("\r\n", "\n");
        Assert.Contains("Permission required:\n  admin", output);
    }

    [Fact]
    public void ItemsList_HasNoPermissionSection()
    {
        Assert.DoesNotContain("Permission required:", RenderHelp("items", "list"));
    }

    private static List<string> FileSubVerbs()
    {
        var items = ItemsCommand.Create();
        var file = items.Subcommands.First(c => c.Name == "file");
        return file.Subcommands.Select(c => c.Name).ToList();
    }

    [Fact]
    public void ItemsFile_HasDownloadDeleteFfprobe()
    {
        Assert.Equal(new[] { "download", "delete", "ffprobe" }, FileSubVerbs());
    }

    [Fact]
    public void ItemsFileDownload_RequiresDownloadPermissionAndOptions()
    {
        var output = RenderHelp("items", "file", "download").Replace("\r\n", "\n");
        Assert.Contains("Permission required:\n  download", output);
        Assert.Contains("--id", output);
        Assert.Contains("--ino", output);
        Assert.Contains("--output", output);
    }

    [Fact]
    public void ItemsFileDelete_RequiresDeletePermission_AndWarnsOnDiskDeletion()
    {
        var output = RenderHelp("items", "file", "delete").Replace("\r\n", "\n");
        Assert.Contains("Permission required:\n  delete", output);
        Assert.Contains("disk", output.ToLowerInvariant());
    }

    [Fact]
    public void ItemsFileFfprobe_RequiresAdmin_AndDocumentsAudioOnly()
    {
        var output = RenderHelp("items", "file", "ffprobe").Replace("\r\n", "\n");
        Assert.Contains("Permission required:\n  admin", output);
        Assert.Contains("audio", output.ToLowerInvariant());
    }

    [Fact]
    public void MediaUpdateBody_EmptyObject_IsAccepted()
    {
        // ABS accepts {} — we must not invent a requirement.
        Assert.Equal("{}", ItemsCommand.PrepareMediaUpdateBody("{}"));
    }

    [Fact]
    public void MediaUpdateBody_UnknownField_IsForwardedUnchanged()
    {
        const string body = "{\"metadata\":{\"title\":\"T\"},\"somethingNew\":1}";
        Assert.Equal(body, ItemsCommand.PrepareMediaUpdateBody(body));
    }

    [Fact]
    public void MediaUpdateBody_Malformed_Throws()
    {
        Assert.ThrowsAny<JsonException>(() => ItemsCommand.PrepareMediaUpdateBody("{not json"));
    }

    [Fact]
    public void BatchUpdateBody_Valid_IsForwardedUnchanged()
    {
        const string body = "[{\"id\":\"li_a\",\"tags\":[\"x\"]}]";
        Assert.Equal(body, ItemsCommand.PrepareBatchUpdateBody(body));
    }

    [Fact]
    public void BatchUpdateBody_EmptyArray_Rejected()
    {
        Assert.Throws<ArgumentException>(() => ItemsCommand.PrepareBatchUpdateBody("[]"));
    }

    [Fact]
    public void BatchUpdateBody_DuplicateIds_Rejected()
    {
        Assert.Throws<ArgumentException>(
            () => ItemsCommand.PrepareBatchUpdateBody("[{\"id\":\"li_a\"},{\"id\":\"li_a\"}]"));
    }

    [Fact]
    public void BatchUpdateBody_MissingId_Rejected()
    {
        Assert.Throws<ArgumentException>(() => ItemsCommand.PrepareBatchUpdateBody("[{\"tags\":[\"x\"]}]"));
    }

    [Fact]
    public void BatchUpdateBody_Malformed_Throws()
    {
        Assert.ThrowsAny<JsonException>(() => ItemsCommand.PrepareBatchUpdateBody("[{"));
    }

    [Fact]
    public void LibraryItemIdsBody_Valid_IsForwardedUnchanged()
    {
        const string body = "{\"libraryItemIds\":[\"li_a\",\"li_b\"]}";
        Assert.Equal(body, ItemsCommand.PrepareLibraryItemIdsBody(body));
    }

    [Fact]
    public void LibraryItemIdsBody_EmptyArray_Rejected()
    {
        Assert.Throws<ArgumentException>(
            () => ItemsCommand.PrepareLibraryItemIdsBody("{\"libraryItemIds\":[]}"));
    }

    [Fact]
    public void LibraryItemIdsBody_MissingField_Rejected()
    {
        Assert.Throws<ArgumentException>(() => ItemsCommand.PrepareLibraryItemIdsBody("{}"));
    }

    [Fact]
    public void LibraryItemIdsBody_Malformed_Throws()
    {
        Assert.ThrowsAny<JsonException>(() => ItemsCommand.PrepareLibraryItemIdsBody("{\"libraryItemIds\":["));
    }

    [Fact]
    public void BatchUpdateProgressBody_Valid_IsForwardedUnchanged()
    {
        const string body = "[{\"libraryItemId\":\"li_a\",\"isFinished\":true}]";
        Assert.Equal(body, ItemsCommand.PrepareBatchUpdateProgressBody(body));
    }

    [Fact]
    public void BatchUpdateProgressBody_EmptyArray_Rejected()
    {
        Assert.Throws<ArgumentException>(() => ItemsCommand.PrepareBatchUpdateProgressBody("[]"));
    }

    [Fact]
    public void BatchUpdateProgressBody_Malformed_Throws()
    {
        Assert.ThrowsAny<JsonException>(() => ItemsCommand.PrepareBatchUpdateProgressBody("[{"));
    }

    [Fact]
    public void BatchUpdate_HelpFull_ShowsRequestShape()
    {
        var output = RenderHelp("items", "batch-update");
        Assert.Contains("Request shape:", output);
        Assert.Contains("\"id\"", output);
    }

    [Fact]
    public void BatchGet_HelpFull_ShowsRequestShape()
    {
        var output = RenderHelp("items", "batch-get");
        Assert.Contains("Request shape:", output);
        Assert.Contains("\"libraryItemIds\"", output);
    }

    [Fact]
    public void BatchDelete_HelpFull_ShowsRequestShape()
    {
        var output = RenderHelp("items", "batch-delete");
        Assert.Contains("Request shape:", output);
        Assert.Contains("\"libraryItemIds\"", output);
    }

    [Fact]
    public void BatchUpdateProgress_HelpFull_ShowsRequestShape()
    {
        var output = RenderHelp("items", "batch-update-progress");
        Assert.Contains("Request shape:", output);
        Assert.Contains("\"currentTime\"", output);
    }

    [Fact]
    public void BatchEmbedMetadata_HelpFull_ShowsRequestShape()
    {
        var output = RenderHelp("items", "batch-embed-metadata");
        Assert.Contains("Request shape:", output);
        Assert.Contains("\"libraryItemIds\"", output);
    }
}
