namespace kiwix_rss.Models;

/// <summary>
///     Configuration settings for the Kiwix RSS service.
/// </summary>
public class KiwixRssSettings
{
    public const string SectionName = "KiwixRss";

    /// <summary>
    ///     Interval in hours between scrapes (default: 24).
    /// </summary>
    public int ScrapingIntervalHours { get; set; } = 24;

    /// <summary>
    ///     OPDS catalog API URL.
    /// </summary>
    public string CatalogUrl { get; set; } = "https://opds.library.kiwix.org/catalog/v2/entries?count=-1";

    /// <summary>
    ///     Base URL for downloads (used to construct torrent URLs).
    /// </summary>
    public string DownloadBaseUrl { get; set; } = "https://lb.download.kiwix.org";

    /// <summary>
    ///     Title for the RSS feed.
    /// </summary>
    public string FeedTitle { get; set; } = "Kiwix Torrent RSS";

    /// <summary>
    ///     Link URL for the RSS feed.
    /// </summary>
    public string FeedLink { get; set; } = "https://kiwix.org";

    /// <summary>
    ///     Description for the RSS feed.
    /// </summary>
    public string FeedDescription { get; set; } = "Kiwix ZIM Files - Torrent Downloads";

    /// <summary>
    ///     Format template for individual RSS item titles.
    ///     Use field names separated by '+' (e.g., "title+language+version").
    ///     Supported fields: title, name, language, version, status, category, flavour, size.
    ///     Empty fields are omitted. Uses the current default format if not set.
    /// </summary>
    public string? FeedItemTitleFormat { get; set; }
}