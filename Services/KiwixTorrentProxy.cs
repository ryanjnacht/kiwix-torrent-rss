namespace kiwix_rss.Services;

/// <summary>
///     Proxies torrent file downloads through the app.
///     Solves old uTorrent TLS compatibility issues with the Kiwix download server.
/// </summary>
public class KiwixTorrentProxy
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KiwixTorrentProxy> _logger;

    public KiwixTorrentProxy(HttpClient httpClient, ILogger<KiwixTorrentProxy> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    ///     Proxies a torrent file download, streaming the response directly to the client.
    /// </summary>
    public async Task<IResult> ProxyTorrentAsync(string url, HttpContext httpContext)
    {
        _logger.LogDebug("Proxying torrent download: {Url}", url);

        try
        {
            var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead,
                httpContext.RequestAborted);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsByteArrayAsync(httpContext.RequestAborted);

            _logger.LogDebug("Torrent downloaded: {Url} ({Size} bytes)", url, content.Length);

            return Results.File(content, "application/x-bittorrent", GetFileName(url));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch torrent: {Url}", url);
            throw;
        }
    }

    /// <summary>
    ///     Extracts a filename from the torrent URL for the Content-Disposition header.
    /// </summary>
    private static string GetFileName(string url)
    {
        var uri = new Uri(url);
        var path = uri.AbsolutePath;
        var fileName = Path.GetFileName(path);
        return string.IsNullOrEmpty(fileName) ? "download.torrent" : fileName;
    }
}