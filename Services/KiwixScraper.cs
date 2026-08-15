using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using kiwix_rss.Models;
using Microsoft.Extensions.Options;

namespace kiwix_rss.Services;

/// <summary>
///     Fetches and parses the Kiwix OPDS catalog to extract ZIM file entries.
/// </summary>
public class KiwixScraper
{
    private readonly HttpClient _httpClient;
    private readonly KiwixRssSettings _settings;

    public KiwixScraper(HttpClient httpClient, IOptions<KiwixRssSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    /// <summary>
    ///     Fetches the OPDS catalog and returns all ZIM file entries.
    ///     Uses ETag caching to skip unchanged catalogs.
    /// </summary>
    public async Task<IList<KiwixEntry>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var url = _settings.CatalogUrl;
        var entries = new List<KiwixEntry>();

        var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        // If catalog hasn't changed (304 Not Modified), return empty list
        if (response.StatusCode == HttpStatusCode.NotModified) return entries;

        response.EnsureSuccessStatusCode();

        var xmlContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var feed = XElement.Parse(xmlContent);

        // Atom default namespace
        var atomNs = XNamespace.Get("http://www.w3.org/2005/Atom");
        // Dublin Core namespace used by OPDS
        var dcNs = XNamespace.Get("http://purl.org/dc/terms/");

        var entryElements = feed.Elements(atomNs + "entry").ToList();

        foreach (var entry in entryElements)
        {
            var entryData = new KiwixEntry
            {
                Id = GetElementValue(entry, "id") ?? string.Empty,
                Title = GetElementValue(entry, "title") ?? string.Empty,
                Name = GetElementValue(entry, "name") ?? string.Empty,
                Description = GetElementValue(entry, "summary") ?? string.Empty,
                Author = GetElementValue(entry, "author") ?? string.Empty,
                Publisher = GetElementValue(entry, "publisher") ?? string.Empty,
                Language = GetElementValue(entry, "language") ?? string.Empty,
                Flavour = GetElementValue(entry, "flavour") ?? string.Empty,
                Category = GetElementValue(entry, "category") ?? string.Empty,
                Tags = GetElementValue(entry, "tags") ?? string.Empty,
                Published = ParsePublished(entry, atomNs, dcNs)
            };

            // Extract the ZIM download link (type="application/x-zim")
            var zimLink = entry.Elements(atomNs + "link")
                .FirstOrDefault(l => l.Attribute("type")?.Value == "application/x-zim");

            if (zimLink != null)
            {
                entryData.ZimUrl = zimLink.Attribute("href")?.Value ?? string.Empty;
                if (long.TryParse(zimLink.Attribute("length")?.Value, out var size)) entryData.SizeBytes = size;
            }

            // Extract version from the filename (last _YYYY-MM segment)
            entryData.Version = ExtractVersion(entryData.Name, entryData.ZimUrl);

            entries.Add(entryData);
        }

        return entries;
    }

    /// <summary>
    ///     Gets the text value of a child element in the Atom namespace, handling nested elements like author/name.
    /// </summary>
    private static string? GetElementValue(XElement parent, string localName)
    {
        var ns = XNamespace.Get("http://www.w3.org/2005/Atom");
        var element = parent.Element(ns + localName);

        if (element == null)
            return null;

        // Handle nested elements (e.g., author/name, publisher/name)
        var childElement = element.Elements().FirstOrDefault();
        if (childElement != null)
            return childElement.Value;

        return element.Value;
    }

    /// <summary>
    ///     Parses the publication date from either the Atom 'updated' field or the Dublin Core 'issued' field.
    /// </summary>
    private static DateTimeOffset ParsePublished(XElement entry, XNamespace atomNs, XNamespace dcNs)
    {
        // Try Atom 'updated' first
        var updated = entry.Element(atomNs + "updated")?.Value;
        if (!string.IsNullOrEmpty(updated) && DateTimeOffset.TryParse(updated, out var date)) return date;

        // Fall back to Dublin Core 'issued'
        var issued = entry.Element(dcNs + "issued")?.Value;
        if (!string.IsNullOrEmpty(issued) && DateTimeOffset.TryParse(issued, out var dcDate)) return dcDate;

        return DateTime.UtcNow;
    }

    /// <summary>
    ///     Extracts the version string (e.g., "2024-06") from the name or URL.
    /// </summary>
    private static string ExtractVersion(string name, string url)
    {
        // Try to find _YYYY-MM pattern in the URL or name
        var source = string.IsNullOrEmpty(url) ? name : url;
        var match = Regex.Match(source, @"_(\d{4}-\d{2})\.");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}