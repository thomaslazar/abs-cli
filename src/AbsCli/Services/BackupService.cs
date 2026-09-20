using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class BackupService
{
    // create/apply/download are sync server-side and can take minutes on large
    // libraries (SQLite dump capped at 2min, zip step uncapped). Override the default
    // 100s HTTP timeout so the CLI doesn't drop the connection while ABS is still
    // working. Upload opts out entirely — see UploadAsync.
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
        // StreamContent, not ByteArrayContent: a backup zip can exceed .NET's 2 GB
        // byte[] cap. The `using` is what closes the file handle.
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(File.OpenRead(filePath));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", System.IO.Path.GetFileName(filePath));
        // Infinite, unlike the other backup operations: the body size is the
        // operator's file, not something the server bounds, so 10 minutes is an
        // arbitrary wall for a multi-GB upload over a slow link.
        await _client.PostMultipartAsync(ApiEndpoints.BackupUpload, content, "'admin' access",
            timeout: Timeout.InfiniteTimeSpan);
        return await ListAsync();
    }
}
