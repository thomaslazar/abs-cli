using System.Text.Json;
using System.Web;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class UploadService
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
    private readonly AbsApiClient _client;

    public UploadService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<UploadReceipt> UploadAsync(string libraryId, string folderId, string title,
        string? author, string? series, string? sequence,
        IReadOnlyList<(string LocalPath, string UploadName)> files)
    {
        var uploadTitle = sequence != null ? $"{sequence}. - {title}" : title;
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
            RelPath = FilenameSanitizer.PredictRelPath(author, series, uploadTitle),
            Files = files.Select(f => f.UploadName).ToList(),
        };
    }

    public async Task<string> ResolveFolderIdAsync(string libraryId)
    {
        var library = await _client.GetAsync(ApiEndpoints.Library(libraryId),
            AppJsonContext.Default.Library);
        if (library.Folders.Count == 0)
        {
            _logger.Error("Library has no folders configured.");
            Environment.Exit(1);
        }
        if (library.Folders.Count > 1)
        {
            _logger.Error(
                $"Library has {library.Folders.Count} folders. Specify --folder <id>.\n" +
                "Use 'abs-cli libraries get --id <id>' to see folder IDs.");
            Environment.Exit(1);
        }
        return library.Folders[0].Id;
    }

    /// <summary>
    /// Poll items list (sorted by addedAt desc) and return the just-uploaded
    /// item, matched against <paramref name="expectedRelPath"/> with tolerant
    /// per-segment prefix matching (see <see cref="RelPathMatcher"/>) because
    /// the CLI's predicted path and ABS's stored path diverge in the truncated
    /// tail of long segments. Returns null if nothing matches within the window
    /// OR if more than one recent item matches — an unresolvable collision the
    /// caller must not guess at.
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
            var candidates = new List<(string RelPath, JsonElement Element)>();
            foreach (var itemElement in result.Results)
            {
                if (!itemElement.TryGetProperty("relPath", out var relPathProp)) continue;
                var relPath = relPathProp.GetString();
                if (relPath == null) continue;
                // ABS stores relPath with a leading slash; strip it for comparison.
                candidates.Add((relPath.TrimStart('/'), itemElement));
            }
            var matches = RelPathMatcher.Matches(expectedRelPath, candidates.Select(c => c.RelPath));
            if (matches.Count == 1)
            {
                var element = candidates.First(c => c.RelPath == matches[0]).Element;
                return JsonSerializer.Deserialize(element.GetRawText(),
                    AppJsonContext.Default.LibraryItemMinified);
            }
            // matches.Count > 1 ⇒ ambiguous collision; polling won't resolve it.
            if (matches.Count > 1) return null;
            await Task.Delay(pollIntervalMs);
        }
        return null;
    }
}
