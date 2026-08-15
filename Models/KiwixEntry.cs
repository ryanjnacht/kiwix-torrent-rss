using System.Net;

namespace kiwix_rss.Models;

/// <summary>
///     Represents a single ZIM file entry from the Kiwix OPDS catalog.
/// </summary>
public class KiwixEntry
{
    /// <summary>
    ///     Unique UUID for this entry (e.g., "urn:uuid:0002ed21-81ff-39eb-7274-d80240a8ea78").
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     Display title (e.g., "Wikipedia").
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     Internal name used in the filename (e.g., "wikipedia_en_all_nopic").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Human-readable description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     Author/creator name.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    ///     Publisher name (e.g., "openZIM").
    /// </summary>
    public string Publisher { get; set; } = string.Empty;

    /// <summary>
    ///     ISO 639-1 language code (e.g., "eng", "fra").
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    ///     Flavour: "mini", "nopic", or "maxi".
    /// </summary>
    public string Flavour { get; set; } = string.Empty;

    /// <summary>
    ///     Category (e.g., "wikipedia", "howto").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    ///     Semicolon-separated tags (e.g., "wikipedia;_category:wikipedia;_pictures:yes").
    /// </summary>
    public string Tags { get; set; } = string.Empty;

    /// <summary>
    ///     Publication date extracted from the updated/issued field (e.g., "2024-06").
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    ///     ZIM file download URL (e.g., "https://lb.download.kiwix.org/zim/.../file.zim.meta4").
    /// </summary>
    public string ZimUrl { get; set; } = string.Empty;

    /// <summary>
    ///     File size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    ///     Publication date as DateTimeOffset for RSS pubDate formatting.
    /// </summary>
    public DateTimeOffset Published { get; set; }

    /// <summary>
    ///     Classification: "UNIQUE" (only version), "LATEST" (newest in series), or "OBSOLETE" (replaced by newer version).
    /// </summary>
    public string Status { get; set; } = "UNIQUE";

    /// <summary>
    ///     The torrent download URL, derived from the ZIM URL by replacing ".meta4" with ".torrent".
    /// </summary>
    public string TorrentUrl => ZimUrl.Replace(".meta4", ".torrent");

    /// <summary>
    ///     The proxied torrent URL, rewritten to go through the local app.
    ///     Format: {proxyBaseUrl}/proxy/torrent?u={originalTorrentUrl}
    /// </summary>
    public string ProxiedTorrentUrl(string proxyBaseUrl)
    {
        return $"{proxyBaseUrl}/proxy/torrent?u={WebUtility.UrlEncode(TorrentUrl)}";
    }
}