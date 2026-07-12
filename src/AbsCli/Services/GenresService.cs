using System.Text.Json;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class GenresService
{
    private readonly AbsApiClient _client;

    public GenresService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<GenreListResponse> ListAsync()
    {
        return await _client.GetAsync(ApiEndpoints.Genres,
            AppJsonContext.Default.GenreListResponse, "admin permission");
    }

    public async Task<GenreRenameResponse> RenameAsync(string genre, string newGenre)
    {
        var json = JsonSerializer.Serialize(
            new GenreRenameRequest { Genre = genre, NewGenre = newGenre },
            AppJsonContext.Default.GenreRenameRequest);
        return await _client.PostAsync(ApiEndpoints.GenreRename, json,
            AppJsonContext.Default.GenreRenameResponse, "admin permission");
    }

    public async Task<GenreDeleteResponse> DeleteAsync(string genre)
    {
        return await _client.DeleteAsync(ApiEndpoints.GenreByName(genre),
            AppJsonContext.Default.GenreDeleteResponse, "admin permission");
    }
}
