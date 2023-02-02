using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Azure.Sdk.Tools.CodeOwnersParser
{
    public static class CodeownersFile
    {
        private const bool UseRegexMatcherDefault = false;

        public static List<CodeownersEntry> GetCodeownersEntriesFromFileOrUrl(
            string codeownersFilePathOrUrl)
        {
            string content = FileHelpers.GetFileOrUrlContents(codeownersFilePathOrUrl);
            return GetCodeownersEntries(content);
        }

        public static List<CodeownersEntry> GetCodeownersEntries(string codeownersContent)
        {
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
                entry = ProcessCodeownersLine(line, entry, entries);
            }

            return entries;
        }

        public static CodeownersEntry GetMatchingCodeownersEntry(
            string targetPath,
            string codeownersFilePathOrUrl,
            bool useRegexMatcher = UseRegexMatcherDefault)
        {
            var codeownersEntries = GetCodeownersEntriesFromFileOrUrl(codeownersFilePathOrUrl);
            return GetMatchingCodeownersEntry(targetPath, codeownersEntries, useRegexMatcher);
        }

        public static Dictionary<string, CodeownersEntry> GetMatchingCodeownersEntries(
            GlobFilePath targetPath,
            string targetDir,
            string codeownersFilePathOrUrl,
            string[]? ignoredPathPrefixes = null,
            bool useRegexMatcher = UseRegexMatcherDefault)
        {
            ignoredPathPrefixes ??= Array.Empty<string>();

            var codeownersEntries = GetCodeownersEntriesFromFileOrUrl(codeownersFilePathOrUrl);

            Dictionary<string, CodeownersEntry> codeownersEntriesByPath = targetPath
                .ResolveGlob(targetDir, ignoredPathPrefixes).ToDictionary(
                    path => path,
                    path => GetMatchingCodeownersEntry(
                        path,
                        codeownersEntries,
                        useRegexMatcher));

            return codeownersEntriesByPath;
        }

        public static CodeownersEntry GetMatchingCodeownersEntry(
            string targetPath,
            List<CodeownersEntry> codeownersEntries,
            bool useRegexMatcher = UseRegexMatcherDefault)
        {
            Debug.Assert(targetPath != null);
            return useRegexMatcher
                ? new MatchedCodeownersEntry(targetPath, codeownersEntries).Value
                : GetMatchingCodeownersEntryLegacyImpl(targetPath, codeownersEntries);
        }

        private static CodeownersEntry ProcessCodeownersLine(
            string line,
            CodeownersEntry entry,
            List<CodeownersEntry> entries)
        {
            line = NormalizeLine(line);

            if (string.IsNullOrWhiteSpace(line))
            {
                return entry;
            }

            if (!IsCommentLine(line) || (IsCommentLine(line) && IsPlaceholderEntry(line)))
            {
                entry.ParseOwnersAndPath(line);

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

        private static CodeownersEntry GetMatchingCodeownersEntryLegacyImpl(
            string targetPath,
            List<CodeownersEntry> codeownersEntries)
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
