using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Azure.Sdk.Tools.CodeOwnersParser
{
    /// <summary>
    /// Represents a CODEOWNERS file entry that matched to targetPath from
    /// the list of entries, assumed to have been parsed from CODEOWNERS file.
    ///
    /// To obtain the value of the matched entry, reference "Value" member.
    /// </summary>
    internal class MatchedCodeOwnerEntry
    {
        public readonly CodeOwnerEntry Value;

        private static readonly char[] unsupportedChars = { '[', ']', '!', '?' };

        public MatchedCodeOwnerEntry(List<CodeOwnerEntry> entries, string targetPath)
        {
            this.Value = FindOwnersForClosestMatch(entries, targetPath);
        }

        /// <summary>
        /// Returns a CodeOwnerEntry from codeOwnerEntries that matches targetPath
        /// per algorithm described in:
        /// https://git-scm.com/docs/gitignore#_pattern_format
        /// and
        /// https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners#codeowners-syntax
        ///
        /// If there is no match, returns "new CodeOwnerEntry()".
        /// </summary>
        private static CodeOwnerEntry FindOwnersForClosestMatch(
            List<CodeOwnerEntry> codeownersEntries,
            string targetPath)
        {
            // targetPath is assumed to be absolute w.r.t. repository root, hence we ensure
            // it starts with "/" to denote that.
            if (!targetPath.StartsWith("/"))
                targetPath = "/" + targetPath;

            // We do not trim or add the slash ("/") at the end of the targetPath because its
            // presence influences the matching algorithm:
            // Slash at the end denotes the target path is a directory, not a file, so it might
            // match against a CODEOWNERS entry that matches only directories and not files.

            // Entries below take precedence, hence we read the file from the bottom up.
            // By convention, entries in CODEOWNERS should be sorted top-down in the order of:
            // - 'RepoPath',
            // - 'ServicePath'
            // - and then 'PackagePath'.
            // However, due to lack of validation, as of 12/29/2022 this is not always the case.
            for (int i = codeownersEntries.Count - 1; i >= 0; i--)
            {
                string codeownersPath = codeownersEntries[i].PathExpression;
                if (ContainsUnsupportedCharacters(codeownersPath))
                {
                    continue;
                }

                List<string> globPatterns = ConvertToGlobPatterns(codeownersPath);
                PatternMatchingResult patternMatchingResult = MatchGlobPatterns(targetPath, globPatterns);
                if (patternMatchingResult.HasMatches)
                {
                    return codeownersEntries[i];
                }
            }
            // assert: none of the codeownersEntries matched targetPath
            return new CodeOwnerEntry();
        }

        private static bool ContainsUnsupportedCharacters(string codeownersPath)
            => unsupportedChars.Any(codeownersPath.Contains);

        /// <summary>
        /// Converts codeownersPath to a set of glob patterns to include in
        /// glob matching. The conversion is a translation from codeowners and .gitignore
        /// spec into glob. That is, it reduces the spec to glob rules,
        /// which then can be checked against using glob matcher.
        /// </summary>
        /// <returns>
        /// Usually 1 glob pattern to include in matching. In one special case
        /// returns 2 patterns, which happens when the path needs to be interpreted
        /// both as-is file, or as a directory prefix.
        /// </returns>
        private static List<string> ConvertToGlobPatterns(string codeownersPath)
        {
            codeownersPath = ConvertPrefix(codeownersPath);
            var patternsToInclude = PatternsToInclude(codeownersPath);
            return patternsToInclude;
        }

        private static string ConvertPrefix(string codeownersPath)
        {
            // Codeowners entry path starting with "/*" is equivalent to it starting with "*".
            // Note this also covers cases when it starts with "/**".
            if (codeownersPath.StartsWith("/*"))
                codeownersPath = codeownersPath.Substring("/".Length);

            // If the codeownersPath doesn't have any slash at the beginning or in the middle,
            // then it means its start is relative to any directory in the repository,
            // hence we prepend "**/" to reflect this as a glob pattern.
            if (!codeownersPath.TrimEnd('/').Contains("/"))
            {
                codeownersPath = "**/" + codeownersPath;
            }
            // If, on the other hand, codeownersPath has to start at the root, we ensure
            // it starts with slash to reflect that.
            else 
            {
                if (!codeownersPath.StartsWith("/"))
                {
                    codeownersPath = "/" + codeownersPath;
                }
                else
                {
                    // codeownersPath already starts with "/", so nothing to prepend.
                }
            }

            return codeownersPath;
        }

        private static List<string> PatternsToInclude(string codeownersPath)
        {
            List<string> patternsToInclude = new List<string>();

            if (codeownersPath.EndsWith("/"))
            {
                patternsToInclude.Add(ConvertDirectorySuffix(codeownersPath));
            }
            else
            {
                patternsToInclude.Add(ConvertDirectorySuffix(codeownersPath + "/"));
                patternsToInclude.Add(codeownersPath);
            }

            return patternsToInclude;
        }

        private static string ConvertDirectorySuffix(string codeownersPath)
        {
            // If the codeownersPath doesn't already end with "*",
            // we need to append "**", to denote that codeownersPath has to match
            // a prefix of the targetPath, not the entire path.
            if (!codeownersPath.TrimEnd('/').EndsWith("*"))
            {
                codeownersPath += "**";
            }
            else
            {
                // codeownersPath directory already has stars in the suffix, so nothing to do.
                // Example paths:
                // apps/*/
                // apps/**/
            }

            return codeownersPath;
        }

        private static PatternMatchingResult MatchGlobPatterns(
            string targetPath,
            List<string> patterns)
        {
            // Note we use StringComparison.Ordinal, not StringComparison.OrdinalIgnoreCase,
            // as CODEOWNERS paths are case-sensitive.
            var globMatcher = new Matcher(StringComparison.Ordinal);

            foreach (var pattern in patterns)
            {
                globMatcher.AddInclude(pattern);
            }
            
            var dir = new InMemoryDirectoryInfo(
                // This 'rootDir: "/"' is used here only because the globMatcher API requires it.
                rootDir: "/", 
                files: new List<string> { targetPath });

            var patternMatchingResult = globMatcher.Execute(dir);
            return patternMatchingResult;
        }
    }
}
