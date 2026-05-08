using System.Text.Json;
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

    public async Task<AuthorMatchResponse> MatchAsync(string id, AuthorMatchRequest body)
    {
        var json = JsonSerializer.Serialize(body, AppJsonContext.Default.AuthorMatchRequest);
        return await _client.PostAsync(
            ApiEndpoints.AuthorMatch(id),
            json,
            AppJsonContext.Default.AuthorMatchResponse,
            "'update' permission");
    }

    public async Task<string> LookupAsync(string name)
    {
        var endpoint = ApiEndpoints.SearchAuthors + "?q=" + Uri.EscapeDataString(name);
        return await _client.GetAsync(endpoint);
    }

    /// <summary>
    /// PATCH the author. <paramref name="body"/> values are null-significant:
    /// pass a <c>null</c> value to clear that field server-side, or omit a key
    /// entirely to leave it unchanged. The dictionary's nullable annotation is
    /// erased at runtime so STJ emits JSON <c>null</c> for the cleared keys.
    /// </summary>
    public async Task<AuthorUpdateResponse> UpdateAsync(string id, Dictionary<string, string> body)
    {
        var json = JsonSerializer.Serialize(body, AppJsonContext.Default.DictionaryStringString);
        return await _client.PatchAsync(
            ApiEndpoints.AuthorById(id),
            json,
            AppJsonContext.Default.AuthorUpdateResponse,
            "'update' permission");
    }

    public async Task DeleteAsync(string id)
    {
        await _client.DeleteAsync(ApiEndpoints.AuthorById(id), "'delete' permission");
    }
}
