using System.Web;
using AbsCli.Api;

namespace AbsCli.Services;

public class SeriesService
{
    private readonly AbsApiClient _client;

    public SeriesService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<string> ListAsync(string libraryId, int? limit, int? page)
    {
        var query = HttpUtility.ParseQueryString("");
        if (limit.HasValue) query["limit"] = limit.Value.ToString();
        if (page.HasValue) query["page"] = page.Value.ToString();

        var url = ApiEndpoints.LibrarySeries(libraryId);
        if (query.Count > 0) url += "?" + query;

        return await _client.GetAsync(url);
    }

    public async Task<string> GetAsync(string id)
    {
        return await _client.GetAsync(ApiEndpoints.SeriesById(id));
    }
}
