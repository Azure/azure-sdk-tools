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

        private static string teamUserBlobUri = $"https://{StorageConstants.AzureBlobAccountName}.blob.core.windows.net/{StorageConstants.AzureSdkWriteTeamsContainer}/{StorageConstants.AzureSdkWriteTeamsBlobName}";
        private static Dictionary<string, List<string>>? teamUserDict = null;
        public static List<CodeownersEntry> GetCodeownersEntriesFromFileOrUrl(
            string codeownersFilePathOrUrl)
        {
            string content = FileHelpers.GetFileOrUrlContents(codeownersFilePathOrUrl);
            return GetCodeownersEntries(content);
        }

        public static Dictionary<string, List<string>>?GetTeamUserData()
        {
            if (null == teamUserDict)
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                string rawJson = FileHelpers.GetFileOrUrlContents(teamUserBlobUri);
                stopWatch.Stop();
                // Get the elapsed time as a TimeSpan value.
                TimeSpan ts = stopWatch.Elapsed;

                // Format and display the TimeSpan value.
                string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                    ts.Hours, ts.Minutes, ts.Seconds,
                    ts.Milliseconds / 10);
                Console.WriteLine($"Time to pull teamUserBlob: {elapsedTime}");
                var list = JsonSerializer.Deserialize<List<KeyValuePair<string, List<string>>>>(rawJson);
                if (null != list)
                {
                    teamUserDict = list.ToDictionary((keyItem) => keyItem.Key, (valueItem) => valueItem.Value);
                }
            }
            return teamUserDict;
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
            string codeownersFilePathOrUrl)
        {
            var codeownersEntries = GetCodeownersEntriesFromFileOrUrl(codeownersFilePathOrUrl);
            return GetMatchingCodeownersEntry(targetPath, codeownersEntries);
        }

        public static Dictionary<string, CodeownersEntry> GetMatchingCodeownersEntries(
            GlobFilePath targetPath,
            string targetDir,
            string codeownersFilePathOrUrl,
            string[]? ignoredPathPrefixes = null)
        {
            ignoredPathPrefixes ??= Array.Empty<string>();

            var codeownersEntries = GetCodeownersEntriesFromFileOrUrl(codeownersFilePathOrUrl);

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
    }
}
