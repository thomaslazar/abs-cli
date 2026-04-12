using System.CommandLine;
using AbsCli.Models;
using AbsCli.Output;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class UploadCommand
{
    public static Command Create()
    {
        var libraryOption = new Option<string?>("--library", "Library ID or name");
        var folderOption = new Option<string?>("--folder", "Folder ID (auto-resolved if library has one folder)");
        var titleOption = new Option<string>("--title", "Book title") { IsRequired = true };
        var authorOption = new Option<string?>("--author", "Book author");
        var seriesOption = new Option<string?>("--series", "Series name");
        var sequenceOption = new Option<int?>("--sequence", "Series sequence number (requires --series)");
        var waitOption = new Option<bool>("--wait", "Poll until the uploaded item appears in the library");
        var filesOption = new Option<string[]>("--files", "File paths to upload") { IsRequired = true, AllowMultipleArgumentsPerToken = true };
        var command = new Command("upload", "Upload audiobook files to a library")
        {
            libraryOption, folderOption, titleOption, authorOption,
            seriesOption, sequenceOption, waitOption, filesOption
        };
        command.AddHelpSection("Folder ID",
            "If the library has a single folder, it is auto-resolved.",
            "Otherwise pass --folder <id>.",
            "Run 'abs-cli libraries get --id <id>' to see folder IDs.");
        command.AddHelpSection("Folder structure created",
            "Author/Title            — when --author is given",
            "Author/Series/Title     — when --author and --series are given",
            "Title                   — when no --author is given");
        command.AddExamples(
            "abs-cli upload --title \"The Hobbit\" --author \"J.R.R. Tolkien\" --files hobbit.m4b",
            "abs-cli upload --title \"The Final Empire\" --author \"Brandon Sanderson\" --series \"Mistborn\" --sequence 1 --files part1.mp3 part2.mp3",
            "abs-cli upload --title \"My Audiobook\" --files ch1.mp3 ch2.mp3 ch3.mp3 --wait");
        command.SetHandler(async (string? library, string? folder, string title,
            string? author, string? series, int? sequence, bool wait, string[] files) =>
        {
            if (sequence.HasValue && series == null)
            {
                ConsoleOutput.WriteError("--sequence requires --series.");
                Environment.Exit(1);
                return;
            }
            foreach (var file in files)
            {
                if (!File.Exists(file))
                {
                    ConsoleOutput.WriteError($"File not found: {file}");
                    Environment.Exit(1);
                    return;
                }
            }
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new UploadService(client);
            var folderId = folder ?? await service.ResolveFolderIdAsync(libraryId);
            await service.UploadAsync(libraryId, folderId, title, author, series, sequence, files);
            if (wait)
            {
                var searchTitle = sequence.HasValue ? $"{sequence.Value}. - {title}" : title;
                var item = await service.WaitForItemAsync(libraryId, searchTitle);
                if (item == null)
                {
                    ConsoleOutput.WriteError("Timed out waiting for item to appear in library.");
                    Environment.Exit(1);
                    return;
                }
                ConsoleOutput.WriteJson(item, AppJsonContext.Default.LibraryItemMinified);
            }
        }, libraryOption, folderOption, titleOption, authorOption,
            seriesOption, sequenceOption, waitOption, filesOption);
        return command;
    }
}
