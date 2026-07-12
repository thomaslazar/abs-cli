namespace AbsCli.Api;

public static class ApiEndpoints
{
    public const string Login = "login";
    public const string AuthRefresh = "auth/refresh";

    public const string Libraries = "api/libraries";
    public static string Library(string id) => $"api/libraries/{id}";
    public static string LibraryItems(string libraryId) => $"api/libraries/{libraryId}/items";
    public static string LibrarySeries(string libraryId) => $"api/libraries/{libraryId}/series";
    public static string LibraryAuthors(string libraryId) => $"api/libraries/{libraryId}/authors";
    public static string LibrarySearch(string libraryId) => $"api/libraries/{libraryId}/search";

    public static string Item(string id) => $"api/items/{id}";
    public static string ItemMedia(string id) => $"api/items/{id}/media";
    public static string ItemCover(string id) => $"api/items/{id}/cover";
    public static string ItemChapters(string id) => $"api/items/{id}/chapters";
    public static string ItemEbookFileStatus(string id, string fileIno) => $"api/items/{id}/ebook/{fileIno}/status";
    public const string ItemsBatchUpdate = "api/items/batch/update";
    public const string ItemsBatchGet = "api/items/batch/get";
    public const string ItemsBatchDelete = "api/items/batch/delete";

    public static string SeriesById(string id) => $"api/series/{id}";
    public static string AuthorById(string id) => $"api/authors/{id}";
    public static string AuthorMatch(string id) => $"api/authors/{id}/match";
    public static string AuthorImage(string id) => $"api/authors/{id}/image";

    // Backup
    public const string Backups = "api/backups";
    public static string Backup(string id) => $"api/backups/{id}";
    public static string BackupApply(string id) => $"api/backups/{id}/apply";
    public static string BackupDownload(string id) => $"api/backups/{id}/download";
    public const string BackupUpload = "api/backups/upload";

    // Cache
    public const string CachePurgeItems = "api/cache/items/purge";
    public const string CachePurge = "api/cache/purge";

    // Upload
    public const string Upload = "api/upload";

    // Scan
    public static string LibraryScan(string libraryId) => $"api/libraries/{libraryId}/scan";
    public static string ItemScan(string id) => $"api/items/{id}/scan";

    // Metadata search
    public const string SearchBooks = "api/search/books";
    public const string SearchProviders = "api/search/providers";
    public const string SearchCovers = "api/search/covers";
    public const string SearchAuthors = "api/search/authors";
    public const string SearchChapters = "api/search/chapters";

    // Tools
    public static string ToolsItemEncodeM4b(string id) => $"api/tools/item/{id}/encode-m4b";
    public static string ToolsItemEmbedMetadata(string id) => $"api/tools/item/{id}/embed-metadata";
    public const string ToolsBatchEmbedMetadata = "api/tools/batch/embed-metadata";

    // Tasks
    public const string Tasks = "api/tasks";

    // Collections
    public const string Collections = "api/collections";
    public static string Collection(string id) => $"api/collections/{id}";
    public static string CollectionBook(string id) => $"api/collections/{id}/book";
    public static string CollectionBookById(string cid, string libraryItemId) => $"api/collections/{cid}/book/{libraryItemId}";
    public static string CollectionBatchAdd(string id) => $"api/collections/{id}/batch/add";
    public static string CollectionBatchRemove(string id) => $"api/collections/{id}/batch/remove";
    public static string LibraryCollections(string libraryId) => $"api/libraries/{libraryId}/collections";

    // Me + Progress
    public const string Me = "api/me";
    public static string MeProgress(string libraryItemId) => $"api/me/progress/{libraryItemId}";
    public static string MeProgressById(string progressId) => $"api/me/progress/{progressId}";
    public const string MeProgressBatchUpdate = "api/me/progress/batch/update";

    // Tags & Genres (all admin-only — MiscController.js gates every route on isAdminOrUp)
    public const string Tags = "api/tags";
    public const string TagRename = "api/tags/rename";
    public static string TagByName(string tag) => $"api/tags/{EncodePathValue(tag)}";
    public const string Genres = "api/genres";
    public const string GenreRename = "api/genres/rename";
    public static string GenreByName(string genre) => $"api/genres/{EncodePathValue(genre)}";

    // ABS decodes the :tag / :genre param via
    // Buffer.from(decodeURIComponent(param), 'base64'), so base64-encode the
    // value then URI-escape it into the path segment.
    private static string EncodePathValue(string value)
        => Uri.EscapeDataString(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value)));
}
