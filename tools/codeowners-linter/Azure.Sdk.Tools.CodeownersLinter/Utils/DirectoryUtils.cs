using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersLinter.Constants;
using Azure.Sdk.Tools.CodeownersLinter.Holders;
using Azure.Sdk.Tools.CodeOwnersParser;
using Azure.Sdk.Tools.CodeOwnersParser.Constants;
using Microsoft.Extensions.FileSystemGlobbing;
using Octokit;

namespace Azure.Sdk.Tools.CodeownersLinter.Utils
{
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
        /// <param name="sourcePathEntry">The sourcePathEntry to verify, which may contain a glob</param>
        /// <param name="errorStrings">Any errorStrings encountered are added to this list.</param>
        public virtual void VerifySourcePathEntry(string sourcePathEntry, List<string> errorStrings)
        {
            string pathWithoutOwners = sourcePathEntry;
            // Owners, or lack thereof, are tracked elsewhere.
            if (pathWithoutOwners.Contains(SeparatorConstants.Owner))
            {
                // Grab the string up to the character before the owner constant
                pathWithoutOwners = pathWithoutOwners.Substring(0, pathWithoutOwners.IndexOf(SeparatorConstants.Owner));
            }
            pathWithoutOwners = pathWithoutOwners.Substring(0).Replace('\t', ' ').Trim();
            // The sourcePathEntry is either a glob sourcePathEntry or a sourcePathEntry relative to the repository root
            if (GlobFilePath.IsGlobFilePath(pathWithoutOwners))
            {
                // Ensure the glob pattern is valid for a CODEOWNERS file
                if (IsValidCodeownersGlobPattern(pathWithoutOwners, errorStrings))
                {
                    // If the glob pattern is valid, ensure that it has matches in the repository
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
        /// Check to see if a glob pattern contains any of the patterns that are invalid for CODEOWNERS file.
        /// </summary>
        /// <param name="glob">The glob to check</param>
        /// <param name="errorStrings">The list that any discovered errorStrings will be added to. Necessary because there can be multiple errorStrings.</param>
        /// <returns>True if the glob doesn't contain invalid CODEOWNERS glob patterns, false otherwise.</returns>
        public bool IsValidCodeownersGlobPattern(string glob, List<string> errorStrings)
        {
            bool returnValue = true;

            if (glob.Contains(InvalidGlobPatterns.EscapedPound))
            {
                errorStrings.Add($"{glob}{ErrorMessageConstants.ContainsEscapedPoundPartial}");
                returnValue = false;
            }
            if (glob.Contains(InvalidGlobPatterns.ExclamationMark))
            {
                errorStrings.Add($"{glob}{ErrorMessageConstants.ContainsNegationPartial}");
                returnValue = false;
            }
            if (glob.Contains(InvalidGlobPatterns.LeftBracket) && glob.Contains(InvalidGlobPatterns.RightBracket))
            {
                errorStrings.Add($"{glob}{ErrorMessageConstants.ContainsRangePartial}");
                returnValue = false;
            }
            return returnValue;
        }

        /// <summary>
        /// Check whether or not the glob pattern has any matches for the repository.
        /// </summary>
        /// <param name="glob">The glob pattern to match.</param>
        /// <returns>True if the glob pattern has results in the repository, false otherwise.</returns>
        private bool IsValidGlobPatternForRepo(string glob)
        {
            Matcher matcher = new();
            matcher.AddIncludePatterns(new[] { glob });

            // There are a few unfortunate things about this glob check:
            // 1. If the glob pattern is syntatically invalid it just returns no matches.
            // 2. Even if the glob pattern is valid, there's no real way to know if the pattern has too many matches outside
            //    of declaring some arbitrary number and comparing to that
            // 3. CODEOWNERS glob patterns can have *s in them but but !, [] and \# (escaped pound
            var matches = matcher.GetResultsInFullPath(_repoRoot);

            if (matches.ToList().Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Verify that the sourcePathEntry entry exists in the repository.
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
    }
}
