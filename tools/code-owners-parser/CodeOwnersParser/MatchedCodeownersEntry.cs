using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.CodeOwnersParser
{
    /// <summary>
    /// Represents a CODEOWNERS file entry that matched to targetPath from
    /// the list of entries, assumed to have been parsed from CODEOWNERS file.
    ///
    /// This is a new matcher, compared to the old one, located in:
    ///
    ///   CodeownersFile.GetMatchingCodeownersEntryLegacyImpl()
    /// 
    /// This new matcher supports matching against wildcards, while the old one doesn't.
    /// This new matcher is designed to work with CODEOWNERS file validation:
    /// https://github.com/Azure/azure-sdk-tools/issues/4859
    ///
    /// To use this class, construct it.
    /// 
    /// To obtain the value of the matched entry, reference "Value" member.
    ///
    /// Reference:
    /// https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners#codeowners-syntax
    /// https://git-scm.com/docs/gitignore#_pattern_format
    /// </summary>
    internal class MatchedCodeownersEntry
    {
        /// <summary>
        /// Token for temporarily substituting "**" in regex, to avoid it being escaped when
        /// Regex.Escape is called.
        /// </summary>
        private const string DoubleStar = "_DOUBLE_STAR_";
        /// <summary>
        /// Token for temporarily substituting "*" in regex, to avoid it being escaped when
        /// Regex.Escape is called.
        /// </summary>
        private const string SingleStar = "_SINGLE_STAR_";

        public readonly CodeownersEntry Value;

        /// <summary>
        /// See comment on IsCodeownersPathValid
        /// </summary>
        public bool IsValid => IsCodeownersPathValid(this.Value.PathExpression);

        /// <summary>
        /// Any CODEOWNERS path with these characters will be skipped.
        /// Note these are valid parts of file paths, but we are not supporting
        /// them to simplify the matcher logic.
        /// </summary>
        private static readonly char[] unsupportedChars = { '[', ']', '!', '?' };

        public MatchedCodeownersEntry(string targetPath, List<CodeownersEntry> codeownersEntries)
        {
            this.Value = GetMatchingCodeownersEntry(targetPath, codeownersEntries);
        }

        /// <summary>
        /// Returns a CodeownersEntry from codeOwnerEntries that matches targetPath
        /// per algorithm described in the GitHub CODEOWNERS reference,
        /// as linked to in this class comment.
        ///
        /// If there is no match, returns "new CodeownersEntry()".
        /// </summary>
        private CodeownersEntry GetMatchingCodeownersEntry(
            string targetPath,
            List<CodeownersEntry> codeownersEntries)
        {
            if (targetPath.Contains('*'))
            {
                Console.Error.WriteLine(
                    $"Target path \"{targetPath}\" contains star ('*') which is not supported. " 
                    + "Returning no match without checking for ownership.");
                return NoMatchCodeownersEntry;
            }

            // targetPath is assumed to be absolute w.r.t. repository root, hence we ensure
            // it starts with "/" to denote that.
            if (!targetPath.StartsWith("/"))
                targetPath = "/" + targetPath;

            // Note we cannot add or trim the slash at the end of targetPath.
            // Slash at the end of target path denotes it is a directory, not a file,
            // so it can not match against a CODEOWNERS entry that is guaranteed to be a file,
            // by the virtue of not ending with "/".
            
            CodeownersEntry matchedEntry = codeownersEntries
                .Where(entry => !ContainsUnsupportedCharacters(entry.PathExpression))
                // Entries listed in CODEOWNERS file below take precedence, hence we read the file from the bottom up.
                // By convention, entries in CODEOWNERS should be sorted top-down in the order of:
                // - 'RepoPath',
                // - 'ServicePath'
                // - and then 'PackagePath'.
                // However, due to lack of validation, as of 12/29/2022 this is not always the case.
                .Reverse()
                .FirstOrDefault(
                    entry => Matches(targetPath, entry), 
                    // assert: none of the codeownersEntries matched targetPath
                    NoMatchCodeownersEntry);

            return matchedEntry;
        }

        private CodeownersEntry NoMatchCodeownersEntry { get; } = new CodeownersEntry();

        /// <summary>
        /// See the comment on unsupportedChars.
        /// </summary>
        private bool ContainsUnsupportedCharacters(string codeownersPath)
        {
            var contains = unsupportedChars.Any(codeownersPath.Contains);
            if (contains)
            {
                Console.Error.WriteLine(
                    $"CODEOWNERS path \"{codeownersPath}\" contains unsupported characters: " +
                    string.Join(' ', unsupportedChars) +
                    " Because of that this path will never match.");
            }
            return contains;
        }

        /// <summary>
        /// Returns true if the regex expression representing the PathExpression
        /// of CODEOWNERS entry matches a prefix of targetPath.
        /// </summary>
        private bool Matches(string targetPath, CodeownersEntry entry)
        {
            string codeownersPath = entry.PathExpression;

            Regex regex = ConvertToRegex(targetPath, codeownersPath);
            // Is prefix match. I.e. it will return true if the regex matches
            // a prefix of targetPath.
            return regex.IsMatch(targetPath);
        }

        private Regex ConvertToRegex(string targetPath, string codeownersPath)
        {
            codeownersPath = NormalizePath(codeownersPath);

            string pattern = codeownersPath;

            if (codeownersPath.Contains(DoubleStar) || pattern.Contains(SingleStar))
            {
                Console.Error.WriteLine(
                    $"CODEOWNERS path \"{codeownersPath}\" contains reserved phrases: " +
                    $"\"{DoubleStar}\" or \"{SingleStar}\"");
            }

            pattern = pattern.Replace("**", DoubleStar);
            pattern = pattern.Replace("*", SingleStar);

            pattern = Regex.Escape(pattern);

            // Denote that all paths are absolute by pre-pending "beginning of string" symbol.
            pattern = "^" + pattern;

            pattern = SetPatternSuffix(targetPath, pattern);

            // Note that the "/**/" case is implicitly covered by "**/".
            pattern = pattern.Replace($"{DoubleStar}/", "(.*)");
            // This case is necessary to cover suffix case, e.g. "/foo/bar/**".
            pattern = pattern.Replace($"/{DoubleStar}", "(.*)");
            // This case is necessary to cover inline **, e.g. "/a**b/".
            pattern = pattern.Replace(DoubleStar, "(.*)");
            pattern = pattern.Replace(SingleStar, "([^/]*)");

            return new Regex(pattern);
        }

        private static string SetPatternSuffix(string targetPath, string pattern)
        {
            // Lack of slash at the end denotes the path is a path to a file,
            // per our validation logic.
            // Note we assume this is path to a file even if the path is invalid,
            // even though in such case the path might be a path to a directory.
            if (!pattern.EndsWith("/"))
            {
                // Append "end of string" symbol, denoting the match has to be exact,
                // not a substring, as we are dealing with a file.
                pattern += "$";
            }
            // If the CODEOWNERS pattern is matching only against directories,
            // but the targetPath may not be a directory
            // (as it doesn't have "/" at the end), we need to trim
            // the "/" from the pattern to ensure match is present.
            //
            // To illustrate this, consider following cases:
            //
            //               1.      2.
            //   targetPath: /a   ,  /a*/
            //      pattern: /a/  ,  /abc
            //
            // Without trimming pattern to be "/a" and "/a*" respectively,
            // these wouldn't match, but they should.
            //
            // On the other hand, trimming the suffix "/" when it is not
            // necessary won't lead to issues. E.g.:
            //
            //   targetPath: /a/b
            //      pattern: /a/
            //
            // Here we still have a prefix match even if we trim pattern to "/a".
            else if (pattern.EndsWith("/") && !targetPath.EndsWith("/"))
            {
                pattern = pattern.TrimEnd('/');
            }

            return pattern;
        }

        /// <summary>
        /// CODEOWNERS paths that do not start with "/" are relative and considered invalid,
        /// See comment on "IsCodeownersPathValid" for definition of "valid".
        /// However, here we handle such cases to accomodate for parsing CODEOWNERS file
        /// paths that somehow slipped through that validation. We do so by instead treating
        /// such paths as if they were absolute to repository root, i.e. starting with "/".
        /// </summary>
        private string NormalizePath(string codeownersPath)
        {
            if (!codeownersPath.StartsWith("/"))
                codeownersPath = "/" + codeownersPath;
            return codeownersPath;
        }

        /// <summary>
        /// The entry is valid if it obeys following conditions:
        /// - The Value was obtained with a call to
        ///   Azure.Sdk.Tools.CodeOwnersParser.CodeownersFile.GetCodeownersEntries().
        ///   - As a consequence, in the case of no match, the entry is not valid.
        /// - It does not contain unsupported characters (see "unsupportedChars").
        /// - the Value.PathExpression starts with "/".
        ///
        /// Once the validation described in the following issue is implemented:
        /// https://github.com/Azure/azure-sdk-tools/issues/4859
        /// to be valid, the entry will also have to obey following conditions:
        /// - if the Value.PathExpression ends with "/", at least one corresponding
        /// directory exists in the repository.
        /// - if the Value.PathExpression does not end with "/", at least one corresponding
        /// file exists in the repository.
        /// </summary>
        private bool IsCodeownersPathValid(string codeownersPath)
            => codeownersPath.StartsWith("/") && !ContainsUnsupportedCharacters(codeownersPath);
    }
}
