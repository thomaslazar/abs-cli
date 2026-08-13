using System.Text.Json;
using System.Web;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

/// <summary>
/// Calls the ABS playlist endpoints. Playlists are user-owned; none of these
/// operations require a <c>user.permissions</c> flag, so no permissionHint is
/// passed. The CLI accepts a <c>{"books":[...]}</c> id list (same as
/// collections) and this service maps it to ABS's <c>items:[{libraryItemId}]</c>
/// body shape.
/// </summary>
public class PlaylistsService
{
    private readonly AbsApiClient _client;

    public PlaylistsService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<PaginatedResponse> ListAsync(string libraryId, int limit, int? page)
    {
        var query = HttpUtility.ParseQueryString("");
        query["limit"] = limit.ToString();
        query["page"] = (page ?? 0).ToString();
        var url = ApiEndpoints.LibraryPlaylists(libraryId) + "?" + query;
        return await _client.GetAsync(url, AppJsonContext.Default.PaginatedResponse);
    }

    public async Task<Playlist> GetAsync(string id)
    {
        return await _client.GetAsync(ApiEndpoints.Playlist(id), AppJsonContext.Default.Playlist);
    }

    public async Task<Playlist> CreateAsync(string libraryId, string name, string? description, List<string> books)
    {
        var body = new PlaylistCreateRequest
        {
            LibraryId = libraryId,
            Name = name,
            Description = description,
            Items = books.Select(b => new PlaylistItemRef { LibraryItemId = b }).ToList()
        };
        var json = JsonSerializer.Serialize(body, AppJsonContext.Default.PlaylistCreateRequest);
        return await _client.PostAsync(ApiEndpoints.Playlists, json, AppJsonContext.Default.Playlist);
    }

    /// <summary>PATCH name/description. Empty values are ignored server-side.</summary>
    public async Task<Playlist> UpdateAsync(string id, Dictionary<string, string> body)
    {
        var json = JsonSerializer.Serialize(body, AppJsonContext.Default.DictionaryStringString);
        return await _client.PatchAsync(ApiEndpoints.Playlist(id), json, AppJsonContext.Default.Playlist);
    }

    /// <summary>
    /// PATCH the playlist with a full ordered item list to reshuffle order.
    /// ABS reorders existing membership only; the list length must equal the
    /// current item count.
    /// </summary>
    public async Task<Playlist> ReorderAsync(string id, List<string> books)
    {
        var json = SerializeItems(books);
        return await _client.PatchAsync(ApiEndpoints.Playlist(id), json, AppJsonContext.Default.Playlist);
    }

    public async Task DeleteAsync(string id)
    {
        await _client.DeleteAsync(ApiEndpoints.Playlist(id));
    }

    public async Task<Playlist> AddBookAsync(string id, string libraryItemId)
    {
        var body = new PlaylistItemRef { LibraryItemId = libraryItemId };
        var json = JsonSerializer.Serialize(body, AppJsonContext.Default.PlaylistItemRef);
        return await _client.PostAsync(ApiEndpoints.PlaylistItem(id), json, AppJsonContext.Default.Playlist);
    }

    public async Task<Playlist> RemoveBookAsync(string id, string libraryItemId)
    {
        return await _client.DeleteAsync(
            ApiEndpoints.PlaylistItemById(id, libraryItemId),
            AppJsonContext.Default.Playlist);
    }

    public async Task<Playlist> BatchAddAsync(string id, List<string> books)
    {
        var json = SerializeItems(books);
        return await _client.PostAsync(ApiEndpoints.PlaylistBatchAdd(id), json, AppJsonContext.Default.Playlist);
    }

    public async Task<Playlist> BatchRemoveAsync(string id, List<string> books)
    {
        var json = SerializeItems(books);
        return await _client.PostAsync(ApiEndpoints.PlaylistBatchRemove(id), json, AppJsonContext.Default.Playlist);
    }

    public async Task<Playlist> CreateFromCollectionAsync(string collectionId)
    {
        return await _client.PostEmptyAsync(
            ApiEndpoints.PlaylistFromCollection(collectionId),
            AppJsonContext.Default.Playlist);
    }

    /// <summary>
    /// Maps the CLI's book-id list to ABS's items:[{libraryItemId}] wire
    /// body. Internal (not private) so PlaylistsService_BooksContract tests
    /// can pin the CLI input contract (books) to the actual wire shape
    /// (items) and catch drift between them.
    /// </summary>
    internal static string SerializeItems(List<string> books)
    {
        var body = new PlaylistItemsRequest
        {
            Items = books.Select(b => new PlaylistItemRef { LibraryItemId = b }).ToList()
        };
        return JsonSerializer.Serialize(body, AppJsonContext.Default.PlaylistItemsRequest);
    }
}
