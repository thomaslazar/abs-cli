namespace AbsCli.Api;

public static class ApiEndpoints
{
    public const string Login = "/login";
    public const string AuthRefresh = "/auth/refresh";

    public const string Libraries = "/api/libraries";
    public static string Library(string id) => $"/api/libraries/{id}";
    public static string LibraryItems(string libraryId) => $"/api/libraries/{libraryId}/items";
    public static string LibrarySeries(string libraryId) => $"/api/libraries/{libraryId}/series";
    public static string LibraryAuthors(string libraryId) => $"/api/libraries/{libraryId}/authors";
    public static string LibrarySearch(string libraryId) => $"/api/libraries/{libraryId}/search";

    public static string Item(string id) => $"/api/items/{id}";
    public static string ItemMedia(string id) => $"/api/items/{id}/media";
    public const string ItemsBatchUpdate = "/api/items/batch/update";
    public const string ItemsBatchGet = "/api/items/batch/get";

    public static string SeriesById(string id) => $"/api/series/{id}";
    public static string AuthorById(string id) => $"/api/authors/{id}";
}
