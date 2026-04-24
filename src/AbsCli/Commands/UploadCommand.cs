using System.CommandLine;
using System.Text.Json;
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
        var sequenceOption = new Option<string?>("--sequence", "Series sequence (requires --series). Any non-empty string — integer (\"1\"), decimal (\"1.5\"), or free-form (\"0\", \"II\"). Becomes the \"N. -\" prefix on the item folder.");
        var waitOption = new Option<bool>("--wait",
            "After upload, poll until the item appears in the library (up to ~2min) and return its full LibraryItemMinified. Without --wait, a terse UploadReceipt is returned immediately.");
        var filesOption = new Option<string[]>("--files", "File paths to upload (mutually exclusive with --files-manifest)") { AllowMultipleArgumentsPerToken = true };
        var prefixSourceDirOption = new Option<bool>("--prefix-source-dir",
            "Prefix each upload filename with its parent directory name (avoids collisions when files from multiple source dirs share basenames)");
        var manifestOption = new Option<string?>("--files-manifest",
            "Path to JSON manifest mapping {src, as} pairs (or '-' for stdin). Mutually exclusive with --files and --prefix-source-dir.");
        var command = new Command("upload", "Upload audiobook files to a library")
        {
            libraryOption, folderOption, titleOption, authorOption,
            seriesOption, sequenceOption, waitOption, filesOption,
            prefixSourceDirOption, manifestOption
        };
        command.AddHelpSection("Folder ID",
            "If the library has a single folder, it is auto-resolved.",
            "Otherwise pass --folder <id>.",
            "Run 'abs-cli libraries get --id <id>' to see folder IDs.");
        command.AddHelpSection("Folder structure created",
            "Title                              — when no --author is given",
            "Author/Title                       — when --author is given",
            "Author/Series/Title                — when --author and --series are given",
            "Author/Series/{N}. - {Title}       — when --sequence N is also given (requires --series)");
        command.AddHelpSection("Filename collisions",
            "ABS silently overwrites files with the same name in one upload — the CLI",
            "refuses to do this. By default duplicate basenames in --files cause an error.",
            "",
            "--prefix-source-dir   Prepend each file's parent directory name to the",
            "                      uploaded filename. Good for multi-disc / multi-part",
            "                      audiobooks where parent dir names sort correctly",
            "                      (e.g. \"Part 1-2\" before \"Part 3\").",
            "",
            "--files-manifest <p>  JSON file (or '-' for stdin) mapping each source path",
            "                      to the name ABS should save it as. Use when you need",
            "                      explicit per-file naming. Schema: [{\"src\":\"path\",\"as\":\"name\"}, ...]");
        command.AddExamples(
            "abs-cli upload --title \"The Hobbit\" --author \"J.R.R. Tolkien\" --files hobbit.m4b",
            "abs-cli upload --title \"The Final Empire\" --author \"Brandon Sanderson\" --series \"Mistborn\" --sequence 1 --files part1.mp3 part2.mp3",
            "abs-cli upload --title \"Morning Star\" --author \"Pierce Brown\" --series \"Red Rising\" --sequence 3 --prefix-source-dir --files \"Part 1-2\"/*.mp3 \"Part 3\"/*.mp3",
            "abs-cli upload --title \"My Audiobook\" --files-manifest manifest.json",
            "cat manifest.json | abs-cli upload --title \"My Audiobook\" --files-manifest -");
        command.AddHelpSection("Output",
            "Without --wait: returns a receipt ({uploaded, title, author, series,",
            "                libraryId, folderId, relPath, files}) once the HTTP",
            "                upload completes. ABS writes the files synchronously",
            "                but creates the library item on its next scan tick, so",
            "                the receipt confirms files landed, not that the item",
            "                exists yet. Use receipt.relPath to locate the resulting",
            "                library item later, e.g.:",
            "                  abs-cli items list --sort addedAt --desc \\",
            "                    | jq '.results[] | select(.relPath == \"<relPath>\")'",
            "With --wait:    polls items list for an item whose relPath matches the",
            "                receipt.relPath above, returns it as a LibraryItemMinified.",
            "                If the item doesn't appear within ~2 minutes the receipt",
            "                is emitted on stdout and the command exits 1 — ABS may",
            "                still be scanning; re-check with 'items list --sort addedAt --desc'.");
        command.AddResponseExample<UploadReceipt>();
        command.AddHelpSection("Response shape (with --wait, on success)",
            "LibraryItemMinified — same shape as 'abs-cli items get --help'.");
        command.SetHandler(async (context) =>
        {
            var library = context.ParseResult.GetValueForOption(libraryOption);
            var folder = context.ParseResult.GetValueForOption(folderOption);
            var title = context.ParseResult.GetValueForOption(titleOption)!;
            var author = context.ParseResult.GetValueForOption(authorOption);
            var series = context.ParseResult.GetValueForOption(seriesOption);
            var sequence = context.ParseResult.GetValueForOption(sequenceOption);
            var wait = context.ParseResult.GetValueForOption(waitOption);
            var files = context.ParseResult.GetValueForOption(filesOption) ?? Array.Empty<string>();
            var prefixSourceDir = context.ParseResult.GetValueForOption(prefixSourceDirOption);
            var manifestPath = context.ParseResult.GetValueForOption(manifestOption);
            if (sequence != null && series == null)
            {
                ConsoleOutput.WriteError("--sequence requires --series.");
                Environment.Exit(1);
                return;
            }
            if (sequence != null && string.IsNullOrWhiteSpace(sequence))
            {
                ConsoleOutput.WriteError("--sequence must be a non-empty string.");
                Environment.Exit(1);
                return;
            }
            if (manifestPath != null && files.Length > 0)
            {
                ConsoleOutput.WriteError("--files and --files-manifest are mutually exclusive.");
                Environment.Exit(1);
                return;
            }
            if (manifestPath != null && prefixSourceDir)
            {
                ConsoleOutput.WriteError("--prefix-source-dir and --files-manifest are mutually exclusive.");
                Environment.Exit(1);
                return;
            }
            if (manifestPath == null && files.Length == 0)
            {
                ConsoleOutput.WriteError("Pass --files <path>... or --files-manifest <path|->.");
                Environment.Exit(1);
                return;
            }
            var uploadList = manifestPath != null
                ? await BuildFromManifestAsync(manifestPath)
                : BuildFromFiles(files, prefixSourceDir);
            foreach (var entry in uploadList)
            {
                if (!File.Exists(entry.LocalPath))
                {
                    ConsoleOutput.WriteError($"File not found: {entry.LocalPath}");
                    Environment.Exit(1);
                    return;
                }
            }
            CheckForDuplicates(uploadList);
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new UploadService(client);
            var folderId = folder ?? await service.ResolveFolderIdAsync(libraryId);
            var receipt = await service.UploadAsync(libraryId, folderId, title, author, series, sequence, uploadList);
            if (wait)
            {
                // Match by relPath — deterministic given that we compute the
                // exact path ABS wrote to using the same sanitisation. The
                // old title-substring search failed with --sequence because
                // ABS strips the "N. -" prefix from media.metadata.title when
                // scanning, so the CLI's search query (which included the
                // prefix) no longer matched what ABS had indexed.
                var item = await service.WaitForItemByPathAsync(libraryId, receipt.RelPath);
                if (item == null)
                {
                    ConsoleOutput.WriteError(
                        $"Upload completed but the library item did not appear within the wait window. " +
                        $"Expected relPath: '{receipt.RelPath}'. " +
                        $"ABS may still be scanning — re-run: abs-cli items list --sort addedAt --desc --limit 5");
                    ConsoleOutput.WriteJson(receipt, AppJsonContext.Default.UploadReceipt);
                    Environment.Exit(1);
                    return;
                }
                ConsoleOutput.WriteJson(item, AppJsonContext.Default.LibraryItemMinified);
            }
            else
            {
                ConsoleOutput.WriteJson(receipt, AppJsonContext.Default.UploadReceipt);
            }
        });
        return command;
    }

    private static List<(string LocalPath, string UploadName)> BuildFromFiles(string[] files, bool prefixSourceDir)
    {
        var result = new List<(string LocalPath, string UploadName)>(files.Length);
        foreach (var file in files)
        {
            var basename = Path.GetFileName(file);
            string uploadName;
            if (prefixSourceDir)
            {
                var parentDir = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(file)) ?? "");
                uploadName = string.IsNullOrEmpty(parentDir) ? basename : $"{parentDir} - {basename}";
            }
            else
            {
                uploadName = basename;
            }
            result.Add((file, uploadName));
        }
        return result;
    }

    private static async Task<List<(string LocalPath, string UploadName)>> BuildFromManifestAsync(string manifestPath)
    {
        string json;
        if (manifestPath == "-")
        {
            json = await Console.In.ReadToEndAsync();
        }
        else
        {
            if (!File.Exists(manifestPath))
            {
                ConsoleOutput.WriteError($"Manifest file not found: {manifestPath}");
                Environment.Exit(1);
            }
            json = await File.ReadAllTextAsync(manifestPath);
        }
        List<UploadManifestEntry>? entries = null;
        try
        {
            entries = JsonSerializer.Deserialize(json, AppJsonContext.Default.ListUploadManifestEntry);
        }
        catch (JsonException ex)
        {
            ConsoleOutput.WriteError($"Manifest is not valid JSON: {ex.Message}");
            Environment.Exit(1);
        }
        if (entries == null || entries.Count == 0)
        {
            ConsoleOutput.WriteError("Manifest is empty or null. Provide a non-empty array of {src, as} entries.");
            Environment.Exit(1);
        }
        var result = new List<(string LocalPath, string UploadName)>(entries!.Count);
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Src) || string.IsNullOrWhiteSpace(entry.TargetName))
            {
                ConsoleOutput.WriteError("Manifest entry missing 'src' or 'as'. Each entry must have both.");
                Environment.Exit(1);
            }
            result.Add((entry.Src, entry.TargetName));
        }
        return result;
    }

    private static void CheckForDuplicates(IReadOnlyList<(string LocalPath, string UploadName)> uploadList)
    {
        var groups = uploadList
            .GroupBy(e => e.UploadName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();
        if (groups.Count == 0) return;
        var lines = new List<string> { "Duplicate filenames in upload — ABS would silently overwrite:" };
        foreach (var group in groups)
        {
            lines.Add($"  \"{group.Key}\" maps to {group.Count()} source files:");
            foreach (var entry in group)
            {
                lines.Add($"    {entry.LocalPath}");
            }
        }
        lines.Add("");
        lines.Add("Pass --prefix-source-dir to prefix each upload filename with its parent");
        lines.Add("directory name, or --files-manifest <path> for explicit per-file naming.");
        ConsoleOutput.WriteError(string.Join("\n", lines));
        Environment.Exit(1);
    }
}
