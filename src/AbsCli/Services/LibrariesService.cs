using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class LibrariesService
{
    private readonly AbsApiClient _client;

    public LibrariesService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<LibraryListResponse> ListAsync()
    {
        return await _client.GetAsync(ApiEndpoints.Libraries, AppJsonContext.Default.LibraryListResponse);
    }

    public async Task<Library> GetAsync(string id)
    {
        return await _client.GetAsync(ApiEndpoints.Library(id), AppJsonContext.Default.Library);
    }
}
