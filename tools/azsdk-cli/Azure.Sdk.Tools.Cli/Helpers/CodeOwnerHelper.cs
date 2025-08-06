using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface ICodeOwnerHelper
    {
        List<CodeownersEntry?> FindMatchingEntries(IList<CodeownersEntry> entries, string serviceName = null, string repoPath = null);
        List<string> ExtractUniqueOwners(CodeownersEntry entry);
        int findAlphabeticalInsertionPoint(List<CodeownersEntry> codeownersEntries, string path = null, string serviceLabel = null);
        public (List<CodeownersEntry>, int) mergeCodeownerEntries(List<CodeownersEntry> codeownersEntries, int index);
        string addCodeownersEntryAtIndex(string codeownersContent, string codeownersEntry, int index);
        string formatCodeownersEntry(string path, string serviceLabel, List<string> serviceOwners, List<string> sourceOwners);
        (int StartLine, int EndLine) findBlock(string currentContent, string serviceCategory);
        string CreateBranchName(string prefix, string identifier);
        
        // New owner manipulation helper methods
        string NormalizeUsername(string username);
        bool ContainsOwner(List<string> existingOwners, string newOwner);
        void AddUniqueOwners(List<string> targetList, List<CodeOwnerValidationResult> ownersToAdd);
        void RemoveOwners(List<string> targetList, List<string> ownersToRemove);
        List<string> ReplaceEntryInLines(List<string> lines, CodeownersEntry targetEntry);
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
                int comparison = comparer.Compare(codeownersEntries[i], newEntry);

                // If the current entry should come after our new entry, insert before it
                if (comparison > 0)
                {
                    return codeownersEntries[i].startLine;
                }

                (codeownersEntries, i) = mergeCodeownerEntries(codeownersEntries, i);
            }

            // If we didn't find an insertion point, append at the end
            if (codeownersEntries.Count > 0)
            {
                var lastEntry = codeownersEntries[codeownersEntries.Count - 1];
                return lastEntry.endLine + 1;
            }

            return 1;
        }

        public (List<CodeownersEntry>, int) mergeCodeownerEntries(List<CodeownersEntry> codeownersEntries, int index)
        {
            if (index < codeownersEntries.Count - 1)
            {
                var codeownersEntry = codeownersEntries[index];
                var nextCodeownersEntry = codeownersEntries[index + 1];

                if (AreEntriesRelatedByPath(codeownersEntry, nextCodeownersEntry))
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

                    codeownersEntries[index] = mergedEntry;
                    codeownersEntries.RemoveAt(index + 1);

                    if (index - 1 < 0)
                    {
                        index = 0;
                    }
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

            bool addSeperationLine = false;

            // Add PRLabel if serviceLabel is provided
            if (!string.IsNullOrEmpty(serviceLabel))
            {
                addSeperationLine = true;
                lines.Add($"# PRLabel: %{serviceLabel}");
            }

            // Add the path and source owners line
            if (!string.IsNullOrEmpty(path) && sourceOwners != null && sourceOwners.Count > 0)
            {
                addSeperationLine = true;
                var formattedSourceOwners = sourceOwners.Select(owner => owner.StartsWith("@") ? owner : $"@{owner}");
                var pathLine = $"{path}".PadRight(67) + $"{string.Join(" ", formattedSourceOwners)}";
                lines.Add(pathLine);
            }

            if (addSeperationLine)
            {
                lines.Add("");
            }

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

            // 1: Compare paths to service labels
            if (!string.IsNullOrEmpty(path1) && !string.IsNullOrEmpty(serviceLabel2))
            {
                var dirName = ExtractDirectoryName(path1);
                if (serviceLabel2.Contains(dirName, StringComparison.OrdinalIgnoreCase) ||
                    dirName.Contains(serviceLabel2.Replace("%", "").Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(path2) && !string.IsNullOrEmpty(serviceLabel1))
            {
                var dirName = ExtractDirectoryName(path2);
                if (serviceLabel1.Contains(dirName, StringComparison.OrdinalIgnoreCase) ||
                    dirName.Contains(serviceLabel1.Replace("%", "").Trim(), StringComparison.OrdinalIgnoreCase))
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
            return $"{prefix}-{normalizedIdentifier}";
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

        // New owner manipulation helper methods
        public string NormalizeUsername(string username)
        {
            return username.TrimStart('@');
        }

        public bool ContainsOwner(List<string> existingOwners, string newOwner)
        {
            var normalizedNewOwner = NormalizeUsername(newOwner);
            return existingOwners.Any(existing => NormalizeUsername(existing).Equals(normalizedNewOwner, StringComparison.OrdinalIgnoreCase));
        }

        public void AddUniqueOwners(List<string> targetList, List<CodeOwnerValidationResult> ownersToAdd)
        {
            foreach (var owner in ownersToAdd)
            {
                if (!ContainsOwner(targetList, owner.Username))
                {
                    targetList.Add(owner.Username);
                }
            }
        }

        public void RemoveOwners(List<string> targetList, List<string> ownersToRemove)
        {
            var normalizedOwnersToRemove = ownersToRemove.Select(NormalizeUsername).ToList();
            targetList.RemoveAll(owner => normalizedOwnersToRemove.Contains(NormalizeUsername(owner)));
        }

        public List<string> ReplaceEntryInLines(List<string> lines, CodeownersEntry targetEntry)
        {
            var modifiedLines = new List<string>(lines);

            // Generate the new formatted entry
            var formattedCodeownersEntry = formatCodeownersEntry(
                targetEntry.PathExpression,
                targetEntry.ServiceLabels[0],
                targetEntry.ServiceOwners,
                targetEntry.SourceOwners);

            // Remove the old entry lines
            int originalEntryLineCount = targetEntry.endLine - targetEntry.startLine + 1;
            modifiedLines.RemoveRange(targetEntry.startLine, originalEntryLineCount);

            // Insert the new formatted entry at the same position
            var entryLines = formattedCodeownersEntry.Split('\n');
            modifiedLines.InsertRange(targetEntry.startLine, entryLines);

            return modifiedLines;
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

        public override string ToString()
        {
            var result = new List<string>
            {
                $"Username: {Username}",
                $"IsValid: {IsValidCodeOwner}",
                $"HasWritePermission: {HasWritePermission}",
                $"Status: {Status}",
                $"Message: {Message ?? "None"}"
            };

            if (Organizations?.Any() == true)
            {
                result.Add($"Organizations:");
                foreach (var org in Organizations)
                {
                    result.Add($"  - {org.Key}: {org.Value}");
                }
            }
            
            return string.Join("\n", result);
        }
    }
}
