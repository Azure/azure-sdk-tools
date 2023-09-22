using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Azure.Sdk.Tools.CodeOwnersParser
{
    public static class CodeownersFile
    {

        public static List<CodeownersEntry> GetCodeownersEntriesFromFileOrUrl(
            string codeownersFilePathOrUrl,
            string? teamStorageURI = null)
        {
            string content = FileHelpers.GetFileOrUrlContents(codeownersFilePathOrUrl);
            return GetCodeownersEntries(content, teamStorageURI);
        }

        public static List<CodeownersEntry> GetCodeownersEntries(string codeownersContent, string? teamStorageURI = null)
        {
            TeamUserHolder teamUserHolder = new TeamUserHolder(teamStorageURI);
            List<CodeownersEntry> entries = new List<CodeownersEntry>();
            
            // We are going to read line by line until we find a line that is not a comment
            // OR that is using the placeholder entry inside the comment.
            // while we are trying to find the folder entry, we parse all comment lines
            // to extract the labels from it. when we find the path or placeholder,
            // we add the completed entry and create a new one.
            CodeownersEntry entry = new CodeownersEntry();
            using StringReader sr = new StringReader(codeownersContent);
            while (sr.ReadLine() is { } line)
            {
                entry = ProcessCodeownersLine(line, entry, entries, teamUserHolder);
            }

            return entries;
        }

        public static CodeownersEntry GetMatchingCodeownersEntry(
            string targetPath,
            string codeownersFilePathOrUrl,
            string? teamStorageURI = null)
        {
            var codeownersEntries = GetCodeownersEntriesFromFileOrUrl(codeownersFilePathOrUrl, teamStorageURI);
            return GetMatchingCodeownersEntry(targetPath, codeownersEntries);
        }

        public static Dictionary<string, CodeownersEntry> GetMatchingCodeownersEntries(
            GlobFilePath targetPath,
            string targetDir,
            string codeownersFilePathOrUrl,
            string[]? ignoredPathPrefixes = null,
            string? teamStorageURI = null)
        {
            ignoredPathPrefixes ??= Array.Empty<string>();

            var codeownersEntries = GetCodeownersEntriesFromFileOrUrl(codeownersFilePathOrUrl, teamStorageURI);

            Dictionary<string, CodeownersEntry> codeownersEntriesByPath = targetPath
                .ResolveGlob(targetDir, ignoredPathPrefixes)
                .ToDictionary(
                    path => path,
                    path => GetMatchingCodeownersEntry(
                        path,
                        codeownersEntries));

            return codeownersEntriesByPath;
        }

        public static CodeownersEntry GetMatchingCodeownersEntry(
            string targetPath,
            List<CodeownersEntry> codeownersEntries)
        {
            Debug.Assert(targetPath != null);
            return new MatchedCodeownersEntry(targetPath, codeownersEntries).Value;
        }

        private static CodeownersEntry ProcessCodeownersLine(
            string line,
            CodeownersEntry entry,
            List<CodeownersEntry> entries,
            TeamUserHolder teamUserHolder)
        {
            line = NormalizeLine(line);

            if (string.IsNullOrWhiteSpace(line))
            {
                return entry;
            }

            if (!IsCommentLine(line) || (IsCommentLine(line) && IsPlaceholderEntry(line)))
            {
                entry.ParseOwnersAndPath(line, teamUserHolder);

                if (entry.IsValid)
                    entries.Add(entry);

                // An entry has ended, as we got to a path: real bath or placeholder path.
                return new CodeownersEntry();
            }

            if (IsCommentLine(line))
            {
                // try to process the line in case there are markers that need to be extracted
                entry.ProcessLabelsOnLine(line);
                return entry;
            }

            throw new InvalidOperationException(
                $"This case shouldn't be possible. line: '{line}'");
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
    }
}
