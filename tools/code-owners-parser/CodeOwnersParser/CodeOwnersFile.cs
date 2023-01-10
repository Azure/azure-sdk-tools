using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Azure.Sdk.Tools.CodeOwnersParser
{
    public static class CodeownersFile
    {
        public static List<CodeownersEntry> GetCodeownersEntriesFromFileOrUrl(string codeownersFilePathOrUrl)
        {
            string content = FileHelpers.GetFileOrUrlContents(codeownersFilePathOrUrl);
            return GetCodeownersEntries(content);
        }

        public static List<CodeownersEntry> GetCodeownersEntries(string codeownersContent)
        {
            List<CodeownersEntry> entries = new List<CodeownersEntry>();

            // An entry ends when we get to a path (a real path or a commented dummy path)
            using (StringReader sr = new StringReader(codeownersContent))
            {
                CodeownersEntry entry = new CodeownersEntry();

                // we are going to read line by line until we find a line that is not a comment OR that is using the placeholder entry inside the comment.
                // while we are trying to find the folder entry, we parse all comment lines to extract the labels from it.
                // when we find the path or placeholder, we add the completed entry and create a new one.
                while (sr.ReadLine() is { } line)
                {
                    ProcessCodeownersLine(line, entry, entries);
                }
            }
            return entries;
        }

        public static CodeownersEntry GetMatchingCodeownersEntry(
            string targetPath,
            string codeownersFilePathOrUrl,
            bool useNewImpl = false)
        {
            var codeownersEntries = GetCodeownersEntriesFromFileOrUrl(codeownersFilePathOrUrl);
            return GetMatchingCodeownersEntry(targetPath, codeownersEntries, useNewImpl);
        }

        public static CodeownersEntry GetMatchingCodeownersEntry(
            string targetPath,
            List<CodeownersEntry> codeownersEntries,
            bool useNewImpl = false)
        {
            Debug.Assert(targetPath != null);
            return useNewImpl
                ? new MatchedCodeownersEntry(codeownersEntries, targetPath).Value
                : GetMatchingCodeownersEntryLegacyImpl(codeownersEntries, targetPath);
        }

        private static void ProcessCodeownersLine(string line, CodeownersEntry entry, List<CodeownersEntry> entries)
        {
            line = NormalizeLine(line);

            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            if (!IsCommentLine(line) || IsPlaceholderEntry(line))
            {
                entry.ParseOwnersAndPath(line);

                // only add it if it is a valid entry
                if (entry.IsValid)
                    entries.Add(entry);
            }
            else if (line.StartsWith("#"))
            {
                // try to process the line in case there are markers that need to be extracted
                entry.ProcessLabelsOnLine(line);
            }
        }

        private static bool IsPlaceholderEntry(string line)
            => line.Contains(CodeownersEntry.MissingFolder, StringComparison.OrdinalIgnoreCase);

        private static bool IsCommentLine(string line)
            => line.StartsWith("#");

        private static string NormalizeLine(string line)
            => !string.IsNullOrEmpty(line)
                // Remove tabs and trim extra whitespace
                ? line.Replace('\t', ' ').Trim()
                : line;

        private static CodeownersEntry GetMatchingCodeownersEntryLegacyImpl(
            List<CodeownersEntry> codeownersEntries,
            string targetPath)
        {
            // Normalize the start and end of the paths by trimming slash
            targetPath = targetPath.Trim('/');

            // We want to find the match closest to the bottom of the codeowners file.
            // CODEOWNERS sorts the paths in order of 'RepoPath', 'ServicePath' and then 'PackagePath'.
            for (int i = codeownersEntries.Count - 1; i >= 0; i--)
            {
                string codeownersPath = codeownersEntries[i].PathExpression.Trim('/');
                // Note that this only matches on paths without glob patterns which is good enough
                // for our current scenarios but in the future might need to support globs
                if (targetPath.StartsWith(codeownersPath, StringComparison.OrdinalIgnoreCase))
                {
                    return codeownersEntries[i];
                }
            }

            return new CodeownersEntry();
        }
    }
}
