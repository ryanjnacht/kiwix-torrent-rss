using kiwix_rss.Models;
using kiwix_rss.Services;
using Microsoft.Extensions.Options;

namespace kiwix_rss;

/// <summary>
///     Background hosted service that periodically scrapes the Kiwix OPDS catalog
///     and updates the RSS cache with the latest torrent listings.
/// </summary>
public class KiwixRssService : BackgroundService
{
    private readonly KiwixRssBuilder _builder;
    private readonly KiwixRssCache _cache;
    private readonly KiwixClassifier _classifier;
    private readonly ILogger<KiwixRssService> _logger;
    private readonly KiwixScraper _scraper;
    private readonly KiwixRssSettings _settings;
    private Timer? _timer;

    public KiwixRssService(
        ILogger<KiwixRssService> logger,
        KiwixScraper scraper,
        KiwixClassifier classifier,
        KiwixRssBuilder builder,
        KiwixRssCache cache,
        IOptions<KiwixRssSettings> settings)
    {
        _logger = logger;
        _scraper = scraper;
        _classifier = classifier;
        _builder = builder;
        _cache = cache;
        _settings = settings.Value;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(_settings.ScrapingIntervalHours);

        _timer = new Timer(
            async state => await ScrapeAndBuildAsync(stoppingToken),
            null,
            TimeSpan.Zero, // Run immediately on start
            interval // Then run at the configured interval
        );

        return Task.CompletedTask;
    }

    private async Task ScrapeAndBuildAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting Kiwix OPDS catalog scrape...");

            var entries = await _scraper.ScrapeAsync(cancellationToken);

            if (entries.Count == 0)
            {
                _logger.LogInformation("Catalog unchanged — no new entries to process.");
                return;
            }

            // Classify entries as UNIQUE, LATEST, or OBSOLETE
            _classifier.Classify(entries);

            var unique = entries.Count(e => e.Status == "UNIQUE");
            var latest = entries.Count(e => e.Status == "LATEST");
            var obsolete = entries.Count(e => e.Status == "OBSOLETE");

            _cache.SetEntries(entries);

            _logger.LogInformation(
                "Scrape complete: {Count} entries — {Unique} unique, {Latest} latest, {Obsolete} obsolete.",
                entries.Count, unique, latest, obsolete);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Kiwix catalog scrape.");
            // Keep the stale cache intact — don't overwrite with empty/error data
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        return base.StopAsync(cancellationToken);
    }
}