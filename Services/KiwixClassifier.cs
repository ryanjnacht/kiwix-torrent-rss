using System.Text.RegularExpressions;
using kiwix_rss.Models;

namespace kiwix_rss.Services;

/// <summary>
///     Classifies ZIM entries as UNIQUE, LATEST, or OBSOLETE based on version history.
///     - UNIQUE: only version of this base name
///     - LATEST: newest version of a multi-version series
///     - OBSOLETE: older version (a newer version of the same base name exists)
///     Entries with the same base name but different sizes in the same version
///     (different flavours) are all marked LATEST, not obsolete.
/// </summary>
public class KiwixClassifier
{
    /// <summary>
    ///     Classifies all entries, setting their Status property.
    /// </summary>
    public void Classify(IList<KiwixEntry> entries)
    {
        // Group by base name, then by version within each group
        var groups = new Dictionary<string, Dictionary<string, List<KiwixEntry>>>();

        foreach (var entry in entries)
        {
            var baseName = ExtractBaseName(entry.Name);
            var version = entry.Version;

            if (!groups.TryGetValue(baseName, out var versionGroups))
            {
                versionGroups = new Dictionary<string, List<KiwixEntry>>();
                groups[baseName] = versionGroups;
            }

            if (!versionGroups.TryGetValue(version, out var group))
            {
                group = new List<KiwixEntry>();
                versionGroups[version] = group;
            }

            group.Add(entry);
        }

        // Classify each base name group
        foreach (var versionGroups in groups.Values)
            if (versionGroups.Count == 1)
            {
                // Only one version — all entries are UNIQUE
                var version = versionGroups.Keys.First();
                foreach (var entry in versionGroups[version]) entry.Status = "UNIQUE";
            }
            else
            {
                // Multiple versions — sort by version descending
                var sortedVersions = versionGroups.Keys.OrderByDescending(v => v).ToList();

                // First version is LATEST
                foreach (var entry in versionGroups[sortedVersions[0]]) entry.Status = "LATEST";

                // All older versions are OBSOLETE
                for (var i = 1; i < sortedVersions.Count; i++)
                    foreach (var entry in versionGroups[sortedVersions[i]])
                        entry.Status = "OBSOLETE";
            }
    }

    /// <summary>
    ///     Extracts the base name by removing the _YYYY-MM suffix.
    ///     Example: "wikipedia_en_all_2026-08" → "wikipedia_en_all"
    /// </summary>
    private static string ExtractBaseName(string name)
    {
        var match = Regex.Match(name, @"^(.+)_\d{4}-\d{2}$");
        return match.Success ? match.Groups[1].Value : name;
    }
}