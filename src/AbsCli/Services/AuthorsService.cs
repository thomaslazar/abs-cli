using AbsCli.Api;

namespace AbsCli.Services;

public class AuthorsService
{
    private readonly AbsApiClient _client;

    public AuthorsService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<string> ListAsync(string libraryId)
    {
        return await _client.GetAsync(ApiEndpoints.LibraryAuthors(libraryId));
    }

    public async Task<string> GetAsync(string id)
    {
        return await _client.GetAsync(ApiEndpoints.AuthorById(id) + "?include=items");
    }
}
