using System.Text.Json;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class ChaptersService
{
    private readonly AbsApiClient _client;

    public ChaptersService(AbsApiClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Look up chapter timings on Audnexus by ASIN.
    ///
    /// ABS's GET /api/search/chapters returns HTTP 200 in three failure
    /// modes — no Audnexus match, invalid ASIN, invalid region — each
    /// with an in-band {error, stringKey} body. We detect those by
    /// deserialising into the error shape first; if Error is non-null,
    /// the response is a failure variant. Otherwise re-parse as the
    /// success shape.
    ///
    /// HTTP 500 (Audnexus-side fault) is handled by AbsApiClient's
    /// generic 5xx path before we get here.
    /// </summary>
    public async Task<ChaptersLookupResult> LookupAsync(string asin, string? region)
    {
        var url = ApiEndpoints.SearchChapters + "?asin=" + Uri.EscapeDataString(asin);
        if (!string.IsNullOrEmpty(region))
            url += "&region=" + Uri.EscapeDataString(region);

        var json = await _client.GetAsync(url);

        var maybeError = JsonSerializer.Deserialize(json, AppJsonContext.Default.ChaptersLookupError)!;
        if (!string.IsNullOrEmpty(maybeError.Error))
            return new ChaptersLookupResult { Error = maybeError };

        var success = JsonSerializer.Deserialize(json, AppJsonContext.Default.ChaptersLookupResponse)!;
        return new ChaptersLookupResult { Success = success };
    }

    /// <summary>
    /// Write chapters onto a library item. The caller passes a JSON
    /// body that has already been deserialised + re-serialised through
    /// ChaptersSetRequest (the CLI's shape gate). ABS validates the
    /// body itself (LibraryItemController.js:861) and returns 500
    /// (not 400/404) for missing/non-book/no-audio items
    /// (LibraryItemController.js:856-858) — that quirk is documented
    /// in the verb's help.
    /// </summary>
    public async Task<ChaptersSetResponse> SetAsync(string itemId, string jsonBody)
    {
        return await _client.PostAsync(
            ApiEndpoints.ItemChapters(itemId),
            jsonBody,
            AppJsonContext.Default.ChaptersSetResponse,
            permissionHint: "'update' permission");
    }
}
