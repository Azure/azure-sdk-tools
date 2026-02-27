// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services.Languages.Samples;

namespace Azure.Sdk.Tools.Cli.Services.Languages
{
    public abstract class LanguageService
    {
        protected readonly IProcessHelper processHelper;
        protected readonly IGitHelper gitHelper;
        protected readonly ILogger<LanguageService> logger;
        protected readonly ICommonValidationHelpers commonValidationHelpers;
        protected readonly IPackageInfoHelper packageInfoHelper;
        protected readonly IFileHelper fileHelper;
        protected readonly ISpecGenSdkConfigHelper specGenSdkConfigHelper;
        protected readonly IChangelogHelper changelogHelper;

        /// <summary>
        /// Protected parameterless constructor for test mocking purposes.
        /// </summary>
        protected LanguageService()
        {
            processHelper = null!;
            gitHelper = null!;
            logger = null!;
            commonValidationHelpers = null!;
            packageInfoHelper = null!;
            fileHelper = null!;
            specGenSdkConfigHelper = null!;
            changelogHelper = null!;
        }

        protected LanguageService(
            IProcessHelper processHelper,
            IGitHelper gitHelper,
            ILogger<LanguageService> logger,
            ICommonValidationHelpers commonValidationHelpers,
            IPackageInfoHelper packageInfoHelper,
            IFileHelper fileHelper,
            ISpecGenSdkConfigHelper specGenSdkConfigHelper,
            IChangelogHelper changelogHelper)
        {
            this.processHelper = processHelper;
            this.gitHelper = gitHelper;
            this.logger = logger;
            this.commonValidationHelpers = commonValidationHelpers;
            this.packageInfoHelper = packageInfoHelper;
            this.fileHelper = fileHelper;
            this.specGenSdkConfigHelper = specGenSdkConfigHelper;
            this.changelogHelper = changelogHelper;
        }

        public abstract SdkLanguage Language { get; }
        public virtual bool IsCustomizedCodeUpdateSupported => false;

#pragma warning disable CS1998
        public async virtual Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetPackageInfo is not implemented for this language.");
        }
#pragma warning restore CS1998

        /// <summary>
        /// Discovers all packages in a service directory (or all services if empty).
        /// Returns fully-populated PackageInfo including CI parameters and triggering paths.
        /// Default implementation discovers package directories and calls GetPackageInfo for each.
        /// </summary>
        /// <param name="repoRoot">Absolute path to the repository root.</param>
        /// <param name="serviceDirectory">Service directory under sdk/ (e.g., "storage"). Empty for all services.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of discovered packages with CI parameters populated.</returns>
        public virtual async Task<IReadOnlyList<PackageInfo>> DiscoverPackagesAsync(
            string repoRoot,
            string? serviceDirectory,
            CancellationToken ct = default)
        {
            var sdkRoot = Path.Combine(repoRoot, "sdk");
            var normalizedServiceDirectory = serviceDirectory;
            // Handle service directories passed with sdk like 'sdk/core'
            if (!string.IsNullOrWhiteSpace(normalizedServiceDirectory))
            {
                normalizedServiceDirectory = NormalizedPath.Normalize(normalizedServiceDirectory);
                if (normalizedServiceDirectory.StartsWith("sdk/", StringComparison.OrdinalIgnoreCase))
                {
                    normalizedServiceDirectory = normalizedServiceDirectory["sdk/".Length..];
                }
            }

            var searchRoot = string.IsNullOrWhiteSpace(serviceDirectory)
                ? sdkRoot
                : Path.Combine(sdkRoot, normalizedServiceDirectory!);

            if (!Directory.Exists(searchRoot))
            {
                return [];
            }

            var packageDirectories = DiscoverPackageDirectories(searchRoot, !string.IsNullOrWhiteSpace(normalizedServiceDirectory));
            var packages = new List<PackageInfo>();

            foreach (var packageDirectory in packageDirectories)
            {
                try
                {
                    var packageInfo = await GetPackageInfo(packageDirectory, ct);
                    PopulateCiMetadata(packageInfo);
                    packages.Add(packageInfo);
                }
                catch (Exception ex)
                {
                    logger?.LogDebug(ex, "Failed to get package info for {directory}", packageDirectory);
                }
            }

            return packages;
        }

