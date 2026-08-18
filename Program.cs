using kiwix_rss;
using kiwix_rss.Models;
using kiwix_rss.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on port 80
builder.WebHost.ConfigureKestrel(options => { options.ListenAnyIP(80); });

// Bind configuration section
builder.Services.Configure<KiwixRssSettings>(
    builder.Configuration.GetSection(KiwixRssSettings.SectionName));

// Register services
builder.Services.AddHostedService<KiwixRssService>();
builder.Services.AddSingleton<KiwixRssCache>();
builder.Services.AddSingleton<KiwixScraper>();
builder.Services.AddSingleton<KiwixClassifier>();
builder.Services.AddSingleton<KiwixRssBuilder>();

// HttpClient for the OPDS catalog scraper (with retry support)
builder.Services.AddHttpClient<KiwixScraper>((_provider, client) =>
{
    client.BaseAddress = new Uri("https://opds.library.kiwix.org/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// HttpClient for the torrent proxy (with larger timeout for large files)
builder.Services.AddHttpClient<KiwixTorrentProxy>((_, client) => { client.Timeout = TimeSpan.FromMinutes(5); });

var app = builder.Build();

// /kiwix.rss endpoint - serves RSS feed with optional ?q= filter and ?proxy=true
// Torrent URLs are rewritten to proxy through this app only when ?proxy=true
app.MapGet("/kiwix.rss", (KiwixRssCache cache, KiwixRssBuilder builder, HttpContext httpContext) =>
{
    var entries = cache.GetEntries();
    if (entries is null || entries.Count == 0) return Results.StatusCode(503); // Service not ready yet

    var q = httpContext.Request.Query.TryGetValue("q", out var queryString) ? queryString[0] : null;
    var proxyBaseUrl = httpContext.Request.Query.TryGetValue("proxy", out var proxyQuery) && proxyQuery[0] == "true"
        ? GetProxyBaseUrl(httpContext)
        : null;
    var customFormat = httpContext.Request.Query.TryGetValue("format", out var formatQuery) ? formatQuery[0] : null;
    var rss = builder.Build(entries, q, proxyBaseUrl, customFormat);
    return Results.Content(rss, "application/xml");
});

// /proxy/torrent endpoint - proxies torrent file downloads through the app
// Solves old uTorrent TLS compatibility issues with the Kiwix server
app.MapGet("/proxy/torrent", async (KiwixTorrentProxy proxy, HttpContext httpContext) =>
{
    var url = httpContext.Request.Query.TryGetValue("u", out var urlQuery) ? urlQuery[0] : null;
    if (string.IsNullOrEmpty(url)) return Results.BadRequest("Missing 'u' query parameter with the torrent URL.");

    try
    {
        return await proxy.ProxyTorrentAsync(url, httpContext);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to fetch torrent: {ex.Message}", statusCode: 502);
    }
});

// 404 catch-all for all other routes
app.MapFallback(() => Results.NotFound());

app.Run();

static string GetProxyBaseUrl(HttpContext context)
{
    var scheme = context.Request.Scheme;
    var host = context.Request.Host.Host;
    var port = context.Request.Host.Port;

    // Only include port if it's non-standard
    if (port.HasValue && port != (scheme == "https" ? 443 : 80)) return $"{scheme}://{host}:{port}";
    return $"{scheme}://{host}";
}