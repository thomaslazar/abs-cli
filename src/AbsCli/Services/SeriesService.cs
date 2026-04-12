using System.Web;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class SeriesService
{
    private readonly AbsApiClient _client;

    public SeriesService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<PaginatedResponse> ListAsync(string libraryId, int? limit, int? page)
    {
        var query = HttpUtility.ParseQueryString("");
        if (limit.HasValue) query["limit"] = limit.Value.ToString();
        if (page.HasValue) query["page"] = page.Value.ToString();

        var url = ApiEndpoints.LibrarySeries(libraryId);
        if (query.Count > 0) url += "?" + query;

        return await _client.GetAsync(url, AppJsonContext.Default.PaginatedResponse);
    }

    public async Task<SeriesItem> GetAsync(string id)
    {
        return await _client.GetAsync(ApiEndpoints.SeriesById(id), AppJsonContext.Default.SeriesItem);
    }
}
