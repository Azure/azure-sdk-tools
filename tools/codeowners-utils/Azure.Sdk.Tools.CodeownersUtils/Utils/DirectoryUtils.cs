using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.XPath;
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using Azure.Sdk.Tools.CodeownersUtils.Caches;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Azure.Sdk.Tools.CodeownersUtils.Utils
{
    /// <summary>
    /// Contains the utilities used for verification and matching. The instance methods are all used
    /// by the linter which requires a repository root to determine if CODEOWNERS path is valid for a
    /// given repository. The static methods are also used by the linter but primarily used for matching
    /// a CodeownersEntry's path expression to a file passed in.
    /// </summary>
    public class DirectoryUtils
    {
        private string _repoRoot = null;

        public DirectoryUtils()
        {
        }

        public DirectoryUtils(string repoRoot)
        {
            _repoRoot = repoRoot;
        }

        /// <summary>
        /// Verify the source sourcePathEntry entry for the repository.
        /// </summary>
        /// <param name="sourcePathEntry">The sourcePathEntry to verify, which may contain a pathExpression</param>
        /// <param name="errorStrings">Any errorStrings encountered are added to this list.</param>
        public virtual void VerifySourcePathEntry(string sourcePathEntry, List<string> errorStrings)
        {
            string pathWithoutOwners = ParsingUtils.ParseSourcePathFromLine(sourcePathEntry);
            // The sourcePathEntry is either a pathExpression sourcePathEntry or a sourcePathEntry relative to the repository root
            if (IsGlobFilePath(pathWithoutOwners))
            {
                // Ensure the pathExpression pattern is valid for a CODEOWNERS file
                if (IsValidCodeownersPathExpression(pathWithoutOwners, errorStrings))
                {
                    // If the pathExpression pattern is valid, ensure that it has matches in the repository
                    if (!IsValidGlobPatternForRepo(pathWithoutOwners))
                    {
                        errorStrings.Add($"{pathWithoutOwners}{ErrorMessageConstants.GlobHasNoMatchesInRepoPartial}");
                    }
                }
            }
            else
            {
                // Verify that the sourcePathEntry is valid for the repository
                if (!IsValidRepositoryPath(pathWithoutOwners))
                {
                    errorStrings.Add($"{pathWithoutOwners}{ErrorMessageConstants.PathOrFileNotExistInRepoPartial}");
                }
            }
        }

        /// <summary>
        /// Overloaded IsValidCodeownersPathExpression that throws away the error strings. This function is used by
        /// the matcher where errors aren't being collected but can still be reported if needed.
        /// </summary>
        /// <param name="pathExpression">The pathExpression to check</param>
        /// <param name="reportErrors">Whether or not errors should be reported during </param>
        /// <returns>True if the pathExpression doesn't contain invalid CODEOWNERS pathExpression patterns, false otherwise.</returns>
        public static bool IsValidCodeownersPathExpression(string pathExpression, bool reportErrors = false)
        {
            List<string> errors = new List<string>();
            bool returnValue = IsValidCodeownersPathExpression(pathExpression, errors);
            if (reportErrors)
            {
                // The original matcher used to write errors to Console.Error, do the same here.
                foreach (string error in errors)
                {
                    Console.Error.WriteLine(error);
                }
            }
            return returnValue;
        }

        /// <summary>
        /// Check to see if a pathExpression pattern contains any of the patterns or sequences that are invalid for CODEOWNERS file.
        /// </summary>
        /// <param name="pathExpression">The pathExpression to check</param>
        /// <param name="errorStrings">The list that any discovered errorStrings will be added to. Necessary because there can be multiple errorStrings.</param>
        /// <returns>True if the pathExpression doesn't contain invalid CODEOWNERS pathExpression patterns, false otherwise.</returns>
        public static bool IsValidCodeownersPathExpression(string pathExpression, List<string> errorStrings)
        {
            bool returnValue = true;

            // A path expression can contain all of the invalid characters
            if (pathExpression.Contains(GlobConstants.EscapedPound))
            {
                errorStrings.Add($"{pathExpression}{ErrorMessageConstants.ContainsEscapedPoundPartial}");
                returnValue = false;
            }
            if (pathExpression.Contains(GlobConstants.ExclamationMark))
            {
                errorStrings.Add($"{pathExpression}{ErrorMessageConstants.ContainsNegationPartial}");
                returnValue = false;
            }
            if (pathExpression.Contains(GlobConstants.LeftBracket) || pathExpression.Contains(GlobConstants.RightBracket))
            {
                errorStrings.Add($"{pathExpression}{ErrorMessageConstants.ContainsRangePartial}");
                returnValue = false;
            }
            if (pathExpression.Contains(GlobConstants.QuestionMark))
            {
                errorStrings.Add($"{pathExpression}{ErrorMessageConstants.ContainsQuestionMarkPartial}");
                returnValue = false;
            }

            // A path expression needs to start with a single / but that cannot be the entire expression
            if (pathExpression == GlobConstants.SingleSlash)
            {
                errorStrings.Add(ErrorMessageConstants.PathIsSingleSlash);
                returnValue = false;
            }
            else if (pathExpression == GlobConstants.SingleSlashTwoAsterisksSingleSlash)
            {
                errorStrings.Add($"{ErrorMessageConstants.PathIsSingleSlashTwoAsterisksSingleSlash}");
                returnValue = false;
            }
            else if (!pathExpression.StartsWith(GlobConstants.SingleSlash))
            {
                errorStrings.Add($"{pathExpression}{ErrorMessageConstants.MustStartWithASlashPartial}");
                returnValue = false;
            }

            // The following glob patterns only need to be checked if the path is a glob path and the glob isn't only "/**"
            // or "/**/". Unlike the invalid characters, a path expression won't contain multiple invalid patterns because
            // the invalid patterns are all ones the path has to end with.
            if (IsGlobFilePath(pathExpression) && pathExpression != GlobConstants.SingleSlashTwoAsterisks)
            {
                // The suffix of "/**" is not supported because it is equivalent to "/". For example,
                // "/foo/**" is equivalent to "/foo/". One exception to this rule is if the entire 
                // path is "/**".
                if (pathExpression.EndsWith(GlobConstants.SingleSlashTwoAsterisks))
                {
                    errorStrings.Add($"{pathExpression}{ErrorMessageConstants.GlobCannotEndWithSingleSlashTwoAsterisksPartial}");
                    returnValue = false;
                }
                // The suffix /**/ is invalid. It's effectively equal to /** which is equivalent to /.
                else if (pathExpression.EndsWith(GlobConstants.SingleSlashTwoAsterisksSingleSlash) && 
                         pathExpression != GlobConstants.SingleSlashTwoAsterisksSingleSlash)
                {
                    errorStrings.Add($"{pathExpression}{ErrorMessageConstants.GlobCannotEndWithSingleSlashTwoAsterisksSingleSlashPartial}");
                    returnValue = false;
                }
                // A wildcard pattern ending in * won't work with the file globber. The exception is /* which says match everything
                // in that directory. This also means that we can't say all files beginning with foo*. Wildcarding file types *.md
                // works just fine. For directories a slash at the end /foo*/ would work as intended. It would match all files under
                // directories whose names started with foo, like foobar/myFile or foobar2/myDir/myFile etc.
                else if (pathExpression.EndsWith(GlobConstants.SingleAsterisk) && !pathExpression.EndsWith(GlobConstants.SingleSlash + GlobConstants.SingleAsterisk))
                {
                    errorStrings.Add($"{pathExpression}{ErrorMessageConstants.GlobCannotEndInWildCardPartial}");
                    return false;
                }
            }
            return returnValue;
        }

        /// <summary>
        /// Check whether or not the pathExpression pattern has any matches for the repository. This is only
        /// called by the linter if the path expression is valid
        /// </summary>
        /// <param name="pathExpression">The pathExpression pattern to match.</param>
        /// <returns>True if the pathExpression pattern has results in the repository, false otherwise.</returns>
        public List<string> GetRepoFilesForGlob(string pathExpression)
        {
            // Don't use OrginalIgnoreCase. GitHub is case sensitive for directories
            Matcher matcher = new Matcher(StringComparison.Ordinal);
            matcher.AddInclude(pathExpression);

            // The unfortunate thing about this pathExpression check is that even with a valid pathExpression,
            // there's no real way to know if the pattern has too many matches outside of declaring some arbitrary
            // max number and comparing to that
            var matches = matcher.GetResultsInFullPath(_repoRoot);
            return matches.ToList();
        }

        /// <summary>
        /// Check whether or not the pathExpression pattern has any matches for the repository.
        /// </summary>
        /// <param name="glob">The pathExpression pattern to match.</param>
        /// <returns>True if the pathExpression pattern has results in the repository, false otherwise.</returns>
        private bool IsValidGlobPatternForRepo(string glob)
        {
            var matches = GetRepoFilesForGlob(glob);
            if (matches.ToList().Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Verify that the sourcePathEntry entry exists in the repository if it isn't a glob.
        /// </summary>
        /// <param name="path">the sourcePathEntry to verify</param>
        /// <returns>True if the sourcePathEntry exists in the repository, false otherwise.</returns>
        private bool IsValidRepositoryPath(string path)
        {
            // Path.GetFullPath will normalize the directory separator characters for the platform
            // it's running on. If there is a leading '/" on the path it must be trimmed or combine
            // will think the path is rooted and will just return the path instead of combining it
            // with the repo root.
            var fullPath = Path.GetFullPath(Path.Combine(_repoRoot, path.TrimStart('/')));

            // In CODEOWNERS, the path can be a directory or a file.
            return Directory.Exists(fullPath) || File.Exists(fullPath);
        }

        /// <summary>
        /// The '*' is the only character that can denote pathExpression pattern
        /// in the used globbing library, per:
        /// - https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.filesystemglobbing.matcher?view=dotnet-plat-ext-7.0#remarks
        /// - https://learn.microsoft.com/en-us/dotnet/core/extensions/file-globbing#pattern-formats
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <returns>True if the path is a valid pathExpression path, false otherwise.</returns>
        public static bool IsGlobFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }
            return path.Contains('*');
        }

        /// <summary>
        /// Given a pathExpression and a targetPath, usually a partial file relative to the repository's root (ex.
        /// sdk/SomeServiceDirectory/SomeFileName) check to see if the pathExpression matches the target path.
        /// </summary>
        /// <param name="pathExpression">The path expression to check which may or may not be a glob path.</param>
        /// <param name="targetPath">The file or path to check.</param>
        /// <returns>True if match, false otherwise.</returns>
        public static bool PathExpressionMatchesTargetPath(string pathExpression, string targetPath)
        {
            if (!IsValidCodeownersPathExpression(pathExpression))
            {
                return false;
            }

            // The target path cannot be a wildcard
            if (targetPath.Contains('*'))
            {
                Console.Error.WriteLine(
                    $"Target path \"{targetPath}\" contains star ('*') which is not supported. "
                    + "Returning no match without checking for ownership.");
                return false;
            }

            // The target path can't start with a "/" otherwise the Matcher will try to root it which
            // will cause the match to fail.
            if (targetPath.StartsWith("/"))
            {
                targetPath = targetPath.Substring(1);
            }
            // Don't use OrginalIgnoreCase. GitHub is case sensitive for directories
            Matcher matcher = new Matcher(StringComparison.Ordinal);
            matcher.AddInclude(pathExpression);

            // The unfortunate thing about this pathExpression check is that even with a valid pathExpression,
            // there's no real way to know if the pattern has too many matches outside of declaring some arbitrary
            // max number and comparing to that
            var matches = matcher.Match(targetPath);
            return matches.HasMatches;
        }
    }
}
