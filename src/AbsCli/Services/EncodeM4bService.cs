using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class EncodeM4bService
{
    private readonly AbsApiClient _client;

    public EncodeM4bService(AbsApiClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Start an encode-m4b task on the given library item. POSTs to the
    /// endpoint with a query string built only from options the caller
    /// explicitly set, then returns a CLI-synthesised receipt (no follow-up
    /// HTTP — ABS returns empty 200).
    /// </summary>
    public async Task<EncodeM4bStartReceipt> StartAsync(string itemId, EncodeM4bOptions options)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(options.Codec))
            query.Add($"codec={Uri.EscapeDataString(options.Codec)}");
        if (!string.IsNullOrEmpty(options.Bitrate))
            query.Add($"bitrate={Uri.EscapeDataString(options.Bitrate)}");
        if (options.Channels.HasValue)
            query.Add($"channels={options.Channels.Value}");

        var endpoint = ApiEndpoints.ToolsItemEncodeM4b(itemId);
        if (query.Count > 0)
            endpoint += "?" + string.Join("&", query);

        await _client.PostEmptyAsync(endpoint, permissionHint: "admin permission");

        return new EncodeM4bStartReceipt
        {
            LibraryItemId = itemId,
            Action = "encode-m4b",
            Started = true,
            Options = options
        };
    }

    /// <summary>
    /// Cancel a pending encode-m4b task on the given library item. ABS
    /// returns 200 on success, 404 if either no task is pending or the
    /// item does not exist (the API does not distinguish). The
    /// <c>notFoundHint</c> surfaces the combined message via the generic
    /// 404 handler in <see cref="AbsApiClient"/>.
    /// </summary>
    public async Task CancelAsync(string itemId)
    {
        await _client.DeleteAsync(
            ApiEndpoints.ToolsItemEncodeM4b(itemId),
            permissionHint: "admin permission",
            notFoundHint: $"no pending encode-m4b task for item {itemId}, or item does not exist");
    }
}
