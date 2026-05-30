using System.Text.Json;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class ProgressService
{
    private readonly AbsApiClient _client;

    public ProgressService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<MediaProgress> GetAsync(string libraryItemId)
        => await _client.GetAsync(
            ApiEndpoints.MeProgress(libraryItemId),
            AppJsonContext.Default.MediaProgress);

    /// <summary>
    /// PATCH the progress record. Only non-null fields on
    /// <paramref name="body"/> are written; the server treats absent
    /// fields as "leave unchanged." Returns the post-update record
    /// via a follow-up GET (server's PATCH response is empty 200).
    /// </summary>
    public async Task<MediaProgress> SetAsync(string libraryItemId, ProgressUpdateRequest body)
    {
        var json = JsonSerializer.Serialize(body, AppJsonContext.Default.ProgressUpdateRequest);
        await _client.PatchAsync(ApiEndpoints.MeProgress(libraryItemId), json);
        return await GetAsync(libraryItemId);
    }

    /// <summary>
    /// Removes the progress record. Server's DELETE takes the
    /// MediaProgress row id (not the libraryItemId), so this method
    /// does an internal GET to discover the id and then deletes.
    /// </summary>
    public async Task RemoveAsync(string libraryItemId)
    {
        var progress = await GetAsync(libraryItemId);
        await _client.DeleteAsync(ApiEndpoints.MeProgressById(progress.Id));
    }

    /// <summary>
    /// PATCH the batch endpoint with a raw JSON array (caller-supplied).
    /// Server returns 200 even when individual entries fail; no
    /// per-entry feedback in the response.
    /// </summary>
    public async Task BatchUpdateAsync(string jsonBody)
    {
        await _client.PatchAsync(ApiEndpoints.MeProgressBatchUpdate, jsonBody);
    }
}
