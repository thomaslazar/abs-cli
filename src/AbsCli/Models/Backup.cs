using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class BackupItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    [JsonPropertyName("backupDirPath")]
    public string? BackupDirPath { get; set; }
    [JsonPropertyName("datePretty")]
    public string? DatePretty { get; set; }
    [JsonPropertyName("fullPath")]
    public string? FullPath { get; set; }
    [JsonPropertyName("path")]
    public string? Path { get; set; }
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }
    [JsonPropertyName("fileSize")]
    public long? FileSize { get; set; }
    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; set; }
    [JsonPropertyName("serverVersion")]
    public string? ServerVersion { get; set; }
}

public class BackupListResponse
{
    [JsonPropertyName("backups")]
    public List<BackupItem> Backups { get; set; } = new();
    [JsonPropertyName("backupLocation")]
    public string? BackupLocation { get; set; }
    [JsonPropertyName("backupPathEnvSet")]
    public bool? BackupPathEnvSet { get; set; }
}
