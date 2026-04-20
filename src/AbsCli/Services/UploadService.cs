using System.Web;
using AbsCli.Api;
using AbsCli.Models;
using AbsCli.Output;

namespace AbsCli.Services;

public class UploadService
{
    private readonly AbsApiClient _client;

    public UploadService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<UploadReceipt> UploadAsync(string libraryId, string folderId, string title,
        string? author, string? series, int? sequence,
        IReadOnlyList<(string LocalPath, string UploadName)> files)
    {
        var uploadTitle = sequence.HasValue ? $"{sequence.Value}. - {title}" : title;
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(libraryId), "library");
        content.Add(new StringContent(folderId), "folder");
        content.Add(new StringContent(uploadTitle), "title");
        if (author != null)
            content.Add(new StringContent(author), "author");
        if (series != null)
            content.Add(new StringContent(series), "series");
        for (int i = 0; i < files.Count; i++)
        {
            var fileBytes = await File.ReadAllBytesAsync(files[i].LocalPath);
            var fileContent = new ByteArrayContent(fileBytes);
            content.Add(fileContent, i.ToString(), files[i].UploadName);
        }
        await _client.PostMultipartAsync(ApiEndpoints.Upload, content, "'upload' permission",
            timeout: Timeout.InfiniteTimeSpan);

        // ABS's upload endpoint returns HTTP 200 with an empty body (see
        // server/controllers/MiscController.js handleUpload → res.sendStatus(200)).
        // Synthesise a receipt from the request so callers have a concrete
        // success signal instead of empty stdout.
        return new UploadReceipt
        {
            Uploaded = true,
            Title = uploadTitle,
            Author = author,
            Series = series,
            LibraryId = libraryId,
            FolderId = folderId,
            Files = files.Select(f => f.UploadName).ToList(),
        };
    }

    public async Task<string> ResolveFolderIdAsync(string libraryId)
    {
        var library = await _client.GetAsync(ApiEndpoints.Library(libraryId),
            AppJsonContext.Default.Library);
        if (library.Folders.Count == 0)
        {
            ConsoleOutput.WriteError("Library has no folders configured.");
            Environment.Exit(1);
        }
        if (library.Folders.Count > 1)
        {
            ConsoleOutput.WriteError(
                $"Library has {library.Folders.Count} folders. Specify --folder <id>.\n" +
                "Use 'abs-cli libraries get --id <id>' to see folder IDs.");
            Environment.Exit(1);
        }
        return library.Folders[0].Id;
    }

    /// <summary>
    /// Poll items list (sorted by addedAt desc) and return the first item whose
    /// relPath matches <paramref name="expectedRelPath"/>. Path-based match is
    /// deterministic: we computed the exact folder ABS wrote the files into
    /// using the same sanitisation ABS applies, so there is no false positive
    /// and no false negative from title substring matching (which broke when
    /// --sequence prefixed the uploaded title).
    /// </summary>
    public async Task<LibraryItemMinified?> WaitForItemByPathAsync(
        string libraryId, string expectedRelPath,
        int timeoutSeconds = 120, int pollIntervalMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var query = HttpUtility.ParseQueryString("");
            query["sort"] = "addedAt";
            query["desc"] = "1";
            query["limit"] = "20";
            var url = ApiEndpoints.LibraryItems(libraryId) + "?" + query;
            var result = await _client.GetAsync(url, AppJsonContext.Default.PaginatedResponse);
            foreach (var itemElement in result.Results)
            {
                if (!itemElement.TryGetProperty("relPath", out var relPathProp)) continue;
                var relPath = relPathProp.GetString();
                if (relPath == null) continue;
                // ABS stores relPath with a leading slash; strip it for comparison.
                var normalised = relPath.TrimStart('/');
                if (string.Equals(normalised, expectedRelPath, StringComparison.Ordinal))
                {
                    return System.Text.Json.JsonSerializer.Deserialize(itemElement.GetRawText(),
                        AppJsonContext.Default.LibraryItemMinified);
                }
            }
            await Task.Delay(pollIntervalMs);
        }
        return null;
    }
}
