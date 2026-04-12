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

    public async Task<SearchResult> SearchAsync(string libraryId, string query, int? limit)
    {
        var qs = HttpUtility.ParseQueryString("");
        qs["q"] = query;
        if (limit.HasValue) qs["limit"] = limit.Value.ToString();
        return await _client.GetAsync(ApiEndpoints.LibrarySearch(libraryId) + "?" + qs, AppJsonContext.Default.SearchResult);
    }

    public async Task<UpdateMediaResponse> UpdateMediaAsync(string id, string jsonBody)
    {
        return await _client.PatchAsync(ApiEndpoints.ItemMedia(id), jsonBody, AppJsonContext.Default.UpdateMediaResponse);
    }

    public async Task<BatchUpdateResponse> BatchUpdateAsync(string jsonBody)
    {
        return await _client.PatchAsync(ApiEndpoints.ItemsBatchUpdate, jsonBody, AppJsonContext.Default.BatchUpdateResponse);
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
}
