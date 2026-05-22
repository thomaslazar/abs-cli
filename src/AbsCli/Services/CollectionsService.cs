using System.Text.Json;
using System.Web;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class CollectionsService
{
    private readonly AbsApiClient _client;

    public CollectionsService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<PaginatedResponse> ListAsync(string libraryId, int limit, int? page, string? include)
    {
        var query = HttpUtility.ParseQueryString("");
        // Always send numeric limit and page so ABS emits the paginated
        // shape unconditionally — same pattern as AuthorsService.ListAsync.
        query["limit"] = limit.ToString();
        query["page"] = (page ?? 0).ToString();
        if (!string.IsNullOrEmpty(include)) query["include"] = include;

        var url = ApiEndpoints.LibraryCollections(libraryId) + "?" + query;
        return await _client.GetAsync(url, AppJsonContext.Default.PaginatedResponse);
    }

    public async Task<Collection> GetAsync(string id, string? include)
    {
        var url = ApiEndpoints.Collection(id);
        if (!string.IsNullOrEmpty(include)) url += "?include=" + Uri.EscapeDataString(include);
        return await _client.GetAsync(url, AppJsonContext.Default.Collection);
    }

    public Task<Collection> CreateAsync(string libraryId, string name, string? description, List<string> books)
        => throw new NotImplementedException();

    public Task<Collection> UpdateAsync(string id, Dictionary<string, string> body)
        => throw new NotImplementedException();

    public Task<Collection> ReorderAsync(string id, string booksJson)
        => throw new NotImplementedException();

    public Task DeleteAsync(string id)
        => throw new NotImplementedException();

    public Task<Collection> AddBookAsync(string id, string libraryItemId)
        => throw new NotImplementedException();

    public Task<Collection> RemoveBookAsync(string id, string libraryItemId)
        => throw new NotImplementedException();

    public Task<Collection> BatchAddAsync(string id, string booksJson)
        => throw new NotImplementedException();

    public Task<Collection> BatchRemoveAsync(string id, string booksJson)
        => throw new NotImplementedException();
}
