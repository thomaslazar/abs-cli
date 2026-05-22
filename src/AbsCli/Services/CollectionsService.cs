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

    public Task<PaginatedResponse> ListAsync(string libraryId, int limit, int? page, string? include)
        => throw new NotImplementedException();

    public Task<Collection> GetAsync(string id, string? include)
        => throw new NotImplementedException();

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
