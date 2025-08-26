using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.CodeownersUtils.Editing
{
    public static class CodeownersHelper
    {
        // High-level lookup and update methods first (call helpers placed below)
        public static CodeownersEntry FindMatchingEntry(IList<CodeownersEntry> entries, string path = null, string serviceLabel = null)
        {
            var mergedCodeownersEntries = new List<CodeownersEntry>(entries);
            for (int i = 0; i < mergedCodeownersEntries.Count; i++)
            {
                mergedCodeownersEntries = MergeCodeownerEntries(mergedCodeownersEntries, i);
            }

            foreach (var entry in mergedCodeownersEntries)
            {
                // If serviceLabel is provided, match by ServiceLabels or PRLabels (exact match, case and space insensitive)
                if (!string.IsNullOrEmpty(serviceLabel))
                {
                    // Check ServiceLabels for exact match
                    if (entry.ServiceLabels?.Any(label => NormalizeLabel(label).Equals(NormalizeLabel(serviceLabel), StringComparison.OrdinalIgnoreCase)) == true)
                    {
                        return entry;
                    }
                    // Check PRLabels for exact match
                    else if (entry.PRLabels?.Any(label => NormalizeLabel(label).Equals(NormalizeLabel(serviceLabel), StringComparison.OrdinalIgnoreCase)) == true)
                    {
                        return entry;
                    }
                }

                // If repoPath is provided, match by PathExpression (contains match for paths - this is intentional)
                if (!string.IsNullOrEmpty(path) && ExtractDirectoryName(entry.PathExpression).Contains(ExtractDirectoryName(path), StringComparison.OrdinalIgnoreCase) == true)
                {
                    return entry;
                }
            }
            return null;
        }

        public static CodeownersEntry UpdateCodeownersEntry(
            CodeownersEntry existingEntry,
            List<string> serviceOwners,
            List<string> sourceOwners,
            bool isAdding)
        {
            // Copy constructor for codeownersentry needed
            var updatedCodeownersEntry = new CodeownersEntry(existingEntry);

            if (existingEntry != null)
            {
                if (isAdding)
                {
                    updatedCodeownersEntry.ServiceOwners = AddOwners(updatedCodeownersEntry.ServiceOwners, serviceOwners);
                    updatedCodeownersEntry.SourceOwners = AddOwners(updatedCodeownersEntry.SourceOwners, sourceOwners);
                }
                else
                {
                    updatedCodeownersEntry.ServiceOwners = RemoveOwners(updatedCodeownersEntry.ServiceOwners, serviceOwners);
                    updatedCodeownersEntry.SourceOwners = RemoveOwners(updatedCodeownersEntry.SourceOwners, sourceOwners);
                }
            }
            return updatedCodeownersEntry;
        }

        public static CodeownersEntry CreateCodeownersEntry(
            string path,
            string serviceLabel,
            List<string> serviceOwners,
            List<string> sourceOwners,
            bool isMgmtPlane)
        {
            // Create new entry
            if (string.IsNullOrEmpty(serviceLabel) || string.IsNullOrEmpty(path))
            {
                throw new Exception($"When creating a new entry, both a Service Label and Path are required. Provided: serviceLabel = '{serviceLabel}', path = '{path}'");
            }

            List<string> serviceLabels = new List<string>() { serviceLabel };

            if (isMgmtPlane)
            {
                serviceLabels.Add("Mgmt");
            }

            var newEntry = new CodeownersEntry()
            {
                PathExpression = path,
                ServiceLabels = serviceLabels,
                PRLabels = new List<string>() { serviceLabel } ?? new List<string>(),
                AzureSdkOwners = new List<string>()
            };

            newEntry.ServiceOwners = AddOwners(newEntry.ServiceOwners, serviceOwners);
            newEntry.SourceOwners = AddOwners(newEntry.SourceOwners, sourceOwners);

            return newEntry;
        }

        public static string AddCodeownersEntryToFile(List<CodeownersEntry> codeownersEntries, string codeownersContent, CodeownersEntry codeownersEntry, bool codeownersEntryExists)
        {
            var lines = codeownersContent.Split('\n').ToList();

            if (codeownersEntryExists)
            {
                // Validate that the entry has valid line information for replacement
                if (codeownersEntry.startLine >= 0 && codeownersEntry.endLine >= 0 &&
                    codeownersEntry.startLine < lines.Count && codeownersEntry.endLine < lines.Count &&
                    codeownersEntry.startLine <= codeownersEntry.endLine)
                {
                    return ReplaceEntryInLines(codeownersContent, codeownersEntry);
                }
                else
                {
                    throw new InvalidOperationException($"Invalid replacement point: startLine={codeownersEntry.startLine}, endLine={codeownersEntry.endLine}, lines.Count={lines.Count}");
                }
            }

            var index = FindAlphabeticalInsertionPoint(codeownersEntries, codeownersEntry).startLine;
            var formattedEntry = codeownersEntry.FormatCodeownersEntry();

            // If the index is not valid, or the entries list is empty, always append at the end
            if (index < 0 || index > lines.Count || codeownersEntries.Count == 0)
            {
                // Ensure proper spacing before appending
                if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                {
                    lines.Add("");
                }
                lines.Add(formattedEntry);
            }
            else
            {
                // Ensure we have proper spacing before inserting
                if (index > 0 && index < lines.Count && !string.IsNullOrWhiteSpace(lines[index - 1]))
                {
                    // Insert a blank line before the new entry if the previous line isn't blank
                    lines.Insert(index, "");
                    index++; // Adjust index since we inserted a blank line
                }

                lines.Insert(index, formattedEntry);

                // Ensure we have proper spacing after inserting
                if (index + 1 < lines.Count && !string.IsNullOrWhiteSpace(lines[index + 1]))
                {
                    lines.Insert(index + 1, "");
                }
            }

            return string.Join('\n', lines);
        }

        public static CodeownersEntry FindAlphabeticalInsertionPoint(List<CodeownersEntry> codeownersEntries, CodeownersEntry codeownersEntry)
        {
            var comparer = new CodeownersEntryPathComparer();
            var codeownersEntriesCopy = new List<CodeownersEntry>(codeownersEntries);
            var updatedCodeownersEntry = new CodeownersEntry(codeownersEntry);

            // Find the first entry that should come after our new entry
            for (int i = 0; i < codeownersEntriesCopy.Count; i++)
            {
                int comparison = comparer.Compare(codeownersEntriesCopy[i], updatedCodeownersEntry);

                // If the current entry should come after our new entry, insert before it
                // Treat equality as "insert before" so entries that compare equal don't fall through and get appended
                if (comparison >= 0)
                {
                    updatedCodeownersEntry.startLine = codeownersEntriesCopy[i].startLine;
                    updatedCodeownersEntry.endLine = codeownersEntriesCopy[i].endLine;
                    return updatedCodeownersEntry;
                }

                codeownersEntriesCopy = MergeCodeownerEntries(codeownersEntriesCopy, i);
            }

            // If we didn't find an insertion point, append at the end
            if (codeownersEntriesCopy.Count > 0)
            {
                var lastEntry = codeownersEntriesCopy[codeownersEntriesCopy.Count - 1];
                // Add 1 to move past the last entry, plus 1 more for spacing between entries
                // Place the new entry after the last entry's end line, leaving one blank line between
                updatedCodeownersEntry.startLine = lastEntry.endLine + 2;
                updatedCodeownersEntry.endLine = updatedCodeownersEntry.endLine + 2;
                return updatedCodeownersEntry;
            }

            // If the list is empty, start at line 1
            updatedCodeownersEntry.startLine = 1;
            updatedCodeownersEntry.endLine = 1;
            return updatedCodeownersEntry;
        }

        public static List<CodeownersEntry> MergeCodeownerEntries(List<CodeownersEntry> codeownersEntries, int index)
        {
            var codeownersEntriesCopy = new List<CodeownersEntry>(codeownersEntries);

            if (index < codeownersEntriesCopy.Count - 1)
            {
                var codeownersEntry = codeownersEntriesCopy[index];
                var nextCodeownersEntry = codeownersEntriesCopy[index + 1];

                if (AreEntriesRelated(codeownersEntry, nextCodeownersEntry))
                {
                    var mergedEntry = new CodeownersEntry
                    {
                        PathExpression = !string.IsNullOrEmpty(codeownersEntry.PathExpression) ? codeownersEntry.PathExpression : nextCodeownersEntry.PathExpression,
                        ServiceLabels = codeownersEntry.ServiceLabels.Union(nextCodeownersEntry.ServiceLabels).ToList(),
                        ServiceOwners = codeownersEntry.ServiceOwners.Union(nextCodeownersEntry.ServiceOwners).ToList(),
                        SourceOwners = codeownersEntry.SourceOwners.Union(nextCodeownersEntry.SourceOwners).ToList(),
                        PRLabels = codeownersEntry.PRLabels.Union(nextCodeownersEntry.PRLabels).ToList(),
                        AzureSdkOwners = codeownersEntry.AzureSdkOwners.Union(nextCodeownersEntry.AzureSdkOwners).ToList(),
                        startLine = Math.Min(codeownersEntry.startLine, nextCodeownersEntry.startLine),
                        endLine = Math.Max(codeownersEntry.endLine, nextCodeownersEntry.endLine)
                    };

                    codeownersEntriesCopy[index] = mergedEntry;
                    codeownersEntriesCopy.RemoveAt(index + 1);

                }
            }
            return codeownersEntriesCopy;
        }

        private static bool AreEntriesRelated(CodeownersEntry entry1, CodeownersEntry entry2)
        {
            var path1 = entry1.PathExpression?.Trim();
            var path2 = entry2.PathExpression?.Trim();
            var (serviceLabel1, PRLabel1) = GetPrimaryLabel(entry1);
            var (serviceLabel2, PRLabel2) = GetPrimaryLabel(entry2);

            if (!string.IsNullOrEmpty(path1) && !string.IsNullOrEmpty(path2))
            {
                if (path1.Equals(path2))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            // 1: Compare paths to service labels
            if (!string.IsNullOrEmpty(path1) && !string.IsNullOrEmpty(serviceLabel2))
            {
                var dirName = ExtractDirectoryName(path1);
                if (NormalizeLabel(serviceLabel2).Equals(NormalizeLabel(dirName), StringComparison.OrdinalIgnoreCase) ||
                    NormalizeLabel(dirName).Equals(NormalizeLabel(serviceLabel2), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(path2) && !string.IsNullOrEmpty(serviceLabel1))
            {
                var dirName = ExtractDirectoryName(path2);
                if (NormalizeLabel(serviceLabel1).Equals(NormalizeLabel(dirName), StringComparison.OrdinalIgnoreCase) ||
                    NormalizeLabel(dirName).Equals(NormalizeLabel(serviceLabel1), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // 2: Compare service label to PR label
            if (!string.IsNullOrEmpty(serviceLabel1) && !string.IsNullOrEmpty(PRLabel2))
            {
                if (NormalizeLabel(serviceLabel1).Equals(NormalizeLabel(PRLabel2), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(PRLabel1) && !string.IsNullOrEmpty(serviceLabel2))
            {
                if (NormalizeLabel(PRLabel1).Equals(NormalizeLabel(serviceLabel2), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static string ReplaceEntryInLines(string lines, CodeownersEntry targetEntry)
        {
            var modifiedLines = lines.Split('\n').ToList();

            // Generate the new formatted entry
            var formattedCodeownersEntry = targetEntry.FormatCodeownersEntry();

            // Remove the old entry lines
            int originalEntryLineCount = targetEntry.endLine - targetEntry.startLine + 1;

            // Ensure we have a valid range
            if (originalEntryLineCount <= 0)
            {
                throw new InvalidOperationException($"Invalid entry line range: startLine={targetEntry.startLine}, endLine={targetEntry.endLine}");
            }

            if (targetEntry.startLine < 0 || targetEntry.startLine >= modifiedLines.Count)
            {
                throw new InvalidOperationException($"Invalid startLine: {targetEntry.startLine}, total lines: {modifiedLines.Count}");
            }

            if (targetEntry.startLine + originalEntryLineCount > modifiedLines.Count)
            {
                originalEntryLineCount = modifiedLines.Count - targetEntry.startLine;
            }

            modifiedLines.RemoveRange(targetEntry.startLine, originalEntryLineCount);

            // Insert the new formatted entry at the same position
            var entryLines = formattedCodeownersEntry.Split('\n');
            modifiedLines.InsertRange(targetEntry.startLine, entryLines);

            return string.Join('\n', modifiedLines);
        }

        public static (int StartLine, int EndLine) FindBlock(string currentContent, string serviceCategory)
        {
            var lines = currentContent.Split('\n');

            int startLine = -1;
            int endLine = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim().Equals(serviceCategory))
                {
                    if (lines[i + 1].Trim().EndsWith("#"))
                    {
                        i++;
                        startLine = i;
                    }
                    else
                    {
                        startLine = i;
                        i++;
                    }
                }
                else if (startLine != -1 && lines[i].Trim().EndsWith("#"))
                {
                    endLine = i;
                    return (startLine, endLine);
                }
            }

            // If we found the service category but no next section, return from category to end
            if (startLine != -1)
            {
                return (startLine, lines.Length - 1);
            }

            return (0, lines.Length - 1);
        }

        private static (string serviceLabel, string PRLabel) GetPrimaryLabel(CodeownersEntry entry)
        {
            var serviceLabel = entry.ServiceLabels?.FirstOrDefault(label => !string.IsNullOrWhiteSpace(label)) ?? string.Empty;
            var PRLabel = entry.PRLabels?.FirstOrDefault(label => !string.IsNullOrWhiteSpace(label)) ?? string.Empty;
            return (serviceLabel, PRLabel);
        }

        public static string ExtractDirectoryName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            var parts = path.Trim('/').Split('/');

            return parts.LastOrDefault(part => !string.IsNullOrWhiteSpace(part)) ?? string.Empty;
        }

        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            // "  /sdk/service/library/  " -> "sdk/service/library"
            var normalizedPath = Regex.Replace(path, "^[\\s/]+|[\\s/]+$", "");

            if (!normalizedPath.StartsWith("sdk/", StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = "sdk/" + normalizedPath;
            }
            normalizedPath = $"/{normalizedPath}/";
            return normalizedPath;
        }

        // NormalizeOwner: strip leading/trailing whitespace and leading '@' characters from a GitHub owner string.
        // Example: "  @deepakmauryams " -> "deepakmauryams"
        private static string NormalizeOwner(string owner) => Regex.Replace(owner ?? string.Empty, "^[\\s@]+|[\\s@]+$", "");

        private static string NormalizeLabel(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }
            return input.Replace(" ", "").Replace("%", "").Trim();
        }

        public static string CreateBranchName(string prefix, string identifier)
        {
            var normalizedIdentifier = NormalizeIdentifier(identifier);
            return $"{prefix}-{normalizedIdentifier}";
        }

        public static string NormalizeIdentifier(string input)
        {
            var normalizedInput = input
                .Replace(" - ", "-")
                .Replace(" ", "-")
                .Replace("/", "-")
                .Replace("_", "-")
                .Replace(".", "-")
                .Trim('-')
                .ToLowerInvariant();

            // Remove all characters except letters, digits and hyphen.
            // Example: "My Service/Name_v1.0" -> "my-service-name-v10"
            return Regex.Replace(normalizedInput, @"[^a-zA-Z0-9\-]", "");
        }

        public static List<string> AddOwners(List<string> existingOwners, List<string> ownersToAdd)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (existingOwners != null)
            {
                foreach (var existingOwner in existingOwners)
                {
                    var normalized = NormalizeOwner(existingOwner);
                    if (string.IsNullOrEmpty(normalized))
                    {
                        continue;
                    }

                    if (seen.Add(normalized))
                    {
                        result.Add("@" + normalized);
                    }
                }
            }

            if (ownersToAdd != null)
            {
                foreach (var owner in ownersToAdd)
                {
                    var normalized = NormalizeOwner(owner);
                    if (string.IsNullOrEmpty(normalized))
                    {
                        continue;
                    }

                    if (seen.Add(normalized))
                    {
                        result.Add("@" + normalized);
                    }
                }
            }

            return result;
        }

        public static List<string> RemoveOwners(List<string> existingOwners, List<string> ownersToRemove)
        {
            // Always return a new list; do not mutate inputs
            if (existingOwners == null || existingOwners.Count == 0)
            {
                return new List<string>();
            }

            // Build normalized remove set (no leading '@')
            var removeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (ownersToRemove != null)
            {
                foreach (var owner in ownersToRemove)
                {
                    var normalized = NormalizeOwner(owner);
                    if (!string.IsNullOrEmpty(normalized))
                    {
                        removeSet.Add(normalized);
                    }
                }
            }

            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var existingOwner in existingOwners)
            {
                var normalizedExisting = NormalizeOwner(existingOwner);
                if (string.IsNullOrEmpty(normalizedExisting))
                {
                    continue;
                }

                if (removeSet.Contains(normalizedExisting))
                {
                    continue;
                }

                if (seen.Add(normalizedExisting))
                {
                    result.Add("@" + normalizedExisting);
                }
            }

            return result;
        }
    }
}
