using System.Text.Json;
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

    public async Task ScanAsync(string libraryId, bool force)
    {
        var url = ApiEndpoints.LibraryScan(libraryId);
        if (force) url += "?force=1";
        await _client.PostEmptyAsync(url, "'admin' access");
    }

    public async Task<Library> CreateAsync(LibraryCreateRequest body)
    {
        var json = JsonSerializer.Serialize(body, AppJsonContext.Default.LibraryCreateRequest);
        return await _client.PostAsync(ApiEndpoints.Libraries, json, AppJsonContext.Default.Library, "admin permission");
    }

    public async Task<Library> UpdateAsync(string id, LibraryUpdateRequest body)
    {
        var json = JsonSerializer.Serialize(body, AppJsonContext.Default.LibraryUpdateRequest);
        return await _client.PatchAsync(ApiEndpoints.Library(id), json, AppJsonContext.Default.Library, "admin permission");
    }

    public async Task<Library> DeleteAsync(string id)
    {
        return await _client.DeleteAsync(ApiEndpoints.Library(id), AppJsonContext.Default.Library, "admin permission");
    }

    public async Task<LibraryListResponse> ReorderAsync(string orderJson)
    {
        return await _client.PostAsync(ApiEndpoints.LibrariesOrder, orderJson, AppJsonContext.Default.LibraryListResponse, "admin permission");
    }
}
