using OutputColorizer;
using System;
using System.Collections.Generic;
using System.IO;

namespace Azure.Sdk.Tools.CodeOwnersParser
{
    public static class CodeOwnersFile
    {
        public static List<CodeOwnerEntry> ParseFile(string filePathOrUrl)
        {
            string content;
            content = FileHelpers.GetFileContents(filePathOrUrl);

            return ParseContent(content);
        }

        public static List<CodeOwnerEntry> ParseContent(string fileContent)
        {
            List<CodeOwnerEntry> entries = new List<CodeOwnerEntry>();
            string line;


            // An entry ends when we get to a path (a real path or a commented dummy path)

            using (StringReader sr = new StringReader(fileContent))
            {
                CodeOwnerEntry entry = new CodeOwnerEntry();

                // we are going to read line by line until we find a line that is not a comment OR that is using the placeholder entry inside the comment.
                // while we are trying to find the folder entry, we parse all comment lines to extract the labels from it.
                // when we find the path or placeholder, we add the completed entry and create a new one.
                while ((line = sr.ReadLine()) != null)
                {
                    line = NormalizeLine(line);

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (!line.StartsWith("#") || line.IndexOf(CodeOwnerEntry.MissingFolder, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // If this is not a comment line OR this is a placeholder entry

                        entry.ParseOwnersAndPath(line);

                        // only add it if it is a valid entry
                        if (entry.IsValid)
                        {
                            entries.Add(entry);
                        }

                        // create a new entry.
                        entry = new CodeOwnerEntry();
                    }
                    else if (line.StartsWith("#"))
                    {
                        // try to process the line in case there are markers that need to be extracted
                        entry.ProcessLabelsOnLine(line);
                    }

                }
            }
            return entries;
        }

        public static CodeOwnerEntry ParseAndFindOwnersForClosestMatch(string codeOwnersFilePathOrUrl, string targetPath)
        {
            var codeOwnerEntries = ParseFile(codeOwnersFilePathOrUrl);
            // Normalize the start and end of the paths by trimming slash
            targetPath = targetPath.Trim('/');

            // We want to find the match closest to the bottom of the codeowners file.
            // CODEOWNERS sorts the paths in order of 'RepoPath', 'ServicePath' and then 'PackagePath'.
            for (int i = codeOwnerEntries.Count - 1; i >= 0; i--)
            {
                string codeOwnerPath = codeOwnerEntries[i].PathExpression.Trim('/');
                // Note that this only matches on paths without glob patterns which is good enough
                // for our current scenarios but in the future might need to support globs
                if (targetPath.StartsWith(codeOwnerPath, StringComparison.OrdinalIgnoreCase))
                {
                    return codeOwnerEntries[i];
                }
            }

            return null;
        }

        private static string NormalizeLine(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return line;
            }

            // Remove tabs and trim extra whitespace
            return line.Replace('\t', ' ').Trim();
        }
    }
}
