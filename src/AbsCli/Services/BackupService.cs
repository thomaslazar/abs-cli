using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class BackupService
{
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
            AppJsonContext.Default.BackupListResponse, "'admin' access");
    }

    public async Task<string> ApplyAsync(string id)
    {
        return await _client.GetAsync(ApiEndpoints.BackupApply(id), "'admin' access");
    }

    public async Task DownloadAsync(string id, string outputPath)
    {
        await _client.DownloadFileAsync(ApiEndpoints.BackupDownload(id), outputPath, "'admin' access");
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
        await _client.PostMultipartAsync(ApiEndpoints.BackupUpload, content, "'admin' access");
        return await ListAsync();
    }
}
