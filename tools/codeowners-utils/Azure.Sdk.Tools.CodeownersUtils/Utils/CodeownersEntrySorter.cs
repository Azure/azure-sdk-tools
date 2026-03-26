using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.CodeownersUtils.Utils;

/// <summary>
/// Provides sorting functionality for CODEOWNERS entries.
/// </summary>
public static class CodeownersEntrySorter
{
    /// <summary>
    /// Sorts entries with label grouping:
    /// 1. Pathed entries sorted by normalized path
    /// 2. Pathless entries with matching labels placed after last matching pathed entry
    /// 3. Remaining pathless entries at the bottom sorted by ServiceLabel
    /// </summary>
    public static List<CodeownersEntry> SortEntries(List<CodeownersEntry> entries)
    {
        var pathedEntries = entries.Where(e => !string.IsNullOrWhiteSpace(e.PathExpression)).ToList();
        var pathlessEntries = entries.Where(e => string.IsNullOrWhiteSpace(e.PathExpression)).ToList();

        // Collect all labels from pathed entries
        var pathedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in pathedEntries)
        {
            AddLabelsToSet(entry.PRLabels, pathedLabels);
            AddLabelsToSet(entry.ServiceLabels, pathedLabels);
        }

        // Separate pathless entries into matching and non-matching
        var (matchingPathless, nonMatchingPathless) = PartitionPathlessEntries(pathlessEntries, pathedLabels);

        // Sort all entry groups
        pathedEntries = SortByPathThenContent(pathedEntries);
        matchingPathless = SortByLabelsThenContent(matchingPathless);
        nonMatchingPathless = SortByLabelsThenContent(nonMatchingPathless);

        // Build result: pathed entries with matching pathless inserted after their last matching pathed entry
        var result = BuildResultWithInsertions(pathedEntries, matchingPathless);

        // Add remaining non-matching pathless entries at the end
        result.AddRange(nonMatchingPathless);

