using AbsCli.Api;

namespace AbsCli.Services;

public class CacheService
{
    private readonly AbsApiClient _client;

    public CacheService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task PurgeItemsAsync()
    {
        await _client.PostEmptyAsync(ApiEndpoints.CachePurgeItems, permissionHint: "admin permission");
    }

    public async Task PurgeAsync()
    {
        await _client.PostEmptyAsync(ApiEndpoints.CachePurge, permissionHint: "admin permission");
    }
}
