using AbsCli.Api;

namespace AbsCli.Services;

public class LibrariesService
{
    private readonly AbsApiClient _client;

    public LibrariesService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<string> ListAsync()
    {
        return await _client.GetAsync(ApiEndpoints.Libraries);
    }

    public async Task<string> GetAsync(string id)
    {
        return await _client.GetAsync(ApiEndpoints.Library(id));
    }
}