        return result;
    }

    /// <summary>
    /// Sorts owners within each entry alphabetically.
    /// </summary>
    public static void SortOwnersInPlace(IEnumerable<CodeownersEntry> entries)
    {
        foreach (var entry in entries)
        {
            entry.SourceOwners.Sort(StringComparer.OrdinalIgnoreCase);
            entry.ServiceOwners.Sort(StringComparer.OrdinalIgnoreCase);
            entry.AzureSdkOwners.Sort(StringComparer.OrdinalIgnoreCase);
            entry.OriginalSourceOwners.Sort(StringComparer.OrdinalIgnoreCase);
            entry.OriginalServiceOwners.Sort(StringComparer.OrdinalIgnoreCase);
            entry.OriginalAzureSdkOwners.Sort(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Sorts labels within each entry alphabetically.
    /// </summary>
    public static void SortLabelsInPlace(IEnumerable<CodeownersEntry> entries)
    {
        foreach (var entry in entries)
        {
            entry.ServiceLabels.Sort(StringComparer.OrdinalIgnoreCase);
            entry.PRLabels.Sort(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Normalizes a path to have leading and trailing slashes.
    /// </summary>
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/";

        var normalized = path.Trim();
        if (!normalized.StartsWith("/"))
            normalized = "/" + normalized;
        if (!normalized.EndsWith("/") && !Path.HasExtension(normalized) && !normalized.Contains('*'))
            normalized += "/";

        return normalized;
    }

    /// <summary>
    /// Normalizes a label by removing % prefix and trimming.
    /// </summary>
    public static string NormalizeLabel(string label) => label.TrimStart('%').Trim();

    /// <summary>
    /// Gets the primary label (first ServiceLabel, or first PRLabel if no ServiceLabels).
    /// </summary>
    public static string GetPrimaryLabel(CodeownersEntry entry)
    {
        if (entry.ServiceLabels?.Count > 0)
            return NormalizeLabel(entry.ServiceLabels[0]);
        if (entry.PRLabels?.Count > 0)
            return NormalizeLabel(entry.PRLabels[0]);
        return string.Empty;
    }

    /// <summary>
    /// Gets all labels as a single string for sorting.
    /// </summary>
    public static string GetAllLabelsString(CodeownersEntry entry)
    {
        var labels = new List<string>();

        if (entry.ServiceLabels != null)
            labels.AddRange(entry.ServiceLabels.Select(NormalizeLabel));
        if (entry.PRLabels != null)
            labels.AddRange(entry.PRLabels.Select(NormalizeLabel));

        labels.Sort(StringComparer.OrdinalIgnoreCase);
        return string.Join("|", labels);
    }

    private static void AddLabelsToSet(List<string> labels, HashSet<string> set)
    {
        if (labels == null) return;
        foreach (var label in labels)
            set.Add(NormalizeLabel(label));
    }

    private static (List<CodeownersEntry> matching, List<CodeownersEntry> nonMatching) PartitionPathlessEntries(
        List<CodeownersEntry> pathlessEntries,
        HashSet<string> pathedLabels)
    {
        var matching = new List<CodeownersEntry>();
        var nonMatching = new List<CodeownersEntry>();

        foreach (var entry in pathlessEntries)
        {
            bool hasMatch = entry.ServiceLabels?.Any(l => pathedLabels.Contains(NormalizeLabel(l))) ?? false;
            (hasMatch ? matching : nonMatching).Add(entry);
        }

        return (matching, nonMatching);
    }

    private static List<CodeownersEntry> SortByPathThenContent(List<CodeownersEntry> entries)
    {
        return entries
            .OrderBy(GetPrimaryLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => NormalizePath(e.PathExpression), StringComparer.Ordinal)
            .ThenBy(e => e.FormatCodeownersEntry(useOriginalOwners: true), StringComparer.Ordinal)
            .ToList();
    }

    private static List<CodeownersEntry> SortByLabelsThenContent(List<CodeownersEntry> entries)
    {
        return entries
            .OrderBy(GetAllLabelsString, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.FormatCodeownersEntry(useOriginalOwners: true), StringComparer.Ordinal)
            .ToList();
    }

    private static List<CodeownersEntry> BuildResultWithInsertions(
        List<CodeownersEntry> pathedEntries,
        List<CodeownersEntry> matchingPathless)
    {
        var result = new List<CodeownersEntry>(pathedEntries);

        // Track which labels appear at which index
        var labelLastIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < result.Count; i++)
        {
            var entry = result[i];
            UpdateLabelIndex(entry.PRLabels, i, labelLastIndex);
            UpdateLabelIndex(entry.ServiceLabels, i, labelLastIndex);
        }

        // Find insertion points for matching pathless entries
        var insertions = new List<(int insertAfter, CodeownersEntry entry)>();
        foreach (var entry in matchingPathless)
        {
            int bestIndex = FindBestInsertionIndex(entry.ServiceLabels, labelLastIndex);
            if (bestIndex >= 0)
                insertions.Add((bestIndex, entry));
        }

        // Group by position and insert from back to front
        var groups = insertions
            .GroupBy(x => x.insertAfter)
            .OrderByDescending(g => g.Key);

        foreach (var group in groups)
        {
            var entriesToInsert = group
                .Select(x => x.entry)
                .OrderBy(GetAllLabelsString, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.FormatCodeownersEntry(useOriginalOwners: true), StringComparer.Ordinal)
                .ToList();

            result.InsertRange(group.Key + 1, entriesToInsert);
        }

        return result;
    }

    private static void UpdateLabelIndex(List<string> labels, int index, Dictionary<string, int> labelLastIndex)
    {
        if (labels == null) return;
        foreach (var label in labels)
            labelLastIndex[NormalizeLabel(label)] = index;
    }

    private static int FindBestInsertionIndex(List<string> labels, Dictionary<string, int> labelLastIndex)
    {
        int bestIndex = -1;
        if (labels == null) return bestIndex;

        foreach (var label in labels)
        {
            if (labelLastIndex.TryGetValue(NormalizeLabel(label), out int idx) && idx > bestIndex)
                bestIndex = idx;
        }
        return bestIndex;
    }
}
