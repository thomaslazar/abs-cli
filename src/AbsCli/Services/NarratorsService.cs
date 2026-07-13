using System.Text.Json;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class NarratorsService
{
    private readonly AbsApiClient _client;

    public NarratorsService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<NarratorListResponse> ListAsync(string libraryId)
    {
        return await _client.GetAsync(ApiEndpoints.LibraryNarrators(libraryId),
            AppJsonContext.Default.NarratorListResponse);
    }

    public async Task<NarratorUpdateResponse> RenameAsync(string libraryId, string oldName, string newName)
    {
        var json = JsonSerializer.Serialize(
            new NarratorRenameRequest { Name = newName },
            AppJsonContext.Default.NarratorRenameRequest);
        return await _client.PatchAsync(ApiEndpoints.LibraryNarratorByName(libraryId, oldName), json,
            AppJsonContext.Default.NarratorUpdateResponse, "'update' permission");
    }

    public async Task<NarratorUpdateResponse> DeleteAsync(string libraryId, string name)
    {
        return await _client.DeleteAsync(ApiEndpoints.LibraryNarratorByName(libraryId, name),
            AppJsonContext.Default.NarratorUpdateResponse, "'update' permission");
    }
}
