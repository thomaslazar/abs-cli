using System.Web;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class SearchService
{
    private readonly AbsApiClient _client;

    public SearchService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<SearchResult> SearchAsync(string libraryId, string query, int? limit)
    {
        var qs = HttpUtility.ParseQueryString("");
        qs["q"] = query;
        if (limit.HasValue) qs["limit"] = limit.Value.ToString();
        return await _client.GetAsync(ApiEndpoints.LibrarySearch(libraryId) + "?" + qs, AppJsonContext.Default.SearchResult);
    }
}
