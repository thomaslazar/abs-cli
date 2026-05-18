using System.Web;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class ItemsService
{
    private readonly AbsApiClient _client;

    public ItemsService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<PaginatedResponse> ListAsync(string libraryId, string? filter, string? sort,
        bool desc, int? limit, int? page)
    {
        var query = HttpUtility.ParseQueryString("");
        if (filter != null) query["filter"] = FilterEncoder.Encode(filter);
        if (sort != null) query["sort"] = sort;
        if (desc) query["desc"] = "1";
        if (limit.HasValue) query["limit"] = limit.Value.ToString();
        if (page.HasValue) query["page"] = page.Value.ToString();

        var url = ApiEndpoints.LibraryItems(libraryId);
        if (query.Count > 0) url += "?" + query;

        return await _client.GetAsync(url, AppJsonContext.Default.PaginatedResponse);
    }

    public async Task<LibraryItemMinified> GetAsync(string id)
    {
        return await _client.GetAsync(ApiEndpoints.Item(id), AppJsonContext.Default.LibraryItemMinified);
    }

    public async Task<UpdateMediaResponse> UpdateMediaAsync(string id, string jsonBody)
    {
        return await _client.PatchAsync(
            ApiEndpoints.ItemMedia(id),
            jsonBody,
            AppJsonContext.Default.UpdateMediaResponse,
            permissionHint: "'update' permission");
    }

    public async Task<BatchUpdateResponse> BatchUpdateAsync(string jsonBody)
    {
        // ABS route is POST /items/batch/update (not PATCH — the single-item
        // /items/:id/media endpoint is PATCH but the batch variant is POST).
        // See temp/audiobookshelf/server/routers/ApiRouter.js.
        return await _client.PostAsync(
            ApiEndpoints.ItemsBatchUpdate,
            jsonBody,
            AppJsonContext.Default.BatchUpdateResponse,
            permissionHint: "'update' permission");
    }

    public async Task<BatchGetResponse> BatchGetAsync(string jsonBody)
    {
        return await _client.PostAsync(ApiEndpoints.ItemsBatchGet, jsonBody, AppJsonContext.Default.BatchGetResponse);
    }

    public async Task<ScanResult> ScanAsync(string id)
    {
        return await _client.PostEmptyAsync(ApiEndpoints.ItemScan(id),
            AppJsonContext.Default.ScanResult, "'admin' access");
    }

    /// <summary>
    /// Toggle the ebook-primary status of a single file on an item.
    /// PATCH /api/items/:id/ebook/:fileid/status — body is empty; the
    /// targeted file is the entire payload. ABS flips the file's
    /// isSupplementary value (supplementary → primary auto-demotes
    /// previous primary; primary → unset, no auto-promotion).
    /// Returns empty 200 on success.
    /// </summary>
    public async Task ToggleEbookFileStatusAsync(string itemId, string fileIno)
    {
        await _client.PatchAsync(
            ApiEndpoints.ItemEbookFileStatus(itemId, fileIno),
            jsonBody: "",
            permissionHint: "'update' permission");
    }
}
