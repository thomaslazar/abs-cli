using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class BackupService
{
    // create/apply/download/upload are sync server-side and can take minutes on large
    // libraries (SQLite dump capped at 2min, zip step uncapped). Override the default
    // 100s HTTP timeout so the CLI doesn't drop the connection while ABS is still working.
    private static readonly TimeSpan LongOperationTimeout = TimeSpan.FromMinutes(10);

    private readonly AbsApiClient _client;

    public BackupService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<BackupListResponse> ListAsync()
    {
        return await _client.GetAsync(ApiEndpoints.Backups,
            AppJsonContext.Default.BackupListResponse, "'admin' access");
    }

    public async Task<BackupListResponse> CreateAsync()
    {
        return await _client.PostEmptyAsync(ApiEndpoints.Backups,
            AppJsonContext.Default.BackupListResponse, "'admin' access",
            timeout: LongOperationTimeout);
    }

    public async Task<string> ApplyAsync(string id)
    {
        return await _client.GetAsync(ApiEndpoints.BackupApply(id), "'admin' access",
            timeout: LongOperationTimeout);
    }

    public async Task DownloadAsync(string id, string outputPath)
    {
        await _client.DownloadFileAsync(ApiEndpoints.BackupDownload(id), outputPath, "'admin' access",
            timeout: LongOperationTimeout);
    }

    public async Task<BackupListResponse> DeleteAsync(string id)
    {
        return await _client.DeleteAsync(ApiEndpoints.Backup(id),
            AppJsonContext.Default.BackupListResponse, "'admin' access");
    }

    public async Task<BackupListResponse> UploadAsync(string filePath)
    {
        var content = new MultipartFormDataContent();
        var fileBytes = await File.ReadAllBytesAsync(filePath);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", System.IO.Path.GetFileName(filePath));
        await _client.PostMultipartAsync(ApiEndpoints.BackupUpload, content, "'admin' access",
            timeout: LongOperationTimeout);
        return await ListAsync();
    }
}
