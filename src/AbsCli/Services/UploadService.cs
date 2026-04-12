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

    public async Task UploadAsync(string libraryId, string folderId, string title,
        string? author, string? series, int? sequence, string[] filePaths)
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
        for (int i = 0; i < filePaths.Length; i++)
        {
            var fileBytes = await File.ReadAllBytesAsync(filePaths[i]);
            var fileContent = new ByteArrayContent(fileBytes);
            content.Add(fileContent, i.ToString(), Path.GetFileName(filePaths[i]));
        }
        await _client.PostMultipartAsync(ApiEndpoints.Upload, content, "'upload' permission");
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

    public async Task<LibraryItemMinified?> WaitForItemAsync(string libraryId, string title,
        int timeoutSeconds = 60, int pollIntervalMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var query = HttpUtility.ParseQueryString("");
            query["q"] = title;
            query["limit"] = "5";
            var url = ApiEndpoints.LibrarySearch(libraryId) + "?" + query;
            var result = await _client.GetAsync(url, AppJsonContext.Default.SearchResult);
            if (result.Book != null && result.Book.Count > 0)
            {
                var itemJson = result.Book[0].GetProperty("libraryItem").GetRawText();
                return System.Text.Json.JsonSerializer.Deserialize(itemJson,
                    AppJsonContext.Default.LibraryItemMinified);
            }
            await Task.Delay(pollIntervalMs);
        }
        return null;
    }
}
