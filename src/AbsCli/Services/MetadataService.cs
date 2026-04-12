using System.Web;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class MetadataService
{
    private readonly AbsApiClient _client;

    public MetadataService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<string> SearchAsync(string provider, string title, string? author)
    {
        var query = HttpUtility.ParseQueryString("");
        query["provider"] = provider;
        query["title"] = title;
        if (author != null) query["author"] = author;
        return await _client.GetAsync(ApiEndpoints.SearchBooks + "?" + query);
    }

    public async Task<MetadataProvidersResponse> ListProvidersAsync()
    {
        return await _client.GetAsync(ApiEndpoints.SearchProviders,
            AppJsonContext.Default.MetadataProvidersResponse);
    }

    public async Task<CoverSearchResponse> SearchCoversAsync(string provider, string title, string? author)
    {
        var query = HttpUtility.ParseQueryString("");
        query["provider"] = provider;
        query["title"] = title;
        if (author != null) query["author"] = author;
        return await _client.GetAsync(ApiEndpoints.SearchCovers + "?" + query,
            AppJsonContext.Default.CoverSearchResponse);
    }
}
