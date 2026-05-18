using System.Text.Json;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class EmbedMetadataService
{
    private readonly AbsApiClient _client;

    public EmbedMetadataService(AbsApiClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Start a single-item embed-metadata task. ABS POSTs an empty 200;
    /// we return a CLI-synthesised receipt. The caller decides whether
    /// to wait (via WaitForCompletionAsync) before printing.
    /// </summary>
    public async Task<EmbedMetadataReceipt> StartAsync(string itemId, EmbedMetadataOptions options)
    {
        var endpoint = ApiEndpoints.ToolsItemEmbedMetadata(itemId) + BuildQuery(options);
        await _client.PostEmptyAsync(endpoint, permissionHint: "admin permission");
        return new EmbedMetadataReceipt
        {
            LibraryItemId = itemId,
            Action = "embed-metadata",
            Started = true,
            Options = options
        };
    }

    /// <summary>
    /// Start a batch embed-metadata task. Body is the typed request
    /// (re-serialised through AppJsonContext for canonical shape).
    /// Same options apply uniformly across every item in the batch.
    /// </summary>
    public async Task<BatchEmbedMetadataReceipt> StartBatchAsync(
        BatchEmbedMetadataRequest request,
        EmbedMetadataOptions options)
    {
        var endpoint = ApiEndpoints.ToolsBatchEmbedMetadata + BuildQuery(options);
        var json = JsonSerializer.Serialize(request, AppJsonContext.Default.BatchEmbedMetadataRequest);
        await _client.PostAsync(endpoint, json, permissionHint: "admin permission");
        return new BatchEmbedMetadataReceipt
        {
            Action = "embed-metadata",
            Started = true,
            LibraryItemIds = request.LibraryItemIds,
            Options = options
        };
    }

    /// <summary>
    /// Poll /api/tasks until no task with action="embed-metadata" and
    /// data.libraryItemId in <paramref name="libraryItemIds"/> remains,
    /// or the timeout elapses.
    ///
    /// Returns true on success (all tasks gone), false on timeout.
    /// ABS removes completed tasks from /api/tasks regardless of
    /// success or failure (TaskManager.taskFinished is called by
    /// AudioMetadataManager after both setFinished and setFailed),
    /// so "tasks gone" means processing ended — it does NOT guarantee
    /// the embed succeeded.
    /// </summary>
    public async Task<bool> WaitForCompletionAsync(
        IReadOnlyList<string> libraryItemIds,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var tasksService = new TasksService(_client);
        var idSet = new HashSet<string>(libraryItemIds);
        var deadline = DateTime.UtcNow.Add(timeout);
        var pollInterval = TimeSpan.FromSeconds(2);

        while (DateTime.UtcNow < deadline)
        {
            var list = await tasksService.ListAsync();
            var pending = list.Tasks.Count(t =>
                t.Action == "embed-metadata" &&
                t.Data.HasValue &&
                t.Data.Value.TryGetProperty("libraryItemId", out var libProp) &&
                libProp.ValueKind == JsonValueKind.String &&
                idSet.Contains(libProp.GetString() ?? ""));

            if (pending == 0) return true;

            try { await Task.Delay(pollInterval, cancellationToken); }
            catch (TaskCanceledException) { return false; }
        }
        return false;
    }

    private static string BuildQuery(EmbedMetadataOptions options)
    {
        return $"?backup={(options.Backup ? 1 : 0)}&forceEmbedChapters={(options.ForceEmbedChapters ? 1 : 0)}";
    }
}