        /// <summary>
        /// Discovers package directories under the search root.
        /// Override this to customize package discovery for a language.
        /// </summary>
        protected virtual IEnumerable<string> DiscoverPackageDirectories(string searchRoot, bool isServiceDirectory)
        {
            if (PackageManifestPatterns.Length == 0)
            {
                return [];
            }


            var packageRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pattern in PackageManifestPatterns)
            {
                var enumerationOptions = new EnumerationOptions()
                {
                    RecurseSubdirectories = true,
                    MaxRecursionDepth = 3
                };

                foreach (var filePath in Directory.EnumerateFiles(searchRoot, pattern, enumerationOptions))
                {
                    var packageRoot = GetPackageRootFromManifest(filePath);
                    if (!string.IsNullOrEmpty(packageRoot))
                    {
                        packageRoots.Add(packageRoot);
                    }
                }
            }

            return packageRoots;
        }

        /// <summary>
        /// File patterns used to identify package manifest files (e.g., "pom.xml", "package.json").
        /// Override in derived classes to specify language-specific patterns.
        /// </summary>
        protected virtual string[] PackageManifestPatterns => [];

        protected virtual void ApplyLanguageCiParameters(PackageInfo packageInfo)
        {
        }

        protected void PopulateCiMetadata(PackageInfo packageInfo)
        {
            packageInfoHelper.PopulateCommonCiMetadata(packageInfo);
            ApplyLanguageCiParameters(packageInfo);
        }

        /// <summary>
        /// Gets the package root directory from a manifest file path.
        /// Default implementation returns the directory containing the manifest.
        /// Override in derived classes if the manifest is in a subdirectory (e.g., src/).
        /// </summary>
        protected virtual string? GetPackageRootFromManifest(string manifestPath)
        {
            return Path.GetDirectoryName(manifestPath);
        }

        /// <summary>
        /// Analyzes dependencies for the specific package.
        /// </summary>
        /// <param name="packagePath">Path to the package directory</param>
        /// <param name="fixCheckErrors">Whether to attempt to automatically fix dependency issues</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the dependency analysis</returns>
        public virtual Task<PackageCheckResponse> AnalyzeDependencies(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageCheckResponse(0, "noop", "This is not an applicable operation for this language."));
        }
        /// <summary>
        /// Validates the README for the specific package.
        /// </summary>
        /// <param name="packagePath">Path to the package directory</param>
        /// <param name="fixCheckErrors">Whether to attempt to automatically fix README issues</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the README validation</returns>
        public virtual Task<PackageCheckResponse> ValidateReadme(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageCheckResponse(0, "noop", "This is not an applicable operation for this language."));
        }

        /// <summary>
        /// Checks spelling in the specific package.
        /// </summary>
        /// <param name="packagePath">Path to the package directory</param>
        /// <param name="fixCheckErrors">Whether to attempt to automatically fix spelling issues where supported by cspell</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the spelling check</returns>
        public virtual Task<PackageCheckResponse> CheckSpelling(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageCheckResponse(0, "noop", "This is not an applicable operation for this language."));
        }

        /// <summary>
        /// Updates code snippets in the specific package using language-specific tools.
        /// </summary>
        /// <param name="packagePath">Path to the package directory</param>
        /// <param name="fixCheckErrors">Whether to attempt to automatically fix snippet issues</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the snippet update operation</returns>
        public virtual Task<PackageCheckResponse> UpdateSnippets(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageCheckResponse(0, "noop", "This is not an applicable operation for this language."));
        }

        /// <summary>
        /// Lints code in the specific package using language-specific tools.
        /// </summary>
        /// <param name="packagePath">Path to the package directory</param>
        /// <param name="fixCheckErrors">Whether to automatically fix linting issues</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the code linting operation</returns>
        public virtual Task<PackageCheckResponse> LintCode(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageCheckResponse(0, "noop", "This is not an applicable operation for this language."));
        }

        /// <summary>
        /// Formats code in the specific package using language-specific tools.
        /// </summary>
        /// <param name="packagePath">Path to the package directory</param>
        /// <param name="fixCheckErrors">Whether to automatically apply code formatting</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the code formatting operation</returns>
        public virtual Task<PackageCheckResponse> FormatCode(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageCheckResponse(0, "noop", "This is not an applicable operation for this language."));
        }

        /// Validate samples for the specific package.
        /// </summary>
        /// <param name="packagePath">Path to the package directory</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the sample validation</returns>
        public virtual Task<PackageCheckResponse> ValidateSamples(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageCheckResponse(0, "noop", "This is not an applicable operation for this language."));
        }

        /// <summary>
        /// Checks AOT compatibility for the specific package using language-specific tools.
        /// </summary>
        /// <param name="packagePath">Path to the package directory</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the AOT compatibility check</returns>
        public virtual Task<PackageCheckResponse> CheckAotCompat(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageCheckResponse(0, "noop", "This is not an applicable operation for this language."));
        }

        /// <summary>
        /// Checks generated code for the specific package using language-specific tools.
        /// </summary>
        /// <param name="packagePath">Path to the package directory</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the generated code check</returns>
        public virtual Task<PackageCheckResponse> CheckGeneratedCode(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageCheckResponse(0, "noop", "This is not an applicable operation for this language."));
        }
        /// <summary>
        /// Validates the changelog for the specific package.
        /// </summary>
        /// <param name="packagePath">Path to the package directory</param>
        /// <param name="fixCheckErrors">Whether to attempt to automatically fix changelog issues</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the changelog validation</returns>
        public virtual Task<PackageCheckResponse> ValidateChangelog(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PackageCheckResponse(0, "noop", "This is not an applicable operation for this language."));
        }

        /// <summary>
        /// Runs all tests in the specified package.
        /// </summary>
        /// <param name="packagePath">The path to the package containing the tests.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>A <see cref="TestRunResponse"/> containing process output details.</returns>
        public virtual Task<TestRunResponse> RunAllTests(string packagePath, CancellationToken ct = default)
        {
            return Task.FromResult(new TestRunResponse(0, "This is not an applicable operation for this language."));
        }

        /// <summary>
        /// Produces an API change list by diffing file contents between two generated source trees.
        /// Implementations may perform a structural or textual diff; when <paramref name="oldGenerationPath"/> is null
        /// they should treat the operation as an initial generation (returning an empty change list).
        /// </summary>
        /// <param name="oldGenerationPath">Previous generation</param>
        /// <param name="newGenerationPath">New/current generation root.</param>
        /// <returns>List of detected API changes (empty if no differences).</returns>
        public virtual Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath)
        {
            List<ApiChange> result = [];
            return Task.FromResult(result);
        }

        /// <summary>
        /// Determines whether the package has customizations and returns their root directory.
        /// </summary>
        /// <param name="packagePath">Root folder of the package (e.g. SDK package directory).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Path to customization root directory if customizations exist, null otherwise.</returns>
        public virtual string? HasCustomizations(string packagePath, CancellationToken ct = default)
        {
            return null;
        }

        /// <summary>
        /// Applies patches to customization files based on build errors.
        /// This is a mechanical worker - it applies safe patches and returns results.
        /// The Classifier (Phase A) does the thinking and routing.
        /// </summary>
        /// <param name="customizationRoot">Path to the customization root directory</param>
        /// <param name="packagePath">Path to the package directory containing generated code</param>
        /// <param name="buildError">The build error that triggered repair</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>List of applied patches</returns>
        public virtual Task<List<AppliedPatch>> ApplyPatchesAsync(
            string customizationRoot,
            string packagePath,
            string buildError,
            CancellationToken ct)
        {
            return Task.FromResult(new List<AppliedPatch>());
        }

        /// <summary>
        /// Performs language-specific validation (build, compile, tests, lint, type-check, etc.).
        /// </summary>
        /// <param name="packagePath">Path to the package directory containing generated code.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A <see cref="ValidationResult"/> indicating success or a list of validation errors.</returns>
        public virtual async Task<ValidationResult> ValidateAsync(string packagePath, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                return ValidationResult.CreateFailure("Package path not specified");
            }
            if (!Directory.Exists(packagePath))
            {
                return ValidationResult.CreateFailure($"Package path not found: {packagePath}");
            }

            try
            {
                var depResult = await AnalyzeDependencies(packagePath, false, ct);
                if (depResult.ExitCode == 0)
                {
                    return ValidationResult.CreateSuccess();
                }
                var errorMessage = string.IsNullOrWhiteSpace(depResult.ResponseError) ? depResult.CheckStatusDetails : depResult.ResponseError;
                return ValidationResult.CreateFailure(errorMessage ?? "Validation failed");
            }
            catch (Exception ex)
            {
                return ValidationResult.CreateFailure($"Validation exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the package metadata content for a specified package.
        /// </summary>
        /// <param name="packagePath">The absolute path to the package directory.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>A response indicating the result of the metadata update operation.</returns>
        public virtual Task<PackageOperationResponse> UpdateMetadataAsync(string packagePath, CancellationToken ct)
        {
            this.logger.LogInformation("No language-specific package metadata update implementation found for package path: {packagePath}.", packagePath);
            return Task.FromResult(PackageOperationResponse.CreateSuccess("No package metadata updates need to be performed.", nextSteps: ["Update the version when preparing for a release"]));
        }

        /// <summary>
        /// Updates the changelog content for a specified package.
        /// </summary>
        /// <param name="packagePath">The absolute path to the package directory.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>A response indicating the result of the changelog update operation.</returns>
        public virtual Task<PackageOperationResponse> UpdateChangelogContentAsync(string packagePath, CancellationToken ct)
        {
            return Task.FromResult(
                PackageOperationResponse.CreateSuccess(
                    "Changelog untouched; manual edits required.",
                    nextSteps: [
                        "Summarize version updates under 'Features Added', 'Breaking Changes', 'Bug Fixes', and 'Other Changes'",
                        "Refer to this documentation: https://eng.ms/docs/products/azure-developer-experience/develop/sdk-release/sdk-release-prerequisites",
                        "Update package metadata after the changelog content has been updated"
                        ],
                    result: "noop"));
        }

        /// <summary>
        /// Updates the version for a specified package.
        /// This method performs two steps:
        /// 1. Updates the release date in the changelog (common across all languages)
        /// 2. Calls language-specific version update logic via <see cref="UpdatePackageVersionInFilesAsync"/>
        /// </summary>
        /// <param name="packagePath">The absolute path to the package directory.</param>
        /// <param name="releaseType">Specifies whether the next version is 'beta' or 'stable'.</param>
        /// <param name="version">Specifies the next version number.</param>
        /// <param name="releaseDate">The date (YYYY-MM-DD) to write into the changelog.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>A response indicating the result of the version update operation.</returns>
        public virtual async Task<PackageOperationResponse> UpdateVersionAsync(string packagePath, string? releaseType, string? version, string? releaseDate, CancellationToken ct)
        {
            logger.LogInformation("Updating version for package at: {PackagePath}", packagePath);
            var packageInfo = await GetPackageInfo(packagePath, ct);

            // Use provided version or get current version from package
            var targetVersion = version;
            if (string.IsNullOrWhiteSpace(targetVersion))
            {
                targetVersion = packageInfo?.PackageVersion;
                if (string.IsNullOrWhiteSpace(targetVersion))
                {
                    return PackageOperationResponse.CreateFailure(
                        "Version is required. Unable to determine the current package version.",
                        packageInfo: packageInfo,
                        nextSteps: ["Provide the version parameter explicitly"]);
                }
            }

            // Step 1: Update the changelog release date (common across all languages)
            var changelogPath = changelogHelper.GetChangelogPath(packagePath);
            if (changelogPath == null)
            {
                logger.LogWarning("No CHANGELOG.md found in package directory: {PackagePath}", packagePath);
                return PackageOperationResponse.CreateFailure(
                    "No CHANGELOG.md found in package directory.",
                    packageInfo: packageInfo,
                    nextSteps: [
                        "Ensure CHANGELOG.md exists in the package root directory",
                        "Run another tool to update the changelog content"
                    ]);
            }

            // releaseDate is already validated and defaulted by VersionUpdateTool
            // Update the changelog with the release date
            // This will also validate that an entry exists for the version
            var changelogResult = changelogHelper.UpdateReleaseDate(changelogPath, targetVersion, releaseDate);
            if (!changelogResult.Success)
            {
                logger.LogWarning("Failed to update changelog: {Message}", changelogResult.Message);
                return PackageOperationResponse.CreateFailure(
                    changelogResult.Message ?? "Failed to update changelog.",
                    packageInfo: packageInfo,
                    nextSteps: [
                        "Run another tool to update the changelog content for this version first",
                        "Then run this tool again to set the release date"
                    ]);
            }

            logger.LogInformation("Changelog updated successfully: {Message}", changelogResult.Message);

            // Step 2: Call language-specific version update logic
            var versionUpdateResult = await UpdatePackageVersionInFilesAsync(packagePath, targetVersion, releaseType, ct);
            if (versionUpdateResult.OperationStatus == Status.Failed)
            {
                // Changelog was updated but version files failed - report partial success
                return PackageOperationResponse.CreateSuccess(
                    $"Changelog release date updated to {releaseDate}, but version file update requires additional steps.",
                    nextSteps: versionUpdateResult.NextSteps?.ToArray() ?? ["Manually update the package version in project files"],
                    result: "partial",
                    packageInfo: packageInfo);
            }

            // If version update returned partial success (e.g., not implemented by specific language),
            // wrap the result to include packageInfo while preserving message and next steps
            if (versionUpdateResult.Result is "partial")
            {
                return PackageOperationResponse.CreateSuccess(
                    versionUpdateResult.Message,
                    nextSteps: versionUpdateResult.NextSteps?.ToArray(),
                    result: versionUpdateResult.Result as string,
                    packageInfo: packageInfo);
            }

            return PackageOperationResponse.CreateSuccess(
                $"Version {targetVersion} updated with release date {releaseDate}.",
                nextSteps: ["Review the changes", "Run validation checks"],
                packageInfo: packageInfo);
        }

        /// <summary>
        /// Updates the package version in language-specific files (e.g., .csproj, pom.xml, package.json).
        /// Override this method in derived classes to implement language-specific version update logic.
        /// </summary>
        /// <param name="packagePath">The absolute path to the package directory.</param>
        /// <param name="version">The version to set.</param>
        /// <param name="releaseType">Specifies whether the version is 'beta' or 'stable'.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>A response indicating the result of the version file update operation.</returns>
        protected virtual Task<PackageOperationResponse> UpdatePackageVersionInFilesAsync(string packagePath, string version, string? releaseType, CancellationToken ct)
        {
            logger.LogInformation("No language-specific version file update implementation for {Language}. Only changelog was updated.", Language);
            return Task.FromResult(PackageOperationResponse.CreateSuccess(
                "Changelog updated. Language-specific version file update not implemented.",
                nextSteps: ["Manually update the package version in project files if needed"],
                result: "partial"));
        }

        /// <summary>
        /// Get sample language context for sample generation.
        /// </summary>
        public virtual SampleLanguageContext SampleLanguageContext
        {
            get
            {
                return Language switch
                {
                    SdkLanguage.DotNet => new DotNetSampleLanguageContext(fileHelper),
                    SdkLanguage.Java => new JavaSampleLanguageContext(fileHelper),
                    SdkLanguage.JavaScript => new TypeScriptSampleLanguageContext(fileHelper),
                    SdkLanguage.Python => new PythonSampleLanguageContext(fileHelper),
                    SdkLanguage.Go => new GoSampleLanguageContext(fileHelper),
                    _ => throw new NotImplementedException($"Sample language context is not implemented for language: {Language}"),
                };
            }
        }

        /// <summary>
        /// Builds/compiles SDK code for the specified package using repository-configured build scripts.
        /// This method retrieves build configuration and executes the appropriate build command or script.
        /// </summary>
        /// <param name="packagePath">Absolute path to the SDK package directory.</param>
        /// <param name="timeoutMinutes">Maximum time to wait for the build process to complete.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A tuple containing: Success (bool), ErrorMessage (string? - null if successful), PackageInfo (PackageInfo? - package metadata if available).</returns>
        public virtual async Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo)> BuildAsync(string packagePath, int timeoutMinutes = 30, CancellationToken ct = default)
        {
            try
            {
                // Skip build for Python projects early (Python SDKs don't require compilation)
                if (Language == SdkLanguage.Python)
                {
                    logger.LogDebug("Python SDK - skipping build");
                    return (true, null, null);
                }

                logger.LogInformation("Building SDK for project path: {PackagePath}", packagePath);

                // Validate package path
                if (string.IsNullOrWhiteSpace(packagePath))
                {
                    return (false, "Package path is required and cannot be empty.", null);
                }

                // Resolves relative paths to absolute
                string fullPath = Path.GetFullPath(packagePath);

                if (!Directory.Exists(fullPath))
                {
                    return (false, $"Package full path does not exist: {fullPath}, input package path: {packagePath}.", null);
                }

                packagePath = fullPath;

                // Get repository root path from project path
                string sdkRepoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, ct);
                if (string.IsNullOrEmpty(sdkRepoRoot))
                {
                    return (false, $"Failed to discover local sdk repo with project-path: {packagePath}.", null);
                }

                PackageInfo? packageInfo = await GetPackageInfo(packagePath, ct);

                var (configContentType, configValue) = await specGenSdkConfigHelper.GetConfigurationAsync(sdkRepoRoot, SpecGenSdkConfigType.Build);
                if (configContentType == SpecGenSdkConfigContentType.Unknown || string.IsNullOrEmpty(configValue))
                {
                    return (false, "No build configuration found or failed to prepare the build command.", packageInfo);
                }

                logger.LogDebug("Found valid configuration for build process. Executing configured script...");

                // Prepare script parameters
                var scriptParameters = new Dictionary<string, string>
                {
                    { "PackagePath", packagePath }
                };

                // Create process options for the build script
                var processOptions = specGenSdkConfigHelper.CreateProcessOptions(configContentType, configValue, sdkRepoRoot, packagePath, scriptParameters, timeoutMinutes);
                if (processOptions == null)
                {
                    return (false, "Failed to create process options for build command.", packageInfo);
                }

                // Execute the build process directly
                var result = await processHelper.Run(processOptions, ct);
                var trimmedOutput = (result.Output ?? string.Empty).Trim();

                if (result.ExitCode != 0)
                {
                    var errorMessage = $"Build failed with exit code {result.ExitCode}. Output:\n{trimmedOutput}";
                    logger.LogDebug("Build failed: {ErrorMessage}", errorMessage);
                    return (false, errorMessage, packageInfo);
                }

                logger.LogDebug("Build completed successfully.");
                return (true, null, packageInfo);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while building SDK code");
                return (false, $"An error occurred: {ex.Message}", null);
            }
        }

        protected static string? GetSpecProjectPath(string packagePath)
        {
            var tspLocationPath = Path.Combine(packagePath, "tsp-location.yaml");
            if (!File.Exists(tspLocationPath))
            {
                return null;
            }

            try
            {
                using var reader = new StreamReader(tspLocationPath);
                var tspLocation = TspLocationYamlDeserializer.Deserialize<TspLocation>(reader);
                return tspLocation?.Directory;
            }
            catch
            {
                return null;
            }
        }

        private static readonly YamlDotNet.Serialization.IDeserializer TspLocationYamlDeserializer =
            new YamlDotNet.Serialization.DeserializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.NullNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

        private class TspLocation
        {
            [YamlDotNet.Serialization.YamlMember(Alias = "directory")]
            public string? Directory { get; set; }
        }
    }
}
