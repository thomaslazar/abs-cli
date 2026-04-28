using System.Text.Json;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class CoversService
{
    private readonly AbsApiClient _client;

    public CoversService(AbsApiClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Apply a cover by URL. ABS server downloads from the URL.
    /// </summary>
    public async Task<CoverApplyResponse> SetByUrlAsync(string itemId, string url)
    {
        var body = JsonSerializer.Serialize(
            new CoverApplyByUrlRequest { Url = url },
            AppJsonContext.Default.CoverApplyByUrlRequest);
        return await _client.PostAsync(ApiEndpoints.ItemCover(itemId), body,
            AppJsonContext.Default.CoverApplyResponse, "'upload' permission");
    }

    /// <summary>
    /// Apply a cover by uploading a local file (multipart).
    /// </summary>
    public async Task<CoverApplyResponse> UploadFromFileAsync(string itemId, string localFilePath)
    {
        var fileBytes = await File.ReadAllBytesAsync(localFilePath);
        var fileContent = new ByteArrayContent(fileBytes);
        var content = new MultipartFormDataContent();
        content.Add(fileContent, "cover", Path.GetFileName(localFilePath));
        return await _client.PostMultipartAsync(ApiEndpoints.ItemCover(itemId), content,
            AppJsonContext.Default.CoverApplyResponse, "'upload' permission");
    }

    /// <summary>
    /// Apply a cover by pointing to an existing file on the ABS server's
    /// filesystem. The server validates the path exists and is a real file.
    /// </summary>
    public async Task<CoverApplyResponse> LinkExistingAsync(string itemId, string serverPath)
    {
        var body = JsonSerializer.Serialize(
            new CoverLinkExistingRequest { Cover = serverPath },
            AppJsonContext.Default.CoverLinkExistingRequest);
        return await _client.PatchAsync(ApiEndpoints.ItemCover(itemId), body,
            AppJsonContext.Default.CoverApplyResponse);
    }

    /// <summary>
    /// Fetch the cover bytes. Caller must dispose the returned stream.
    /// </summary>
    public async Task<Stream> GetStreamAsync(string itemId, bool raw)
    {
        var endpoint = ApiEndpoints.ItemCover(itemId);
        if (raw) endpoint += "?raw=1";
        return await _client.GetStreamAsync(endpoint);
    }

    /// <summary>
    /// Remove the cover from an item. Server returns 200 with empty body.
    /// </summary>
    public async Task RemoveAsync(string itemId)
    {
        await _client.DeleteAsync(ApiEndpoints.ItemCover(itemId));
    }
}
