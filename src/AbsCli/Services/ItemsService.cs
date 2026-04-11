using System.Web;
using AbsCli.Api;

namespace AbsCli.Services;

public class ItemsService
{
    private readonly AbsApiClient _client;

    public ItemsService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<string> ListAsync(string libraryId, string? filter, string? sort,
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

        return await _client.GetAsync(url);
    }

    public async Task<string> GetAsync(string id)
    {
        return await _client.GetAsync(ApiEndpoints.Item(id));
    }

    public async Task<string> SearchAsync(string libraryId, string query, int? limit)
    {
        var qs = HttpUtility.ParseQueryString("");
        qs["q"] = query;
        if (limit.HasValue) qs["limit"] = limit.Value.ToString();
        return await _client.GetAsync(ApiEndpoints.LibrarySearch(libraryId) + "?" + qs);
    }

    public async Task<string> UpdateMediaAsync(string id, string jsonBody)
    {
        return await _client.PatchAsync(ApiEndpoints.ItemMedia(id), jsonBody);
    }

    public async Task<string> BatchUpdateAsync(string jsonBody)
    {
        return await _client.PatchAsync(ApiEndpoints.ItemsBatchUpdate, jsonBody);
    }

    public async Task<string> BatchGetAsync(string jsonBody)
    {
        return await _client.PostAsync(ApiEndpoints.ItemsBatchGet, jsonBody);
    }
}
