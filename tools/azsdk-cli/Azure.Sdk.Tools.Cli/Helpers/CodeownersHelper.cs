using System.Text.RegularExpressions;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface ICodeownersHelper
    {
        public (string, CodeownersEntry) AddCodeownersEntry(
            string codeownersContent,
            bool isMgmtPlane,
            string normalizedPath,
            string serviceLabel,
            List<string> serviceOwners,
            List<string> sourceOwners,
            bool isAdding);
        CodeownersEntry UpdateCodeownersEntry(
            CodeownersEntry? existingEntry,
            List<string> serviceOwners,
            List<string> sourceOwners,
            bool isAdding);
        CodeownersEntry CreateCodeownersEntry(
            string path,
            string serviceLabel,
            List<string> serviceOwners,
            List<string> sourceOwners,
            bool isMgmtPlane);
        string AddCodeownersEntryToFile(List<CodeownersEntry> codeownersEntries, string codeownersContent, CodeownersEntry codeownersEntry, bool codeownersEntryExists);
        CodeownersEntry FindAlphabeticalInsertionPoint(List<CodeownersEntry> codeownersEntries, CodeownersEntry codeownersEntry);
        string ReplaceEntryInLines(string lines, CodeownersEntry targetEntry);
        string FormatCodeownersEntry(CodeownersEntry codeownersEntry);
        (int StartLine, int EndLine) FindBlock(string currentContent, string serviceCategory);
        List<string> AddOwners(List<string> existingOwners, List<string> ownersToAdd);
        List<string> RemoveOwners(List<string> existingOwners, List<string> ownersToRemove);
    }

    public class CodeownersHelper() : ICodeownersHelper
    {
        private static readonly string standardServiceCategory = "# Client Libraries";
        private static readonly string standardManagementCategory = "# Management Libraries";
        private const string azureWriteTeamsBlobUrl = "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob";
        
        public (string, CodeownersEntry) AddCodeownersEntry(
            string codeownersContent,
            bool isMgmtPlane,
            string normalizedPath,
            string serviceLabel,
            List<string> serviceOwners,
            List<string> sourceOwners,
            bool isAdding)
        {
            // Find Codeowner Entry with the validated Label or Path            
            var (startLine, endLine) = isMgmtPlane
                ? FindBlock(codeownersContent, standardManagementCategory)
                : FindBlock(codeownersContent, standardServiceCategory);

            var codeownersContentList = codeownersContent.Split("\n").ToList();

            var codeownersEntries = CodeownersParser.ParseCodeownersEntries(codeownersContentList, azureWriteTeamsBlobUrl, startLine, endLine);


            // Find existing codeowners entry by path or service label
            var matchingEntry = FindMatchingEntries(codeownersEntries, normalizedPath, serviceLabel);

            var codeownersEntryExisted = false;
            var updatedEntry = new CodeownersEntry();
            if (matchingEntry != null)
            {
                updatedEntry = UpdateCodeownersEntry(
                    matchingEntry, serviceOwners, sourceOwners, isAdding);
                codeownersEntryExisted = true;
            }
            else
            {
                updatedEntry = CreateCodeownersEntry(
                    normalizedPath, serviceLabel, serviceOwners, sourceOwners, isMgmtPlane
                );
            }

            // Modify the file
            var modifiedCodeownersContent = AddCodeownersEntryToFile(codeownersEntries, codeownersContent, updatedEntry, codeownersEntryExisted);

            return (modifiedCodeownersContent, updatedEntry);
        }

        // High-level lookup and update methods first (call helpers placed below)
        public static CodeownersEntry? FindMatchingEntries(IList<CodeownersEntry> entries, string path = null, string serviceLabel = null)
        {
            var mergedCodeownersEntries = new List<CodeownersEntry>(entries);
            for (int i = 0; i < mergedCodeownersEntries.Count; i++)
            {
                mergedCodeownersEntries = MergeCodeownerEntries(mergedCodeownersEntries, i);
            }

            foreach (var entry in entries)
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

        public CodeownersEntry UpdateCodeownersEntry(
            CodeownersEntry? existingEntry,
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

        public CodeownersEntry CreateCodeownersEntry(
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
                ServiceOwners = serviceOwners ?? new List<string>(),
                SourceOwners = sourceOwners ?? new List<string>(),
                AzureSdkOwners = new List<string>()
            };
            return newEntry;
        }

        public string AddCodeownersEntryToFile(List<CodeownersEntry> codeownersEntries, string codeownersContent, CodeownersEntry codeownersEntry, bool codeownersEntryExists)
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
            }

            var index = FindAlphabeticalInsertionPoint(codeownersEntries, codeownersEntry).startLine;

            var formattedEntry = FormatCodeownersEntry(codeownersEntry);

            if (index >= 0 && index <= lines.Count)
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
            else
            {
                // If adding at the end, ensure proper spacing
                if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                {
                    lines.Add("");
                }
                lines.Add(formattedEntry);
            }

            return string.Join('\n', lines);
        }

        public CodeownersEntry FindAlphabeticalInsertionPoint(List<CodeownersEntry> codeownersEntries, CodeownersEntry codeownersEntry)
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
                if (serviceLabel2.Equals(dirName, StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals(serviceLabel2.Replace("%", "").Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(path2) && !string.IsNullOrEmpty(serviceLabel1))
            {
                var dirName = ExtractDirectoryName(path2);
                if (serviceLabel1.Equals(dirName, StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals(serviceLabel1.Replace("%", "").Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // 2: Compare service label to PR label
            if (!string.IsNullOrEmpty(serviceLabel1) && !string.IsNullOrEmpty(PRLabel2))
            {
                if (string.Equals(serviceLabel1.Replace("%", "").Trim(), PRLabel2.Replace("%", "").Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(PRLabel1) && !string.IsNullOrEmpty(serviceLabel2))
            {
                if (string.Equals(PRLabel1.Replace("%", "").Trim(), serviceLabel2.Replace("%", "").Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // 3: Compare by service label
            if (!string.IsNullOrEmpty(serviceLabel1) && !string.IsNullOrEmpty(serviceLabel2))
            {
                if (string.Equals(serviceLabel1.Replace("%", "").Trim(), serviceLabel2.Replace("%", "").Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public string ReplaceEntryInLines(string lines, CodeownersEntry targetEntry)
        {
            var modifiedLines = lines.Split('\n').ToList();

            // Generate the new formatted entry
            var formattedCodeownersEntry = FormatCodeownersEntry(targetEntry);

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

        public string FormatCodeownersEntry(CodeownersEntry codeownersEntry)
        {
            var lines = new List<string>();

            bool addSeperationLine = false;

            string path = codeownersEntry.PathExpression ?? string.Empty;
            List<string> serviceLabels = codeownersEntry.ServiceLabels ?? new List<string>();
            List<string> prLabels = codeownersEntry.PRLabels ?? new List<string>();
            List<string> serviceOwners = codeownersEntry.ServiceOwners ?? new List<string>();
            List<string> sourceOwners = codeownersEntry.SourceOwners ?? new List<string>();
            List<string> azureSDKOwners = codeownersEntry.AzureSdkOwners ?? new List<string>();

            // Helper to compute pad width: start at basePad and if the left content length exceeds it,
            // round up to the next multiple of 5.
            int basePad = 67;
            int ComputePad(int leftLength)
            {
                int candidate = ((leftLength + 5) / 5) * 5; // next multiple of 5 >= leftLength+? small margin
                return Math.Max(basePad, candidate);
            }

            // Add all PRLabels first (each on its own line) - derived from service labels
            if (prLabels.Any())
            {
                // ensure label is prefixed without duplicate %
                var formattedPRLabels = prLabels
                    .Where(lbl => !string.IsNullOrWhiteSpace(lbl))
                    .Select(lbl => lbl.StartsWith("%") ? lbl : $"%{lbl}");
                lines.Add($"# PRLabel: {string.Join(" ", formattedPRLabels)}");
                addSeperationLine = true;
            }

            // Add the path and source owners line
            if (!string.IsNullOrEmpty(path) && sourceOwners != null && sourceOwners.Count > 0)
            {
                addSeperationLine = true;
                // Normalize and deduplicate source owners while preserving original casing
                var normalizedSourceOwners = sourceOwners
                    .Where(o => !string.IsNullOrWhiteSpace(o))
                    .Select(o => o.Trim())
                    .Select(o => o.StartsWith("@") ? o.Substring(1) : o)
                    .Select(o => o.Trim())
                    .ToList();

                var uniqueSourceOwners = new List<string>();
                var seenSource = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var owner in normalizedSourceOwners)
                {
                    if (seenSource.Add(owner))
                    {
                        uniqueSourceOwners.Add("@" + owner);
                    }
                }

                // Compute pad width based on the path length (start at basePad and increase in steps of 5)
                int padWidth = ComputePad(path.Length);
                var pathLine = $"{path}".PadRight(padWidth) + $"{string.Join(" ", uniqueSourceOwners)}";
                lines.Add(pathLine);
            }

            if (addSeperationLine)
            {
                lines.Add("");
            }

            // Add ServiceLabel(s) on a single line if provided
            if (serviceLabels.Any())
            {
                var formattedServiceLabels = serviceLabels
                    .Where(lbl => !string.IsNullOrWhiteSpace(lbl))
                    .Select(lbl => lbl.StartsWith("%") ? lbl : $"%{lbl}");
                lines.Add($"# ServiceLabel: {string.Join(" ", formattedServiceLabels)}");
            }

            // Add AzureSDKOwners if provided (normalize/dedupe)
            if (azureSDKOwners != null && azureSDKOwners.Count > 0)
            {
                var normalizedAzureSdkOwners = azureSDKOwners
                    .Where(o => !string.IsNullOrWhiteSpace(o))
                    .Select(o => o.Trim())
                    .Select(o => o.StartsWith("@") ? o.Substring(1) : o)
                    .Select(o => o.Trim())
                    .ToList();

                var uniqueAzureSdkOwnersList = new List<string>();
                var seenAzure = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var owner in normalizedAzureSdkOwners)
                {
                    if (seenAzure.Add(owner))
                    {
                        uniqueAzureSdkOwnersList.Add("@" + owner);
                    }
                }

                // Use computed pad width so owners align even when the path is long
                int padForAzure = ComputePad(Math.Max(path.Length, "# AzureSdkOwners: ".Length));
                var azureSDKOwnersLine = $"# AzureSdkOwners: ".PadRight(padForAzure) + $"{string.Join(" ", uniqueAzureSdkOwnersList)}";
                lines.Add(azureSDKOwnersLine);
            }

            // Add ServiceOwners if provided (normalize/dedupe)
            if (serviceOwners != null && serviceOwners.Count > 0)
            {
                var normalizedServiceOwners = serviceOwners
                    .Where(o => !string.IsNullOrWhiteSpace(o))
                    .Select(o => o.Trim())
                    .Select(o => o.StartsWith("@") ? o.Substring(1) : o)
                    .Select(o => o.Trim())
                    .ToList();

                var uniqueServiceOwners = new List<string>();
                var seenService = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var owner in normalizedServiceOwners)
                {
                    if (seenService.Add(owner))
                    {
                        uniqueServiceOwners.Add("@" + owner);
                    }
                }

                int padForService = ComputePad(Math.Max(path.Length, "# ServiceOwners: ".Length));
                var serviceOwnersLine = $"# ServiceOwners: ".PadRight(padForService) + $"{string.Join(" ", uniqueServiceOwners)}";
                lines.Add(serviceOwnersLine);
            }

            var formattedCodeownersEntry = string.Join("\n", lines);
            return formattedCodeownersEntry;
        }

        public (int StartLine, int EndLine) FindBlock(string currentContent, string serviceCategory)
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

            // Remove any leading or trailing whitespace and slashes using regex
            var normalizedPath = Regex.Replace(path, "^[\\s/]+|[\\s/]+$", "");

            if (!normalizedPath.StartsWith("sdk/", StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = "sdk/" + normalizedPath;
            }
            normalizedPath = $"/{normalizedPath}/";
            return normalizedPath;
        }

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
            return Regex.Replace(normalizedInput, @"[^a-zA-Z0-9\-]", "");
        }

        public List<string> AddOwners(List<string> existingOwners, List<string> ownersToAdd)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Helper to normalize an owner (strip whitespace and leading '@')
            static string Normalize(string owner) => Regex.Replace(owner ?? string.Empty, "^[\\s@]+|[\\s@]+$", "");

            if (existingOwners != null)
            {
                foreach (var existingOwner in existingOwners)
                {
                    var normalized = Normalize(existingOwner);
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
                    var normalized = Normalize(owner);
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

        public List<string> RemoveOwners(List<string> existingOwners, List<string> ownersToRemove)
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
                    var normalized = Regex.Replace(owner ?? string.Empty, "^[\\s@]+|[\\s@]+$", "");
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
                var normalizedExisting = Regex.Replace(existingOwner ?? string.Empty, "^[\\s@]+|[\\s@]+$", "");
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
