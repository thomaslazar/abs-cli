using System.Text.Json.Serialization;
using AbsCli.Configuration;

namespace AbsCli.Models;

[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(LibraryListResponse))]
[JsonSerializable(typeof(Library))]
[JsonSerializable(typeof(PaginatedResponse))]
[JsonSerializable(typeof(LibraryItemMinified))]
[JsonSerializable(typeof(BookMediaMinified))]
[JsonSerializable(typeof(PodcastMedia))]
[JsonSerializable(typeof(SearchResult))]
[JsonSerializable(typeof(UpdateMediaResponse))]
[JsonSerializable(typeof(BatchUpdateResponse))]
[JsonSerializable(typeof(BatchGetResponse))]
[JsonSerializable(typeof(SeriesItem))]
[JsonSerializable(typeof(AuthorItem))]
[JsonSerializable(typeof(AuthorListResponse))]
[JsonSerializable(typeof(BackupItem))]
[JsonSerializable(typeof(BackupListResponse))]
[JsonSerializable(typeof(ScanResult))]
[JsonSerializable(typeof(TaskItem))]
[JsonSerializable(typeof(TaskListResponse))]
[JsonSerializable(typeof(ProviderEntry))]
[JsonSerializable(typeof(MetadataProviderGroups))]
[JsonSerializable(typeof(MetadataProvidersResponse))]
[JsonSerializable(typeof(CoverSearchResponse))]
[JsonSerializable(typeof(UploadManifestEntry))]
[JsonSerializable(typeof(UploadReceipt))]
[JsonSerializable(typeof(List<UploadManifestEntry>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
public partial class AppJsonContext : JsonSerializerContext;

public class LoginRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";
}
