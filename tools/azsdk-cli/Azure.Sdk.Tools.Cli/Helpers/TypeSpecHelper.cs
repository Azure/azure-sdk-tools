// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface ITypeSpecHelper
    {
        public bool IsValidTypeSpecProjectPath(string path);
        public bool IsTypeSpecProjectForMgmtPlane(string Path);

        /// <summary>
        /// Parses the TypeSpec project config and runs the metadata emitter to resolve SDK package names.
        /// Returns a fully populated <see cref="TypeSpecProject"/> with TypeSpec info and package list.
        /// </summary>
        public Task<TypeSpecProject?> ParseTypeSpecProjectAsync(string typeSpecProjectPath, INpxHelper npxHelper, ILogger logger);

        /// <summary>
        /// Checks if the path is within either the azure-rest-api-specs repo.
        /// This should also work for forks of these repos.
        /// </summary>
        /// <param name="path">Path within a repo</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>true if within the azure-rest-api-specs repo, false otherwise</returns>
        public Task<bool> IsRepoPathForPublicSpecRepoAsync(string path, CancellationToken ct = default);

        /// <summary>
        /// Checks if the path is within either the azure-rest-api-specs or azure-rest-api-specs-pr repo.
        /// This should also work for forks of these repos.
        /// </summary>
        /// <param name="path">Path within a repo</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>true if one of our specs repos, false otherwise</returns>
        public Task<bool> IsRepoPathForSpecRepoAsync(string path, CancellationToken ct = default);

        public string GetSpecRepoRootPath(string path);
        public string GetTypeSpecProjectRelativePath(string typeSpecProjectPath);

        /// <summary>
        /// Checks if a string is an HTTP or HTTPS URL
        /// </summary>
        public bool IsUrl(string path);

        /// <summary>
        /// Checks if the given string is a GitHub URL pointing to a TypeSpec project in azure-rest-api-specs
        /// </summary>
        public bool IsValidTypeSpecProjectUrl(string url);

        /// <summary>
        /// Determines if a GitHub URL points to a management plane TypeSpec project
        /// </summary>
        public bool IsTypeSpecUrlForMgmtPlane(string url);

        /// <summary>
        /// Extracts the relative specification path from a GitHub URL
        /// </summary>
        public string GetTypeSpecProjectRelativePathFromUrl(string url);
    }
    public partial class TypeSpecHelper : ITypeSpecHelper
    {
        [GeneratedRegex("azure-rest-api-specs(-pr){0,1}(.git){0,1}$")]
        private static partial Regex RestApiSpecsPublicOrPrivateRegex();

        [GeneratedRegex("azure-rest-api-specs{0,1}(.git){0,1}$")]
        private static partial Regex RestApiSpecsPublicRegex();

        [GeneratedRegex(@"^https://github\.com/[^/]+/azure-rest-api-specs/(blob|tree)/[^/]+/specification/.+$", RegexOptions.IgnoreCase)]
        private static partial Regex GitHubSpecUrlRegex();

        private IGitHelper _gitHelper;

        public TypeSpecHelper(IGitHelper gitHelper)
        {
            _gitHelper = gitHelper;
        }

        public bool IsUrl(string path)
        {
            return Uri.TryCreate(path, UriKind.Absolute, out var uri) && 
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        public bool IsValidTypeSpecProjectPath(string path)
        {
            return TypeSpecProject.IsValidTypeSpecProjectPath(path);
        }

        public bool IsTypeSpecProjectForMgmtPlane(string Path)
        {
            var typeSpecObject = TypeSpecProject.ParseTypeSpecConfig(Path);
            return typeSpecObject?.IsManagementPlane ?? false;
        }

        /// <inheritdoc/>
        public async Task<TypeSpecProject?> ParseTypeSpecProjectAsync(string typeSpecProjectPath, INpxHelper npxHelper, ILogger logger)
        {
            try
            {
                if (!IsValidTypeSpecProjectPath(typeSpecProjectPath))
                {
                    logger.LogWarning("Invalid TypeSpec project path: {typeSpecProjectPath}. Skipping metadata emitter.", typeSpecProjectPath);
                    return null;
                }

                var project = TypeSpecProject.ParseTypeSpecConfig(typeSpecProjectPath);

                logger.LogInformation("Running TypeSpec metadata emitter in {ProjectRootPath}", project.ProjectRootPath);

                var npxOptions = new NpxOptions(
                    package: "@typespec/compiler",
                    args: ["tsp", "compile", ".", "--emit", "@azure-tools/typespec-metadata"],
                    logOutputStream: true,
                    workingDirectory: project.ProjectRootPath,
                    timeout: TimeSpan.FromMinutes(5)
                );

                var result = await npxHelper.Run(npxOptions, CancellationToken.None);
                if (result.ExitCode != 0)
                {
                    logger.LogWarning("TypeSpec metadata emitter failed with exit code {ExitCode}. Output: {Output}", result.ExitCode, result.Output);
                    return project;
                }

                var metadataFilePath = Path.Combine(project.ProjectRootPath, "tsp-output", "@azure-tools", "typespec-metadata", "typespec-metadata.yaml");
                if (!File.Exists(metadataFilePath))
                {
                    logger.LogWarning("typespec-metadata.yaml not found at expected path: {metadataFilePath}", metadataFilePath);
                    return project;
                }

                var metadataYaml = await File.ReadAllTextAsync(metadataFilePath);
                logger.LogDebug("TypeSpec metadata YAML: {metadataYaml}", metadataYaml);

                var packages = ParsePackageNamesFromMetadata(metadataYaml);
                if (packages != null)
                {
                    project.Packages = packages;
                }
                return project;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to run TypeSpec metadata emitter");
                return null;
            }
        }

        /// <summary>
        /// Parses the typespec-metadata.yaml to extract SDK package names per language.
        /// Returns a list of <see cref="PackageInfo"/> with Language and PackageName populated.
        /// </summary>
        public static List<PackageInfo>? ParsePackageNamesFromMetadata(string metadataYaml)
        {
            try
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                var metadata = deserializer.Deserialize<Dictionary<string, object>>(metadataYaml);
                if (metadata == null || !metadata.TryGetValue("languages", out var languagesObj))
                {
                    return null;
                }

                var packages = new List<PackageInfo>();

                if (languagesObj is Dictionary<object, object> languages)
                {
                    foreach (var lang in languages)
                    {
                        var languageName = lang.Key?.ToString() ?? string.Empty;
                        var packageName = string.Empty;

                        if (lang.Value is Dictionary<object, object> langProps)
                        {
                            if (langProps.TryGetValue("packageName", out var pkgName))
                            {
                                packageName = pkgName?.ToString() ?? string.Empty;
                            }
                        }

                        if (!string.IsNullOrEmpty(packageName))
                        {
                            packages.Add(new PackageInfo
                            {
                                Language = SdkLanguageHelpers.GetSdkLanguage(languageName),
                                PackageName = packageName
                            });
                        }
                    }
                }

                return packages.Count > 0 ? packages : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<bool> IsRepoPathForPublicSpecRepoAsync(string path, CancellationToken ct = default)
        {
            var uri = await _gitHelper.GetRepoRemoteUriAsync(path, ct);
            return RestApiSpecsPublicRegex().IsMatch(uri.ToString());
        }

        public async Task<bool> IsRepoPathForSpecRepoAsync(string path, CancellationToken ct = default)
        {
            // Docs say this method should work for paths within a repo,
            // so we need to find the repo root first.
            var repoRootPath = await _gitHelper.DiscoverRepoRootAsync(path, ct);
            var uri = await _gitHelper.GetRepoRemoteUriAsync(repoRootPath, ct);
            return RestApiSpecsPublicOrPrivateRegex().IsMatch(uri.ToString());
        }

        public string GetSpecRepoRootPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("path cannot be null or empty.", nameof(path));
            }

            if (IsUrl(path))
            {
                throw new ArgumentException("GetSpecRepoRootPath does not accept URLs. Use local filesystem paths only.", nameof(path));
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

        // URL-specific helper methods
        public bool IsValidTypeSpecProjectUrl(string url)
        {
            return IsUrl(url) && GitHubSpecUrlRegex().IsMatch(url);
        }

        public bool IsTypeSpecUrlForMgmtPlane(string url)
        {
            if (!IsUrl(url))
            {
                return false;
            }
            // For URLs, infer from path - check for .Management or resource-manager
            return url.Contains(".Management", StringComparison.OrdinalIgnoreCase) || 
                   url.Contains("resource-manager", StringComparison.OrdinalIgnoreCase);
        }

        public string GetTypeSpecProjectRelativePathFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url) || !IsValidTypeSpecProjectUrl(url))
            {
                return string.Empty;
            }

            // Parse URL to get the path component (automatically strips query params and fragments)
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            
            int specIndex = path.IndexOf("specification", StringComparison.OrdinalIgnoreCase);
            return specIndex >= 0 ? path[specIndex..] : string.Empty;
        }
    }
}
