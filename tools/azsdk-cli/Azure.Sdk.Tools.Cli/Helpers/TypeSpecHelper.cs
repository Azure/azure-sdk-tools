// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface ITypeSpecHelper
    {
        public bool IsValidTypeSpecProjectPath(string path);
        public bool IsTypeSpecProjectForMgmtPlane(string Path);

        /// <summary>
        /// Checks if the path is within either the azure-rest-api-specs repo.
        /// This should also work for forks of these repos.
        /// </summary>
        /// <param name="path">Path within a repo</param>
        /// <returns>true if within the azure-rest-api-specs repo, false otherwise</returns>
        public bool IsRepoPathForPublicSpecRepo(string path);

        /// <summary>
        /// Checks if the path is within either the azure-rest-api-specs or azure-rest-api-specs-pr repo.
        /// This should also work for forks of these repos.
        /// </summary>
        /// <param name="path">Path within a repo</param>
        /// <returns>true if one of our specs repos, false otherwise</returns>
        public bool IsRepoPathForSpecRepo(string path);

        public string GetSpecRepoRootPath(string path);
        public string GetTypeSpecProjectRelativePath(string typeSpecProjectPath);
    }
    public partial class TypeSpecHelper : ITypeSpecHelper
    {
        [GeneratedRegex("azure-rest-api-specs(-pr){0,1}(.git){0,1}$")]
        private static partial Regex RestApiSpecsPublicOrPrivateRegex();

        [GeneratedRegex("azure-rest-api-specs{0,1}(.git){0,1}$")]
        private static partial Regex RestApiSpecsPublicRegex();

        [GeneratedRegex(@"^https://github\.com/[^/]+/azure-rest-api-specs/(blob|tree)/[^/]+/specification/.+$", RegexOptions.IgnoreCase)]
        private static partial Regex GitHubSpecUrlRegex();

        [GeneratedRegex(@"^https://github\.com/[^/]+/azure-rest-api-specs(-pr)?/(blob|tree)/[^/]+/specification/.+$", RegexOptions.IgnoreCase)]
        private static partial Regex GitHubSpecUrlPublicOrPrivateRegex();

        private IGitHelper _gitHelper;

        public TypeSpecHelper(IGitHelper gitHelper)
        {
            _gitHelper = gitHelper;
        }

        private static bool IsUrl(string path)
        {
            return Uri.TryCreate(path, UriKind.Absolute, out var uri) && 
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static bool IsValidGitHubSpecUrl(string url)
        {
            return GitHubSpecUrlRegex().IsMatch(url) && 
                   !url.Contains("azure-rest-api-specs-pr", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsValidTypeSpecProjectPath(string path)
        {
            if (IsUrl(path))
            {
                return IsValidGitHubSpecUrl(path);
            }
            return TypeSpecProject.IsValidTypeSpecProjectPath(path);
        }

        public bool IsTypeSpecProjectForMgmtPlane(string Path)
        {
            if (IsUrl(Path))
            {
                // For URLs, infer from path - check for .Management or resource-manager
                return Path.Contains(".Management", StringComparison.OrdinalIgnoreCase) || 
                       Path.Contains("resource-manager", StringComparison.OrdinalIgnoreCase);
            }
            var typeSpecObject = TypeSpecProject.ParseTypeSpecConfig(Path);
            return typeSpecObject?.IsManagementPlane ?? false;
        }

        public bool IsRepoPathForPublicSpecRepo(string path)
        {
            if (IsUrl(path))
            {
                return IsValidGitHubSpecUrl(path);
            }
            var repoUri = _gitHelper.GetRepoRemoteUri(path);
            return RestApiSpecsPublicRegex().IsMatch(repoUri.ToString());
        }

        public bool IsRepoPathForSpecRepo(string path)
        {
            if (IsUrl(path))
            {
                // For URLs, validate proper GitHub blob URL structure for public or private specs repos
                return GitHubSpecUrlPublicOrPrivateRegex().IsMatch(path);
            }
            
            // Docs say this method should work for paths within a repo,
            // so we need to find the repo root first.
            var repoRootPath = _gitHelper.DiscoverRepoRoot(path);
            var uri = _gitHelper.GetRepoRemoteUri(repoRootPath);
            return RestApiSpecsPublicOrPrivateRegex().IsMatch(uri.ToString());
        }

        public string GetSpecRepoRootPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("path cannot be null or empty.", nameof(path));
            }

            // just return it as-is for validation
            if (IsUrl(path))
            {
                return path;
            }

            if (Directory.Exists(Path.Combine(path, "specification")))
            {
                return path;
            }

            // Get absolute path for repo root from given path.
            // Repo root is the parent of "specification" folder.
            var currentDirectory = new DirectoryInfo(path);
            while (currentDirectory != null && !currentDirectory.Name.Equals("specification"))
            {
                currentDirectory = currentDirectory.Parent;
            }
            return currentDirectory?.Parent?.FullName ?? string.Empty;
        }

        public string GetTypeSpecProjectRelativePath(string typeSpecProjectPath)
        {
            if (string.IsNullOrEmpty(typeSpecProjectPath) || !IsValidTypeSpecProjectPath(typeSpecProjectPath))
            {
                return string.Empty;
            }

            int specIndex = typeSpecProjectPath.IndexOf("specification");
            return specIndex >= 0 ? typeSpecProjectPath[specIndex..].Replace("\\", "/") : string.Empty;
        }
    }
}
