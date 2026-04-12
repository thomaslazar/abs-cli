using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class AuthorsService
{
    private readonly AbsApiClient _client;

    public AuthorsService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<AuthorListResponse> ListAsync(string libraryId)
    {
        return await _client.GetAsync(ApiEndpoints.LibraryAuthors(libraryId), AppJsonContext.Default.AuthorListResponse);
    }

    public async Task<AuthorItem> GetAsync(string id)
    {
        return await _client.GetAsync(ApiEndpoints.AuthorById(id) + "?include=items", AppJsonContext.Default.AuthorItem);
    }
}
