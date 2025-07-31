using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface ICodeOwnerHelper
    {
        List<CodeownersEntry?> FindMatchingEntries(IList<CodeownersEntry> entries, string serviceName = null, string repoPath = null);
        List<string> ExtractUniqueOwners(CodeownersEntry entry);
        int findAlphabeticalInsertionPoint(List<CodeownersEntry> codeownersEntries, string path = null, string serviceLabel = null);
        public (List<CodeownersEntry> codeownersEntries, int index) mergeCodeownerEntries(List<CodeownersEntry> codeownersEntries, int index);
        string addCodeownersEntryAtIndex(string codeownersContent, string codeownersEntry, int index);
        string formatCodeownersEntry(string path, string serviceLabel, List<string> serviceOwners, List<string> sourceOwners);
        (int StartLine, int EndLine) findBlock(string currentContent, string serviceCategory);
        string CreateBranchName(string prefix, string identifier);
    }

    public class CodeOwnerHelper : ICodeOwnerHelper
    {
        public static int count = 0;

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

                (codeownersEntries, i) = mergeCodeownerEntries(codeownersEntries, i);

                codeownersEntry = codeownersEntries[i];

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

        public (List<CodeownersEntry> codeownersEntries, int index) mergeCodeownerEntries(List<CodeownersEntry> codeownersEntries, int index)
        {
            if (index < codeownersEntries.Count - 1)
            {
                var codeownersEntry = codeownersEntries[index];
                var nextCodeownersEntry = codeownersEntries[index + 1];

                if (AreEntriesRelatedByPath(codeownersEntry, nextCodeownersEntry))
                {
                    count++;
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

                    codeownersEntries[index] = mergedEntry;
                    codeownersEntries.RemoveAt(index + 1);

                    codeownersEntry = mergedEntry;

                    index--;
                }
            }
            return (codeownersEntries, index);
        }

        public string addCodeownersEntryAtIndex(string codeownersContent, string codeownersEntry, int index)
        {
            var lines = codeownersContent.Split('\n').ToList();

            if (index >= 0 && index <= lines.Count)
            {
                lines.Insert(index, codeownersEntry);
            }
            else
            {
                lines.Add(codeownersEntry);
            }

            return string.Join('\n', lines);
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
                lines.Add($"{path.PadRight(25)} {ownersString}");
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

        private bool AreEntriesRelatedByPath(CodeownersEntry entry1, CodeownersEntry entry2)
        {
            var path1 = entry1.PathExpression?.Trim();
            var path2 = entry2.PathExpression?.Trim();
            var (serviceLabel1, PRLabel1) = GetPrimaryLabel(entry1);
            var (serviceLabel2, PRLabel2) = GetPrimaryLabel(entry2);

            // First priority: Compare by service label
            if (!string.IsNullOrEmpty(serviceLabel1) && !string.IsNullOrEmpty(serviceLabel2))
            {
                return string.Equals(serviceLabel1.Replace("%", "").Trim(), serviceLabel2.Replace("%", "").Trim(), StringComparison.OrdinalIgnoreCase);
            }

            // Second priority: Compare service label to PR label
            if (!string.IsNullOrEmpty(serviceLabel1) && !string.IsNullOrEmpty(PRLabel2))
            {
                return string.Equals(serviceLabel1.Replace("%", "").Trim(), PRLabel2.Replace("%", "").Trim(), StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrEmpty(PRLabel1) && !string.IsNullOrEmpty(serviceLabel2))
            {
                return string.Equals(PRLabel1.Replace("%", "").Trim(), serviceLabel2.Replace("%", "").Trim(), StringComparison.OrdinalIgnoreCase);
            }

            // Third priority: Compare by path
            if (!string.IsNullOrEmpty(path1) && !string.IsNullOrEmpty(path2))
            {
                var dir1 = ExtractDirectoryName(path1);
                var dir2 = ExtractDirectoryName(path2);

                return string.Equals(dir1, dir2, StringComparison.OrdinalIgnoreCase);
            }

            // Fourth priority: Compare path to service label (for cases where one has path, other has service label)
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

        private (string serviceLabel, string PRLabel) GetPrimaryLabel(CodeownersEntry entry)
        {
            var serviceLabel = entry.ServiceLabels?.FirstOrDefault(label => !string.IsNullOrWhiteSpace(label)) ?? string.Empty;
            var PRLabel = entry.PRLabels?.FirstOrDefault(label => !string.IsNullOrWhiteSpace(label)) ?? string.Empty;
            return (serviceLabel, PRLabel);
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
