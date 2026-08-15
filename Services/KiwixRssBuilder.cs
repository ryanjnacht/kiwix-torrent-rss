using System.Globalization;
using System.Text;
using System.Xml;
using kiwix_rss.Models;
using Microsoft.Extensions.Options;

namespace kiwix_rss.Services;

/// <summary>
///     Builds an RSS 2.0 XML document from Kiwix ZIM entries.
///     Output is compact (single-line) for compatibility with torrent clients.
/// </summary>
public class KiwixRssBuilder
{
    private readonly KiwixRssSettings _settings;

    public KiwixRssBuilder(IOptions<KiwixRssSettings> settings)
    {
        _settings = settings.Value;
    }

    /// <summary>
    ///     Builds the RSS XML string from the given entries, sorted by publication date (newest first).
    ///     Optionally filters entries by a query string matched against the formatted title.
    ///     The proxyBaseUrl rewrites torrent URLs to route through this app.
    /// </summary>
    public string Build(IList<KiwixEntry> entries, string? query = null, string? proxyBaseUrl = null)
    {
        var filtered = string.IsNullOrEmpty(query)
            ? entries
            : entries.Where(e => MatchesAllTerms(e, query))
                .ToList();

        var sortedEntries = filtered
            .OrderByDescending(e => e.Published)
            .ToList();

        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(sb, new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = false,
            Encoding = Encoding.UTF8
        });

        // XML declaration
        writer.WriteStartDocument();
        // XML declaration (XmlWriter doesn't write it when OmitXmlDeclaration=true)
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");

        writer.WriteStartElement("rss");
        writer.WriteAttributeString("version", "2.0");

        writer.WriteStartElement("channel");
        writer.WriteElementString("title", _settings.FeedTitle);
        writer.WriteElementString("link", _settings.FeedLink);
        writer.WriteElementString("description", _settings.FeedDescription);
        writer.WriteElementString("lastBuildDate", FormatRfc822(DateTime.UtcNow));

        foreach (var entry in sortedEntries) WriteItem(writer, entry, proxyBaseUrl);

        writer.WriteEndElement(); // channel
        writer.WriteEndElement(); // rss
        writer.Flush();

        return sb.ToString();
    }

    /// <summary>
    ///     Writes a single RSS &lt;item&gt; element for a ZIM entry.
    /// </summary>
    private static void WriteItem(XmlWriter writer, KiwixEntry entry, string? proxyBaseUrl)
    {
        var torrentUrl = string.IsNullOrEmpty(proxyBaseUrl)
            ? entry.TorrentUrl
            : entry.ProxiedTorrentUrl(proxyBaseUrl);

        writer.WriteStartElement("item");

        writer.WriteElementString("title", FormatTitle(entry));
        writer.WriteElementString("link", torrentUrl);

        writer.WriteStartElement("guid");
        writer.WriteAttributeString("isPermaLink", "true");
        writer.WriteString(entry.Id);
        writer.WriteEndElement();

        writer.WriteElementString("pubDate", FormatRfc822(entry.Published));
        writer.WriteElementString("description", FormatDescription(entry));

        writer.WriteStartElement("enclosure");
        writer.WriteAttributeString("url", torrentUrl);
        writer.WriteAttributeString("length", entry.SizeBytes.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("type", "application/x-bittorrent");
        writer.WriteEndElement();

        writer.WriteEndElement(); // item
    }

    /// <summary>
    ///     Formats the RSS title with display name, filename, language, version, status, and description.
    ///     Example: "Python PEPs - peps.python_en_all_2026-08.zim (eng, 2026-08) [LATEST] - 8 MB; other (standard)"
    /// </summary>
    private static string FormatTitle(KiwixEntry entry)
    {
        var name = string.IsNullOrEmpty(entry.Name) ? "unknown" : entry.Name;
        var version = string.IsNullOrEmpty(entry.Version) ? "" : $"_{entry.Version}";
        var filename = $"{name}{version}";
        var language = string.IsNullOrEmpty(entry.Language) ? "????" : entry.Language;
        var versionPart = string.IsNullOrEmpty(entry.Version) ? "" : $", {entry.Version}";
        var status = string.IsNullOrEmpty(entry.Status) ? "" : $" [{entry.Status}]";
        var description = FormatDescription(entry);
        return $"{entry.Title} - {filename}.zim ({language}{versionPart}){status} - {description}";
    }

    /// <summary>
    ///     Checks whether the entry's title contains all terms from the query.
    ///     Terms are separated by whitespace; each must match case-insensitively.
    ///     Example: "2026 eng" matches only entries containing both "2026" AND "eng".
    /// </summary>
    private static bool MatchesAllTerms(KiwixEntry entry, string query)
    {
        var title = FormatTitle(entry);
        var terms = query.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        return terms.All(t => title.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Formats a Kiwix entry as a human-readable description string,
    ///     Format: "{size}GB/MB; {category} ({flavour})".
    /// </summary>
    private static string FormatDescription(KiwixEntry entry)
    {
        var size = FormatSize(entry.SizeBytes);
        var category = string.IsNullOrEmpty(entry.Category) ? "Other" : entry.Category;
        var flavour = string.IsNullOrEmpty(entry.Flavour) ? "standard" : entry.Flavour;
        return $"{size}; {category} ({flavour})";
    }

    /// <summary>
    ///     Formats a byte count as a human-readable size string (e.g., "1.23 GB", "425 MB").
    /// </summary>
    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "Unknown";

        var magnitude = Math.Floor(Math.Log(bytes, 1024));
        if (magnitude >= 3)
        {
            var value = bytes / Math.Pow(1024, magnitude);
            return
                $"{value.ToString("F2", CultureInfo.InvariantCulture).Replace(',', '.').TrimEnd('0').TrimEnd('.')} {new[] { "B", "KB", "MB", "GB", "TB" }[(int)magnitude]}";
        }

        return $"{bytes / (1024 * 1024)} MB";
    }

    /// <summary>
    ///     Formats a DateTime as RFC 822 format suitable for RSS pubDate.
    ///     Example: "Thu, 13 Aug 2026 14:18:31 +0000"
    /// </summary>
    private static string FormatRfc822(DateTimeOffset dateTime)
    {
        // Use RFC 1123 pattern which is compatible with RSS 2.0
        return dateTime.ToString("R", CultureInfo.InvariantCulture);
    }
}