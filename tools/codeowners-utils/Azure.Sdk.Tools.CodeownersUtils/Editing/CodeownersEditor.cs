using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.CodeownersUtils.Editing
{
    public class CodeownersEditor
    {
        private string codeownersContent;
        private bool isMgmtPlane;
        private const string AzureWriteTeamsBlobUrl = "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob";
        private static readonly string standardServiceCategory = "# Client Libraries";
        private static readonly string standardManagementCategory = "# Management Libraries";

        public CodeownersEditor(string codeownersContent, bool isMgmtPlane = false)
        {
            this.codeownersContent = codeownersContent ?? throw new ArgumentNullException(nameof(codeownersContent));
            this.isMgmtPlane = isMgmtPlane;
        }

        /// <summary>
        /// Adds a new codeowners entry or updates an existing one, based on path or service label.
        /// Updates the related codeowners content string to include the modified codeowners entry.
        /// Handles all normalization and validation internally.
        /// </summary>
        /// <param name="path">The path for the codeowners entry (optional if serviceLabel is provided).</param>
        /// <param name="serviceLabel">The service label for the codeowners entry (optional if path is provided).</param>
        /// <param name="serviceOwners">List of service owners to add/update.</param>
        /// <param name="sourceOwners">List of source owners to add/update.</param>
        /// <returns>The added or updated CodeownersEntry.</returns>
        public CodeownersEntry AddOrUpdateCodeownersFile(string path = "", string serviceLabel = "", List<string> serviceOwners = null, List<string> sourceOwners = null)
        {
            // Normalize path
            string normalizedPath = path;
            if (!string.IsNullOrWhiteSpace(path))
            {
                normalizedPath = NormalizePath(path);
            }

            var matchingEntry = FindMatchingEntry(normalizedPath, serviceLabel);

            CodeownersEntry updatedEntry;
            if (matchingEntry != null)
            {
                updatedEntry = UpdateCodeownersEntry(matchingEntry, serviceOwners, sourceOwners, true);
                codeownersContent = AddCodeownersEntryToFile(updatedEntry, true);
            }
            else
            {
                updatedEntry = CreateCodeownersEntry(normalizedPath, serviceLabel, serviceOwners, sourceOwners, isMgmtPlane);
                codeownersContent = AddCodeownersEntryToFile(updatedEntry, false);
            }
            return updatedEntry;
        }

        /// <summary>
        /// Updates the CODEOWNERS file and removes owners from an existing codeowners entry, based on path or service label.
        /// Updates the related codeowners content string to include the modified codeowners entry.
        /// Handles all normalization internally.
        /// </summary>
        /// <param name="path">The path for the codeowners entry (optional if serviceLabel is provided).</param>
        /// <param name="serviceLabel">The service label for the codeowners entry (optional if path is provided).</param>
        /// <param name="serviceOwnersToRemove">List of service owners to remove.</param>
        /// <param name="sourceOwnersToRemove">List of source owners to remove.</param>
        /// <returns>The updated CodeownersEntry after removal.</returns>
        public CodeownersEntry RemoveOwnersFromCodeownersFile(string path = "", string serviceLabel = "", List<string> serviceOwnersToRemove = null, List<string> sourceOwnersToRemove = null)
        {
            string normalizedPath = path;
            if (!string.IsNullOrWhiteSpace(path))
            {
                normalizedPath = NormalizePath(path);
            }

            var matchingEntry = FindMatchingEntry(normalizedPath, serviceLabel);

            if (matchingEntry == null)
            {
                throw new InvalidOperationException("No matching entry found to remove owners from.");
            }
            var updatedEntry = UpdateCodeownersEntry(matchingEntry, serviceOwnersToRemove, sourceOwnersToRemove, false);
            codeownersContent = AddCodeownersEntryToFile(updatedEntry, true);
            return updatedEntry;
        }

        public override string ToString()
        {
            return codeownersContent;
        }

        public CodeownersEntry FindMatchingEntry(string path = null, string serviceLabel = null)
        {
            var codeownersContentList = codeownersContent.Split('\n').ToList();

            var codeownersEntries = CodeownersParser.ParseCodeownersEntries(codeownersContentList, AzureWriteTeamsBlobUrl);
            
            var mergedCodeownersEntries = MergeAllCodeownersEntries();

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

        public string AddCodeownersEntryToFile(CodeownersEntry codeownersEntry, bool codeownersEntryExists)
        {
            var codeownersContentList = codeownersContent.Split('\n').ToList();

            var (startLine, endLine) = isMgmtPlane
                ? FindBlock(standardManagementCategory)
                : FindBlock(standardServiceCategory);

            var codeownersEntries = CodeownersParser.ParseCodeownersEntries(codeownersContentList, AzureWriteTeamsBlobUrl, startLine, endLine);
            
            if (codeownersEntryExists)
            {
                // Validate that the entry has valid line information for replacement
                if (codeownersEntry.startLine >= 0 && codeownersEntry.endLine >= 0 &&
                    codeownersEntry.startLine < codeownersContentList.Count && codeownersEntry.endLine < codeownersContentList.Count &&
                    codeownersEntry.startLine <= codeownersEntry.endLine)
                {
                    return ReplaceEntryInLines(codeownersEntry);
                }
                else
                {
                    throw new InvalidOperationException($"Invalid replacement point: startLine={codeownersEntry.startLine}, endLine={codeownersEntry.endLine}, lines.Count={codeownersContentList.Count}");
                }
            }

            var index = FindAlphabeticalInsertionPoint(codeownersEntry).startLine;
            var formattedEntry = codeownersEntry.FormatCodeownersEntry();

            // If the index is not valid, or the entries list is empty, always append at the end
            if (index < 0 || index > codeownersContentList.Count || codeownersEntries.Count == 0)
            {
                // Ensure proper spacing before appending
                if (codeownersContentList.Count > 0 && !string.IsNullOrWhiteSpace(codeownersContentList[codeownersContentList.Count - 1]))
                {
                    codeownersContentList.Add("");
                }
                codeownersContentList.Add(formattedEntry);
            }
            else
            {
                // Ensure we have proper spacing before inserting
                if (index > 0 && index < codeownersContentList.Count && !string.IsNullOrWhiteSpace(codeownersContentList[index - 1]))
                {
                    // Insert a blank line before the new entry if the previous line isn't blank
                    codeownersContentList.Insert(index, "");
                    index++; // Adjust index since we inserted a blank line
                }

                codeownersContentList.Insert(index, formattedEntry);

                // Ensure we have proper spacing after inserting
                if (index + 1 < codeownersContentList.Count && !string.IsNullOrWhiteSpace(codeownersContentList[index + 1]))
                {
                    codeownersContentList.Insert(index + 1, "");
                }
            }

            return string.Join('\n', codeownersContentList);
        }

        public CodeownersEntry FindAlphabeticalInsertionPoint(CodeownersEntry codeownersEntry)
        {
            var codeownersContentList = codeownersContent.Split('\n').ToList();

            var (startLine, endLine) = isMgmtPlane
                ? FindBlock(standardManagementCategory)
                : FindBlock(standardServiceCategory);

            var codeownersEntries = CodeownersParser.ParseCodeownersEntries(codeownersContentList, AzureWriteTeamsBlobUrl, startLine, endLine);
            
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

        public List<CodeownersEntry> MergeAllCodeownersEntries()
        {
            var codeownersContentList = codeownersContent.Split('\n').ToList();

            var codeownersEntries = CodeownersParser.ParseCodeownersEntries(codeownersContentList, AzureWriteTeamsBlobUrl);
            
            for (int i = 0; i < codeownersEntries.Count; i++)
            {
                codeownersEntries = MergeCodeownerEntries(codeownersEntries, i);
            }

            return codeownersEntries;
        }

        public List<CodeownersEntry> MergeCodeownerEntries(List<CodeownersEntry> codeownersEntries, int index)
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

        public (int StartLine, int EndLine) FindBlock(string serviceCategory)
        {
            var codeownersContentList = codeownersContent.Split('\n').ToList();

            int startLine = -1;
            int endLine = -1;
            for (int i = 0; i < codeownersContentList.Count; i++)
            {
                if (codeownersContentList[i].Trim().Equals(serviceCategory))
                {
                    if (codeownersContentList[i + 1].Trim().EndsWith("#"))
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
                else if (startLine != -1 && codeownersContentList[i].Trim().EndsWith("#"))
                {
                    endLine = i;
                    return (startLine, endLine);
                }
            }

            // If we found the service category but no next section, return from category to end
            if (startLine != -1)
            {
                return (startLine, codeownersContentList.Count - 1);
            }

            return (0, codeownersContentList.Count - 1);
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
                if (NormalizeLabel(serviceLabel2).Equals(dirName, StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals(NormalizeLabel(serviceLabel2), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(path2) && !string.IsNullOrEmpty(serviceLabel1))
            {
                var dirName = ExtractDirectoryName(path2);
                if (NormalizeLabel(serviceLabel1).Equals(dirName, StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals(NormalizeLabel(serviceLabel1), StringComparison.OrdinalIgnoreCase))
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

        private string ReplaceEntryInLines(CodeownersEntry targetEntry)
        {
            var modifiedLines = codeownersContent.Split('\n').ToList();

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

        private static (string serviceLabel, string PRLabel) GetPrimaryLabel(CodeownersEntry entry)
        {
            var serviceLabel = entry.ServiceLabels?.FirstOrDefault(label => !string.IsNullOrWhiteSpace(label)) ?? string.Empty;
            var PRLabel = entry.PRLabels?.FirstOrDefault(label => !string.IsNullOrWhiteSpace(label)) ?? string.Empty;
            return (serviceLabel, PRLabel);
        }

        private static string ExtractDirectoryName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            var parts = path.Trim('/').Split('/');

            return parts.LastOrDefault(part => !string.IsNullOrWhiteSpace(part)) ?? string.Empty;
        }

        private static string NormalizePath(string path)
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
    }
}
