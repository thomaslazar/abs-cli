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

    public async Task<Collection> CreateAsync(string libraryId, string name, string? description, List<string> books)
    {
        var body = new CollectionCreateRequest
        {
            LibraryId = libraryId,
            Name = name,
            Description = description,
            Books = books
        };
        var json = JsonSerializer.Serialize(body, AppJsonContext.Default.CollectionCreateRequest);
        return await _client.PostAsync(
            ApiEndpoints.Collections,
            json,
            AppJsonContext.Default.Collection,
            "'update' permission");
    }

    /// <summary>
    /// PATCH the collection. <paramref name="body"/> values are
    /// null-significant: pass <c>null</c> to clear that field server-side,
    /// or omit a key to leave it unchanged. Same tri-state pattern as
    /// <c>AuthorsService.UpdateAsync</c>.
    /// </summary>
    public async Task<Collection> UpdateAsync(string id, Dictionary<string, string> body)
    {
        var json = JsonSerializer.Serialize(body, AppJsonContext.Default.DictionaryStringString);
        return await _client.PatchAsync(
            ApiEndpoints.Collection(id),
            json,
            AppJsonContext.Default.Collection,
            "'update' permission");
    }

    /// <summary>
    /// PATCH the collection with a books array to reshuffle order. The
    /// raw JSON body is forwarded verbatim — caller supplies
    /// <c>{"books":[...]}</c>. ABS reorders existing membership only;
    /// see spec "Sharp edges" for the partial-list trap.
    /// </summary>
    public async Task<Collection> ReorderAsync(string id, string booksJson)
    {
        return await _client.PatchAsync(
            ApiEndpoints.Collection(id),
            booksJson,
            AppJsonContext.Default.Collection,
            "'update' permission");
    }

    public async Task DeleteAsync(string id)
    {
        await _client.DeleteAsync(ApiEndpoints.Collection(id), "'delete' permission");
    }

    public Task<Collection> AddBookAsync(string id, string libraryItemId)
        => throw new NotImplementedException();

    public Task<Collection> RemoveBookAsync(string id, string libraryItemId)
        => throw new NotImplementedException();

    public Task<Collection> BatchAddAsync(string id, string booksJson)
        => throw new NotImplementedException();

    public Task<Collection> BatchRemoveAsync(string id, string booksJson)
        => throw new NotImplementedException();
}
