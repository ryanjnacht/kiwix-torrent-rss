using kiwix_rss.Models;

namespace kiwix_rss.Services;

/// <summary>
///     Thread-safe cache for the scraped Kiwix ZIM entries.
///     RSS XML is generated on-demand from these entries, allowing
///     optional query-string filtering at request time.
/// </summary>
public class KiwixRssCache
{
    private volatile IList<KiwixEntry>? _entries;

    /// <summary>
    ///     Gets the cached ZIM entries, or null if no scrape has completed yet.
    /// </summary>
    public IList<KiwixEntry>? GetEntries()
    {
        return _entries;
    }

    /// <summary>
    ///     Updates the cache with newly scraped entries.
    ///     This operation is thread-safe via volatile write.
    /// </summary>
    public void SetEntries(IList<KiwixEntry> entries)
    {
        _entries = entries;
    }

    /// <summary>
    ///     Clears the cache.
    /// </summary>
    public void Clear()
    {
        _entries = null;
    }
}