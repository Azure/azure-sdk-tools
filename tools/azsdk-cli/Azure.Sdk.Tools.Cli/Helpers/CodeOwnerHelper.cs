using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface ICodeOwnerHelper
    {
        List<CodeownersEntry> FindMatchingEntries(IList<CodeownersEntry> entries, string serviceName = null, string repoPath = null);
        public string NormalizePath(string path);
        CodeownersEntry findAlphabeticalInsertionPoint(List<CodeownersEntry> codeownersEntries, CodeownersEntry codeownersEntry);
        public (List<CodeownersEntry>, int) mergeCodeownerEntries(List<CodeownersEntry> codeownersEntries, int index);
        string addCodeownersEntryAtIndex(string codeownersContent, CodeownersEntry codeownersEntry, int index, bool codeownersEntryExists);
        string formatCodeownersEntry(CodeownersEntry codeownersEntry);
        (int StartLine, int EndLine) findBlock(string currentContent, string serviceCategory);
        public string ExtractDirectoryName(string path);
        string CreateBranchName(string prefix, string identifier);
        
        // New owner manipulation helper methods
        List<string> AddUniqueOwners(List<string> existingOwners, List<string> ownersToAdd);
        List<string> RemoveOwners(List<string> existingOwners, List<string> ownersToRemove);
        List<string> ReplaceEntryInLines(string lines, CodeownersEntry targetEntry);
    }

    public class CodeOwnerHelper : ICodeOwnerHelper
    {
        public List<CodeownersEntry> FindMatchingEntries(IList<CodeownersEntry> entries, string serviceName = null, string repoPath = null)
        {
            var codeownersEntries = new List<CodeownersEntry>();
            foreach (var entry in entries)
            {
                // If serviceName is provided, match by ServiceLabels or PRLabels (exact match, case and space insensitive)
                if (!string.IsNullOrEmpty(serviceName))
                {
                    // Check ServiceLabels for exact match
                    if (entry.ServiceLabels?.Any(label => NormalizeLabel(label).Equals(NormalizeLabel(serviceName), StringComparison.OrdinalIgnoreCase)) == true)
                    {
                        codeownersEntries.Add(entry);
                    }
                    // Check PRLabels for exact match
                    else if (entry.PRLabels?.Any(label => NormalizeLabel(label).Equals(NormalizeLabel(serviceName), StringComparison.OrdinalIgnoreCase)) == true)
                    {
                        codeownersEntries.Add(entry);
                    }
                }

                // If repoPath is provided, match by PathExpression (contains match for paths - this is intentional)
                if (!string.IsNullOrEmpty(repoPath) && entry.PathExpression?.Contains(repoPath, StringComparison.OrdinalIgnoreCase) == true)
                {
                    codeownersEntries.Add(entry);
                }
            }

            return codeownersEntries.Cast<CodeownersEntry>().ToList();
        }

        private string NormalizeLabel(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }
            return input.Replace(" ", "").Replace("%", "").Trim();
        }

        public string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            path = path.Trim('/');
            if (!path.StartsWith("sdk/", StringComparison.OrdinalIgnoreCase))
            {
                path = "sdk/" + path;
            }
            path = $"/{path}/";
            return path;
        }

        public CodeownersEntry findAlphabeticalInsertionPoint(List<CodeownersEntry> codeownersEntries, CodeownersEntry codeownersEntry)
        {
            var comparer = new CodeownersEntryPathComparer();

            // Find the first entry that should come after our new entry
            for (int i = 0; i < codeownersEntries.Count; i++)
            {
                int comparison = comparer.Compare(codeownersEntries[i], codeownersEntry);

                // If the current entry should come after our new entry, insert before it
                if (comparison > 0)
                {
                    int insertionLine = codeownersEntries[i].startLine;
                    codeownersEntry.startLine = insertionLine;
                    codeownersEntry.endLine = insertionLine; // Set endLine same as startLine for new entries
                    return codeownersEntry;
                }

                (codeownersEntries, i) = mergeCodeownerEntries(codeownersEntries, i);
            }

            // If we didn't find an insertion point, append at the end
            if (codeownersEntries.Count > 0)
            {
                var lastEntry = codeownersEntries[codeownersEntries.Count - 1];
                int insertionLine = lastEntry.endLine + 1;
                codeownersEntry.startLine = insertionLine;
                codeownersEntry.endLine = insertionLine; // Set endLine same as startLine for new entries
                return codeownersEntry;
            }

            // If the list is empty, start at line 1
            codeownersEntry.startLine = 1;
            codeownersEntry.endLine = 1; // Set endLine same as startLine for new entries
            return codeownersEntry;
        }

        public (List<CodeownersEntry>, int) mergeCodeownerEntries(List<CodeownersEntry> codeownersEntries, int index)
        {
            if (index < codeownersEntries.Count - 1)
            {
                var codeownersEntry = codeownersEntries[index];
                var nextCodeownersEntry = codeownersEntries[index + 1];

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

        public string addCodeownersEntryAtIndex(string codeownersContent, CodeownersEntry codeownersEntry, int index, bool codeownersEntryExists)
        {
            var lines = codeownersContent.Split('\n').ToList();

            if (codeownersEntryExists)
            {
                return string.Join('\n', ReplaceEntryInLines(codeownersContent, codeownersEntry));
            }

            if (index >= 0 && index <= lines.Count)
            {
                var formattedEntry = formatCodeownersEntry(codeownersEntry);
                
                // If inserting between entries (not at the end), add spacing after the new entry
                bool isInsertingBetweenEntries = index < lines.Count && 
                                               index > 0 && 
                                               !string.IsNullOrWhiteSpace(lines[index].Trim());
                
                lines.Insert(index, formattedEntry);
                
                // Add blank line after the new entry if inserting between entries
                if (isInsertingBetweenEntries)
                {
                    //lines.Insert(index + 1, "");
                }
            }
            else
            {
                lines.Add(formatCodeownersEntry(codeownersEntry));
            }

            return string.Join('\n', lines);
        }

        public string formatCodeownersEntry(CodeownersEntry codeownersEntry)
        {
            var lines = new List<string>();

            bool addSeperationLine = false;

            string path = codeownersEntry.PathExpression ?? string.Empty;
            string serviceLabel = codeownersEntry.ServiceLabels.FirstOrDefault() ?? string.Empty;
            List<string> serviceOwners = codeownersEntry.ServiceOwners ?? new List<string>();
            List<string> sourceOwners = codeownersEntry.SourceOwners ?? new List<string>();
            List<string> azureSDKOwners = codeownersEntry.AzureSdkOwners ?? new List<string>();
            
            // Add all PRLabels first
            if (codeownersEntry.PRLabels != null && codeownersEntry.PRLabels.Any())
            {
                foreach (var prLabel in codeownersEntry.PRLabels.Where(label => !string.IsNullOrWhiteSpace(label)))
                {
                    lines.Add($"# PRLabel: %{prLabel}");
                }
                addSeperationLine = true;
            }
            
            // Add PRLabel if serviceLabel is provided (for backward compatibility)
            else if (!string.IsNullOrEmpty(serviceLabel))
            {
                addSeperationLine = true;
                lines.Add($"# PRLabel: %{serviceLabel}");
            }

            // Add the path and source owners line
            if (!string.IsNullOrEmpty(path) && sourceOwners != null && sourceOwners.Count > 0)
            {
                if (!addSeperationLine)
                {
                    lines.Add("");  // Add empty string, not "\n"
                }
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

            // Add AzureSDKOwners if provided
            if (azureSDKOwners != null && azureSDKOwners.Count > 0)
            {
                var azureSDKOwnersString = string.Join(" ", azureSDKOwners.Select(owner => owner.StartsWith("@") ? owner : $"@{owner}"));
                lines.Add($"# AzureSdkOwners: {azureSDKOwnersString}");
            }

            // Add ServiceOwners if provided
            if (serviceOwners != null && serviceOwners.Count > 0)
            {
                var serviceOwnersString = string.Join(" ", serviceOwners.Select(owner => owner.StartsWith("@") ? owner : $"@{owner}"));
                lines.Add($"# ServiceOwners: {serviceOwnersString}");
            }
            
            // Add a blank line at the end to separate entries
            lines.Add("");
            
            var formattedCodeownersEntry = string.Join("\n", lines);
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

        private bool AreEntriesRelated(CodeownersEntry entry1, CodeownersEntry entry2)
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

        public string ExtractDirectoryName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

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
            input = input
                .Replace(" - ", "-")
                .Replace(" ", "-")
                .Replace("/", "-")
                .Replace("_", "-")
                .Replace(".", "-")
                .Trim('-')
                .ToLowerInvariant();
            return Regex.Replace(input, @"[^a-zA-Z0-9\-]", "");
        }

        public List<string> AddUniqueOwners(List<string> existingOwners, List<string> ownersToAdd)
        {
            foreach (var owner in ownersToAdd)
            {
                var normalizedOwner = owner.Trim('@');
                var ownerWithAt = $"@{normalizedOwner}";
                if (!existingOwners.Contains(ownerWithAt))
                {
                    existingOwners.Add(ownerWithAt);
                }
            }
            return existingOwners;
        }

        public List<string> RemoveOwners(List<string> existingOwners, List<string> ownersToRemove)
        {
            foreach (var ownerToRemove in ownersToRemove)
            {
                var normalizedOwner = ownerToRemove.Trim('@');
                var ownerWithAt = $"@{normalizedOwner}";
                if (existingOwners.Contains(ownerWithAt))
                {
                    existingOwners.Remove(ownerWithAt);
                }
            }
            return existingOwners;
        }

        public List<string> ReplaceEntryInLines(string lines, CodeownersEntry targetEntry)
        {
            var modifiedLines = lines.Split('\n').ToList();

            // Generate the new formatted entry
            var formattedCodeownersEntry = formatCodeownersEntry(targetEntry);

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

            return modifiedLines;
        }
    }
}
