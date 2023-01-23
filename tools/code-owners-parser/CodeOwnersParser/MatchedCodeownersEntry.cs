using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// This new matcher supports matching against wildcards (* and **), while the old one doesn't.
    /// This new matcher is designed to work with CODEOWNERS file validation:
    /// https://github.com/Azure/azure-sdk-tools/issues/4859
    ///
    /// The validation spec is given in this comment:
    /// https://github.com/Azure/azure-sdk-tools/issues/4859#issuecomment-1370360622
    ///
    /// To use this class, construct it, passing as input relevant paths.
    /// Then, to obtain the value of the matched entry, reference "Value" member.
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
        /// Returns a CodeownersEntry from codeownersEntries, after normalization and validation,
        /// that match targetPath per algorithm described in the GitHub CODEOWNERS reference,
        /// as linked to in this class comment.
        ///
        /// Paths that are not valid after normalization are skipped from matching.
        ///
        /// If there is no match, this method returns "new CodeownersEntry()".
        /// 
        /// For definition of "normalization", see NormalizePath().
        /// For definition of "validation", see IsCodeownersPathValid().
        /// You can also refer to the validation spec linked from this class comment.
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
                .Where(entry => !ContainsUnsupportedFragments(entry.PathExpression))
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

        private static bool ContainsUnsupportedFragments(string codeownersPath)
            => ContainsUnsupportedCharacters(codeownersPath)
               || ContainsUnsupportedSequences(codeownersPath);

        /// <summary>
        /// See the comment on unsupportedChars.
        /// </summary>
        private static bool ContainsUnsupportedCharacters(string codeownersPath)
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

        private static bool ContainsUnsupportedSequences(string codeownersPath)
        {
            // We do not support suffix of "/**" because it is equivalent to "/".
            // We do not support suffix of "/**/" because it is equivalent to
            // "/**/**" which is equivalent to "/".
            string[] unsupportedSuffixSentences = { "/**", "/**/" };

            foreach (var sequence in unsupportedSuffixSentences)
            {
                if (codeownersPath.EndsWith(sequence))
                {
                    Console.Error.WriteLine(
                        $"CODEOWNERS path \"{codeownersPath}\" ends with " +
                        $"unsupported sequence of \"{sequence}\". Replace it with \"/\". " +
                        "Until then this path will never match.");
                    return true;
                }                
            }

            // We do not support inline "**", i.e. if it is not within slashes, i.e. "/**/".
            // Any inline "**" like "/a**/" or "/**a/" or "/a**b/"
            // would be equivalent to single star, hence we forbid double star, to avoid confusion.
            if (codeownersPath.Replace("/**/", "").Contains("**"))
            {
                Console.Error.WriteLine(
                    $"CODEOWNERS path \"{codeownersPath}\" contains " +
                    "unsupported sequence of \"**\" that is not \"/**/\". Replace it with \"*\". " +
                    "Until then this path will never match.");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if the regex expression representing the PathExpression
        /// of CODEOWNERS entry matches a prefix of targetPath.
        /// </summary>
        private bool Matches(string targetPath, CodeownersEntry entry)
        {
            string codeownersPath = entry.PathExpression;

            Regex regex = ConvertToRegex(codeownersPath);
            // Is prefix match. I.e. it will return true if the regex matches
            // a prefix of targetPath.
            return regex.IsMatch(targetPath);
        }

        private Regex ConvertToRegex(string codeownersPath)
        {
            codeownersPath = NormalizePath(codeownersPath);
            Trace.Assert(IsCodeownersPathValid(codeownersPath));

            string pattern = codeownersPath;

            if (codeownersPath.Contains(SingleStar) || pattern.Contains(DoubleStar))
            {
                Console.Error.WriteLine(
                    $"CODEOWNERS path \"{codeownersPath}\" contains reserved phrases: " +
                    $"\"{DoubleStar}\" or \"{SingleStar}\"");
            }

            // We replace "/**/", not "**", because we disallow "**" in any other context.
            // Specifically:
            // - because we normalize the path to start with "/", any prefix "**/" is
            // effectively "/**/";
            // - any suffix "/**", for reasons explained within ContainsUnsupportedSequences().
            // - any inline "**", for reasons explained within ContainsUnsupportedSequences().
            pattern = pattern.Replace("/**/", "/" + DoubleStar + "/");
            pattern = pattern.Replace("*", SingleStar);

            pattern = Regex.Escape(pattern);

            // Denote that all paths are absolute by pre-pending "beginning of string" symbol.
            pattern = "^" + pattern;

            pattern = SetPatternSuffix(pattern);

            pattern = pattern.Replace($"/{DoubleStar}/", "((/.*/)|/)");
            pattern = pattern.Replace(SingleStar, "([^/]*)");

            return new Regex(pattern);
        }

        private static string SetPatternSuffix(string pattern)
        {
            // Lack of slash at the end denotes the path is a path to a file,
            // per our validation logic.
            //
            // Note we assume this is path to a file even if the path is invalid,
            // even though in such case the path might be a path to a directory.
            if (!pattern.EndsWith("/"))
            {
                // Append "end of string" symbol, denoting the match has to be exact,
                // not a substring, as we are dealing with a file.
                pattern += "$";
            }
            else // The pattern ends with "/", denoting it is a directory.
            {
                // We do not append the end-of-string symbol, "$",
                // because we want to allow matching to any string suffix,
                // which semantically means matching to anything (including nothing)
                // in the directory represented by the path.

                // Note that if the targetPath is exactly the same as pattern,
                // except that it is missing the trailing "/", then we assume
                // no match. This is because The pattern is matching
                // against a directory, which, due to our validation,
                // should exist. The targetPath, because it doesn't have trailing "/",
                // matches to a file with the same name, hence it cannot exist.
                //
                // For example:
                // pattern:    /a/
                // targetPath: /a
                // result:     no match
                //
                // There is no match because "/a" is not a substring of "/a/".
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
        private static string NormalizePath(string codeownersPath)
        {
            if (!codeownersPath.StartsWith("/"))
                codeownersPath = "/" + codeownersPath;
            return codeownersPath;
        }

        /// <summary>
        /// The codeownersPathExpression is valid if it obeys following conditions:
        /// - It starts with "/".
        /// - It doesn't contain unsupported fragments, including unsupported
        /// characters, character sequences and character sequence suffixes.
        ///
        /// Once the validation described in the following issue is implemented:
        /// https://github.com/Azure/azure-sdk-tools/issues/4859
        /// to be valid, the entry will also have to obey following conditions:
        /// - if the path expression ends with "/" at least one matching
        /// directory exists in the repository.
        /// - if the path expression does not end with "/", at least one matching
        /// file exists in the repository.
        /// </summary>
        private bool IsCodeownersPathValid(string codeownersPathExpression)
            => codeownersPathExpression.StartsWith("/") && !ContainsUnsupportedFragments(codeownersPathExpression);
    }
}
