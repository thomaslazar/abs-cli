using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class MeService
{
    private readonly AbsApiClient _client;

    public MeService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<Me> GetAsync()
        => await _client.GetAsync(ApiEndpoints.Me, AppJsonContext.Default.Me);
}
