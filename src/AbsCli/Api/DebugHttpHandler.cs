namespace AbsCli.Api;

internal sealed class DebugHttpHandler : DelegatingHandler
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

    public DebugHttpHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (!_logger.IsDebugEnabled) return response;

        var method = request.Method.Method;
        // Full absolute URL — HttpClient has resolved any relative URI
        // against BaseAddress before the handler sees the request.
        var url = request.RequestUri?.AbsoluteUri ?? "";
        var status = (int)response.StatusCode;
        _logger.Debug($"{method} {url} {status}");

        if (status >= 400)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var truncated = body.Length > 500
                ? body[..500] + "..."
                : body;
            _logger.Debug($"response body: {truncated}");
        }
        return response;
    }
}
