using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface ICodeOwnerHelper
    {
        List<CodeownersEntry?> FindMatchingEntries(IList<CodeownersEntry> entries, string serviceName = null, string repoPath = null);
        List<string> ExtractUniqueOwners(CodeownersEntry entry);
        int findAlphabeticalInsertionPoint(List<CodeownersEntry> codeownersEntries, string path = null, string serviceLabel = null);
        string addCodeownersEntryAtIndex(string codeownersContent, string codeownersEntry, int index);
        List<CodeownersEntry> mergeSimilarCodeownersEntries(List<CodeownersEntry> codeownersEntries);
        string formatCodeownersEntry(string path, string serviceLabel, List<string> serviceOwners, List<string> sourceOwners);
        (int StartLine, int EndLine) findBlock(string currentContent, string serviceCategory);
        string CreateBranchName(string prefix, string identifier);
    }

    public class CodeOwnerHelper : ICodeOwnerHelper
    {
        public List<CodeownersEntry?> FindMatchingEntries(IList<CodeownersEntry> entries, string serviceName = null, string repoPath = null)
        {
            var codeownersEntries = new List<CodeownersEntry>();
            foreach (var entry in entries)
            {
                // Check if service matches by ServiceLabels
                if (!string.IsNullOrEmpty(serviceName) && entry.ServiceLabels?.Any(label => label.Contains(serviceName, StringComparison.OrdinalIgnoreCase)) == true)
                    codeownersEntries.Add(entry);

                // Check if service matches by PRLabels  
                if (!string.IsNullOrEmpty(serviceName) && entry.PRLabels?.Any(label => label.Contains(serviceName, StringComparison.OrdinalIgnoreCase)) == true)
                    codeownersEntries.Add(entry);

                // Check if service matches by PathExpression
                if (!string.IsNullOrEmpty(serviceName) && entry.PathExpression?.Contains(serviceName, StringComparison.OrdinalIgnoreCase) == true)
                    codeownersEntries.Add(entry);

                if (!string.IsNullOrEmpty(repoPath) && entry.PathExpression?.Contains(repoPath, StringComparison.OrdinalIgnoreCase) == true)
                    codeownersEntries.Add(entry);

                // Check if service matches by any owner team names
                if (!string.IsNullOrEmpty(serviceName))
                {
                    var allOwners = ExtractUniqueOwners(entry);
                    if (allOwners.Any(owner => owner.Contains(serviceName, StringComparison.OrdinalIgnoreCase)))
                        codeownersEntries.Add(entry);
                }
            }

            return codeownersEntries.Cast<CodeownersEntry?>().ToList();
        }

        public List<string> ExtractUniqueOwners(CodeownersEntry entry)
        {
            var allOwners = new List<string>();
            if (entry.SourceOwners?.Any() == true) allOwners.AddRange(entry.SourceOwners);
            if (entry.ServiceOwners?.Any() == true) allOwners.AddRange(entry.ServiceOwners);
            if (entry.AzureSdkOwners?.Any() == true) allOwners.AddRange(entry.AzureSdkOwners);

            return allOwners.Where(o => !string.IsNullOrEmpty(o)).Distinct().ToList();
        }

        public int findAlphabeticalInsertionPoint(List<CodeownersEntry> codeownersEntries, string path = null, string serviceLabel = null)
        {
            var newEntry = new CodeownersEntry
            {
                PathExpression = path ?? string.Empty,
                ServiceLabels = !string.IsNullOrEmpty(serviceLabel) ? new List<string> { serviceLabel } : new List<string>()
            };

            var comparer = new CodeownersEntryPathComparer();

            // Find the first entry that should come after our new entry
            for (int i = 0; i < codeownersEntries.Count; i++)
            {
                var codeownersEntry = codeownersEntries[i];
                int comparison = comparer.Compare(codeownersEntry, newEntry);

                // If the current entry should come after our new entry, insert before it
                if (comparison > 0)
                {
                    return codeownersEntry.startLine;
                }
            }

            // If we didn't find an insertion point, append at the end
            if (codeownersEntries.Count > 0)
            {
                var lastEntry = codeownersEntries[codeownersEntries.Count - 1];
                return lastEntry.endLine + 1;
            }

            return 1;
        }

        public string addCodeownersEntryAtIndex(string codeownersContent, string codeownersEntry, int index)
        {
            var lines = codeownersContent.Split('\n').ToList();

            // Insert the new entry at the specified index
            if (index >= 0 && index <= lines.Count)
            {
                lines.Insert(index, codeownersEntry);
            }
            else
            {
                // If index is out of bounds, append at the end
                lines.Add(codeownersEntry);
            }

            return string.Join('\n', lines);
        }

        public List<CodeownersEntry> mergeSimilarCodeownersEntries(List<CodeownersEntry> codeownersEntries)
        {
            for (int i = 0; i < codeownersEntries.Count - 1; i++)
            {
                var codeownersEntry = codeownersEntries[i];
                var nextCodeownersEntry = codeownersEntries[i + 1];

                if (AreEntriesRelatedByPath(codeownersEntry, nextCodeownersEntry))
                {
                    var newEntry = new CodeownersEntry
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
                    codeownersEntries[i] = newEntry;
                    codeownersEntries.RemoveAt(i + 1);
                    i--;
                }
            }
            return codeownersEntries;
        }

        public string formatCodeownersEntry(
            string path,
            string serviceLabel,
            List<string> serviceOwners,
            List<string> sourceOwners)
        {
            var lines = new List<string>();

            // Add PRLabel if serviceLabel is provided
            if (!string.IsNullOrEmpty(serviceLabel))
            {
                lines.Add($"# PRLabel: %{serviceLabel}");
            }

            // Add the path and source owners line
            if (!string.IsNullOrEmpty(path) && sourceOwners != null && sourceOwners.Count > 0)
            {
                var ownersString = string.Join(" ", sourceOwners.Select(owner => owner.StartsWith("@") ? owner : $"@{owner}"));
                lines.Add($"{path.PadRight(26)} {ownersString}");
            }

            // Add empty line for separation
            lines.Add("");

            // Add ServiceLabel if provided
            if (!string.IsNullOrEmpty(serviceLabel))
            {
                lines.Add($"# ServiceLabel: %{serviceLabel}");
            }

            // Add ServiceOwners if provided
            if (serviceOwners != null && serviceOwners.Count > 0)
            {
                var serviceOwnersString = string.Join(" ", serviceOwners.Select(owner => owner.StartsWith("@") ? owner : $"@{owner}"));
                lines.Add($"# ServiceOwners: {serviceOwnersString}");
            }
            var formattedCodeownersEntry = string.Join("\n", lines);
            formattedCodeownersEntry += "\n";
            return formattedCodeownersEntry;
        }

        public (int StartLine, int EndLine) findBlock(string currentContent, string serviceCategory)
        {
            var lines = currentContent.Split('\n');

            int startLine = -1;
            int endLine = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim().Equals(serviceCategory))
                {
                    startLine = i;
                    i++;
                }
                else if (startLine != -1 && lines[i].Trim().Contains("###"))
                {
                    endLine = i;
                    return (startLine, endLine);
                }
            }
            return (0, lines.Length - 1);
        }

        private bool AreEntriesRelatedByPath(CodeownersEntry entry1, CodeownersEntry entry2)
        {
            var path1 = entry1.PathExpression?.Trim();
            var path2 = entry2.PathExpression?.Trim();
            var serviceLabel1 = GetPrimaryServiceLabel(entry1);
            var serviceLabel2 = GetPrimaryServiceLabel(entry2);

            if (!string.IsNullOrEmpty(path1) && !string.IsNullOrEmpty(path2))
            {
                var dir1 = ExtractDirectoryName(path1);
                var dir2 = ExtractDirectoryName(path2);

                return string.Equals(dir1, dir2, StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrEmpty(path1) && !string.IsNullOrEmpty(serviceLabel2))
            {
                var dirName = ExtractDirectoryName(path1);
                return serviceLabel2.Contains(dirName, StringComparison.OrdinalIgnoreCase) ||
                       dirName.Contains(serviceLabel2.Replace("%", "").Trim(), StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrEmpty(path2) && !string.IsNullOrEmpty(serviceLabel1))
            {
                var dirName = ExtractDirectoryName(path2);
                return serviceLabel1.Contains(dirName, StringComparison.OrdinalIgnoreCase) ||
                       dirName.Contains(serviceLabel1.Replace("%", "").Trim(), StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private string GetPrimaryServiceLabel(CodeownersEntry entry)
        {
            return entry.ServiceLabels?.FirstOrDefault(label => !string.IsNullOrWhiteSpace(label)) ?? string.Empty;
        }

        private string ExtractDirectoryName(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;

            var parts = path.Trim('/').Split('/');

            return parts.LastOrDefault(part => !string.IsNullOrWhiteSpace(part)) ?? string.Empty;
        }

        public string CreateBranchName(string prefix, string identifier)
        {
            var normalizedIdentifier = NormalizeIdentifier(identifier);
            return $"{prefix}-{normalizedIdentifier}-{DateTime.Now:yyyyMMdd-HHmmss}";
        }

        public string NormalizeIdentifier(string input)
        {
            return input
                .Replace(" - ", "-")
                .Replace(" ", "-")
                .Replace("/", "-")
                .Replace("_", "-")
                .Trim('-')
                .ToLowerInvariant();
        }
    }

    // Data models - moved from CodeOwnerTools for better organization
    public class ServiceCodeOwnerResult
    {
        public string Repository { get; set; } = "";
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
        public List<CodeOwnerValidationResult> CodeOwners { get; set; } = new();
    }

    public class CodeOwnerValidationResult
    {
        public string Username { get; set; } = "";
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
        public bool IsValidCodeOwner { get; set; }
        public bool HasWritePermission { get; set; }
        public Dictionary<string, bool> Organizations { get; set; } = new();
    }
}
