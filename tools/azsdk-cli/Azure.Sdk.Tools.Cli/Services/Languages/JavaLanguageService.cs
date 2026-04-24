// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.CopilotAgents.Tools;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Prompts.Templates;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// Language service implementation for Java SDK packages.
/// </summary>
public sealed partial class JavaLanguageService : LanguageService
{
    /// <inheritdoc/>
    public override SdkLanguage Language { get; } = SdkLanguage.Java;

    /// <inheritdoc/>
    public override bool IsCustomizedCodeUpdateSupported => true;

    private readonly ICopilotAgentRunner copilotAgentRunner;
    private readonly IMavenHelper mavenHelper;
    private readonly IPythonHelper pythonHelper;

    private const string CustomizationDirName = "customization";

    /// <summary>
    /// Initializes a new instance of the <see cref="JavaLanguageService"/> class.
    /// </summary>
    /// <param name="processHelper">Process execution helper.</param>
    /// <param name="gitHelper">Git helper.</param>
    /// <param name="mavenHelper">Maven command helper.</param>
    /// <param name="pythonHelper">Python command helper.</param>
    /// <param name="copilotAgentRunner">Copilot agent runner.</param>
    /// <param name="logger">Logger for language service operations.</param>
    /// <param name="commonValidationHelpers">Common validation helpers.</param>
    /// <param name="packageInfoHelper">Package information helper.</param>
    /// <param name="fileHelper">File helper.</param>
    /// <param name="specGenSdkConfigHelper">Spec-gen SDK config helper.</param>
    /// <param name="changelogHelper">Changelog helper.</param>
    public JavaLanguageService(
        IProcessHelper processHelper,
        IGitHelper gitHelper,
        IMavenHelper mavenHelper,
        IPythonHelper pythonHelper,
        ICopilotAgentRunner copilotAgentRunner,
        ILogger<LanguageService> logger,
        ICommonValidationHelpers commonValidationHelpers,
        IPackageInfoHelper packageInfoHelper,
        IFileHelper fileHelper,
        ISpecGenSdkConfigHelper specGenSdkConfigHelper,
        IChangelogHelper changelogHelper)
        : base(processHelper, gitHelper, logger, commonValidationHelpers, packageInfoHelper, fileHelper, specGenSdkConfigHelper, changelogHelper)
    {
        this.copilotAgentRunner = copilotAgentRunner;
        this.mavenHelper = mavenHelper;
        this.pythonHelper = pythonHelper;
    }

    /// <summary>
    /// Resolves package metadata for a Java SDK package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A populated <see cref="PackageInfo"/> instance.</returns>
    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        logger.LogDebug("Resolving Java package info for path: {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = await packageInfoHelper.ParsePackagePathAsync(packagePath, ct);
        var (packageName, packageVersion) = await TryGetPackageInfoAsync(fullPath, ct);

        if (packageName == null)
        {
            logger.LogWarning("Could not determine package name for Java package at {fullPath}", fullPath);
        }
        if (packageVersion == null)
        {
            logger.LogWarning("Could not determine package version for Java package at {fullPath}", fullPath);
        }

        var sdkType = DetermineSdkType(packageName);

        var model = new PackageInfo
        {
            PackagePath = fullPath,
            RepoRoot = repoRoot,
            RelativePath = relativePath,
            PackageName = packageName,
            PackageVersion = packageVersion,
            ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
            Language = SdkLanguage.Java,
            SamplesDirectory = BuildSamplesDirectory(fullPath),
            SdkType = sdkType
        };

        logger.LogDebug("Resolved Java package: {packageName} v{packageVersion} at {relativePath}",
            packageName ?? "(unknown)", packageVersion ?? "(unknown)", relativePath);

        return model;
    }

    private string BuildSamplesDirectory(string packagePath)
    {
        var moduleName = TryGetJavaModuleName(packagePath);
        if (!string.IsNullOrWhiteSpace(moduleName))
        {
            var modulePath = moduleName!.Replace('.', Path.DirectorySeparatorChar);
            var samplesDir = Path.Combine(packagePath, "src", "samples", "java", modulePath);
            logger.LogTrace("Built samples directory with module name: {samplesDir}", samplesDir);
            return samplesDir;
        }
        var defaultDir = Path.Combine(packagePath, "src", "samples", "java");
        logger.LogTrace("Built default samples directory: {defaultDir}", defaultDir);
        return defaultDir;
    }

    private async Task<(string? Name, string? Version)> TryGetPackageInfoAsync(string packagePath, CancellationToken ct)
    {
        if (!TryGetPomFilePath(packagePath, out var path))
        {
            return (null, null);
        }

        var metadataResult = await TryReadPomMetadataAsync(path, ct);
        if (!metadataResult.Success)
        {
            logger.LogWarning("{Error} at {path}", metadataResult.Error, path);
            return (null, null);
        }

        var metadata = metadataResult.Metadata!;
        logger.LogTrace("Found artifactId: {artifactId}", metadata.ArtifactId);

        if (string.IsNullOrWhiteSpace(metadata.Version))
        {
            logger.LogWarning("No version found in pom.xml at {path}", path);
        }
        else
        {
            logger.LogTrace("Found version: {version}", metadata.Version);
        }

        return (metadata.ArtifactId, metadata.Version);
    }

    private bool TryGetPomFilePath(string packagePath, out string pomPath)
    {
        pomPath = Path.Combine(packagePath, "pom.xml");
        if (File.Exists(pomPath))
        {
            return true;
        }

        logger.LogWarning("No pom.xml file found at {PomPath}", pomPath);
        return false;
    }

    private string? TryGetJavaModuleName(string packagePath)
    {
        try
        {
            var moduleInfoPath = Path.Combine(packagePath, "src", "main", "java", "module-info.java");
            if (!File.Exists(moduleInfoPath))
            {
                logger.LogTrace("No module-info.java found at {moduleInfoPath}", moduleInfoPath);
                return null;
            }

            logger.LogTrace("Reading module-info.java from {moduleInfoPath}", moduleInfoPath);
            var content = File.ReadAllText(moduleInfoPath);
            var match = JavaModuleDeclarationRegex().Match(content);
            if (match.Success)
            {
                var moduleName = match.Groups[1].Value.Trim();
                logger.LogTrace("Found Java module name: {moduleName}", moduleName);
                return moduleName;
            }
            logger.LogTrace("Could not parse module name from module-info.java");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error reading Java module name from {packagePath}", packagePath);
            return null;
        }
    }

    /// <summary>
    /// Determines the SDK type based on the artifact ID following the logic from the PowerShell script Language-Settings.ps1.
    /// </summary>
    /// <param name="artifactId">The artifact ID (package name) to analyze</param>
    /// <returns>The determined SDK type</returns>
    private static SdkType DetermineSdkType(string? artifactId)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            return SdkType.Unknown;
        }

        // Following the logic from azure-sdk-for-java/eng/scripts/Language-Settings.ps1
        if (artifactId.Contains("mgmt", StringComparison.OrdinalIgnoreCase) ||
            artifactId.Contains("resourcemanager", StringComparison.OrdinalIgnoreCase))
        {
            return SdkType.Management;
        }

        if (artifactId.Contains("spring", StringComparison.OrdinalIgnoreCase))
        {
            return SdkType.Spring;
        }

        // Default case - client SDKs
        return SdkType.Dataplane;
    }

    /// <summary>
    /// Matches Java module declarations like "module com.azure.storage.blob {"
    /// </summary>
    [GeneratedRegex(@"^\s*module\s+([^\{\s]+)\s*\{", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex JavaModuleDeclarationRegex();

    private static readonly TimeSpan testTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan versioningScriptTimeout = TimeSpan.FromMinutes(5);

    private sealed record ScriptUpdateResult(bool ScriptsAvailable, bool Success, string? Message)
    {
        public static ScriptUpdateResult NotAvailable(string message) => new(false, false, message);
        public static ScriptUpdateResult Failed(string message) => new(true, false, message);
        public static ScriptUpdateResult Succeeded(string message) => new(true, true, message);
    }

    private sealed record PomMetadata(string ArtifactId, string? Version, string GroupId);

    /// <summary>
    /// Runs all Maven tests for the specified Java package.
    /// </summary>
    /// <param name="packagePath">Path to the package directory.</param>
    /// <param name="testMode">The test mode to use.</param>
    /// <param name="liveTestEnvironment">Optional environment variables for live/record tests.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="TestRunResponse"/> containing test execution details.</returns>
    public override async Task<TestRunResponse> RunAllTests(string packagePath, TestMode testMode = TestMode.Playback, IDictionary<string, string>? liveTestEnvironment = null, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        logger.LogInformation("Starting test execution for Java project at: {PackagePath} in {TestMode} mode", packagePath, testMode);

        if (!TryGetPomFilePath(packagePath, out var pomPath))
        {
            logger.LogError("Cannot run tests - no pom.xml found at {PackagePath}", packagePath);
            return new TestRunResponse(
                exitCode: 1,
                testRunOutput: $"No pom.xml file found at {pomPath}. Cannot run tests.",
                error: "pom.xml not found");
        }

        var envVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AZURE_TEST_MODE"] = testMode.ToString().ToUpperInvariant()
        };

        if (liveTestEnvironment != null)
        {
            foreach (var (key, value) in liveTestEnvironment)
            {
                envVars[key] = value;
            }
        }

        // Use caller-provided timeout if specified, otherwise use mode-based defaults
        timeout ??= testMode == TestMode.Playback
            ? testTimeout
            : TimeSpan.FromMinutes(15);

        var result = await mavenHelper.Run(
            new("test", ["--no-transfer-progress"], pomPath, workingDirectory: packagePath, timeout: timeout, environmentVariables: envVars), ct);

        var response = new TestRunResponse(result);

        // After successful record mode, push test assets to the assets repo
        if (testMode == TestMode.Record && result.ExitCode == 0)
        {
            await PushTestAssets(packagePath, response, ct);
        }

        return response;
    }

    public override async Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo, string? ArtifactPath)> PackAsync(
        string packagePath, string? outputPath = null, int timeoutMinutes = 30, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Packing Java SDK project at: {PackagePath}", packagePath);

            if (string.IsNullOrWhiteSpace(packagePath))
            {
                return (false, "Package path is required and cannot be empty.", null, null);
            }

            string fullPath = Path.GetFullPath(packagePath);
            if (!Directory.Exists(fullPath))
            {
                return (false, $"Package path does not exist: {fullPath}", null, null);
            }

            var packageInfo = await GetPackageInfo(fullPath, ct);

            var pomPath = Path.Combine(fullPath, "pom.xml");
            var args = new List<string>
            {
                "package"
            };

            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                args.Add($"-DpackageOutputDirectory={outputPath}");
            }

            var result = await mavenHelper.Run(
                new("clean", args.ToArray(), pomPath, workingDirectory: fullPath, timeout: TimeSpan.FromMinutes(timeoutMinutes)),
                ct
            );

            if (result.ExitCode != 0)
            {
                var errorMessage = $"mvn clean package failed with exit code {result.ExitCode}. Output:\n{result.Output}";
                logger.LogError("{ErrorMessage}", errorMessage);
                return (false, errorMessage, packageInfo, null);
            }

            // Try to find artifact path in the output directory
            var artifactDir = outputPath ?? Path.Combine(fullPath, "target");
            string? artifactPath = null;
            if (Directory.Exists(artifactDir))
            {
                var jarFiles = Directory.GetFiles(artifactDir, "*.jar", SearchOption.TopDirectoryOnly)
                    .Where(f => !f.EndsWith("-sources.jar") && !f.EndsWith("-javadoc.jar") && !f.EndsWith("-tests.jar"))
                    .ToArray();
                if (jarFiles.Length > 0)
                {
                    artifactPath = jarFiles.OrderByDescending(File.GetLastWriteTimeUtc).First();
                }
            }

            logger.LogInformation("Pack completed successfully. Artifact: {ArtifactPath}", artifactPath ?? "(unknown)");
            return (true, null, packageInfo, artifactPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while packing Java SDK");
            return (false, $"An error occurred: {ex.Message}", null, null);
        }
    }

    public override Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath, CancellationToken ct)
    {
        // TODO: implement file-level diff between oldGenerationPath and newGenerationPath.
        return Task.FromResult(new List<ApiChange>());
    }

    public override string? HasCustomizations(string packagePath, CancellationToken ct = default)
    {
        // In azure-sdk-for-java layout, customizations live under:
        //   <pkgRoot>/azure-<package>-<service>/customization/src/main/java
        var customizationSourceRoot = Path.Combine(packagePath, CustomizationDirName, "src", "main", "java");
        return Directory.Exists(customizationSourceRoot) ? customizationSourceRoot : null;
    }

    /// <summary>
    /// Applies patches to customization files based on build errors.
    /// This is a mechanical worker - the Classifier does the thinking and routing.
    /// </summary>
    public override async Task<List<AppliedPatch>> ApplyPatchesAsync(
        string customizationRoot,
        string packagePath,
        string buildContext,
        CancellationToken ct)
    {
        try
        {
            if (!Directory.Exists(customizationRoot))
            {
                logger.LogDebug("Customization root does not exist: {Root}", customizationRoot);
                return [];
            }

            // Get the list of customization files
            var javaFiles = Directory.GetFiles(customizationRoot, "*.java", SearchOption.AllDirectories);

            // Collect patches directly from the tool as they succeed
            var patchLog = new ConcurrentBag<AppliedPatch>();

            // Build both relative-path lists in a single pass over javaFiles:
            //  - customizationFiles: relative to packagePath (for the ReadFile tool)
            //  - patchFilePaths:     relative to customizationRoot (for the CodePatch tool)
            var customizationFiles = new List<string>(javaFiles.Length);
            var patchFilePaths = new List<string>(javaFiles.Length);
            foreach (var f in javaFiles)
            {
                customizationFiles.Add(Path.GetRelativePath(packagePath, f));
                patchFilePaths.Add(Path.GetRelativePath(customizationRoot, f));
            }

            // Build error-driven prompt for patch agent
            var prompt = new JavaErrorDrivenPatchTemplate(
                buildContext,
                packagePath,
                customizationRoot,
                customizationFiles,
                patchFilePaths).BuildPrompt();

            // Single-pass agent: applies all patches it can in one run
            var agentDefinition = new CopilotAgent<string>
            {
                Instructions = prompt,
                MaxIterations = 10,
                Tools =
                [
                    FileTools.CreateReadFileTool(packagePath, includeLineNumbers: true,
                        description: "Read files from the package directory (generated code, customization files, etc.). Use startLine/endLine to read specific sections of large files."),
                    FileTools.CreateGrepSearchTool(packagePath,
                        description: "Search for text or regex patterns in files. Use this to find specific symbols or references without reading entire files."),
                    CodePatchTools.CreateCodePatchTool(customizationRoot,
                        description: "Apply code patches to customization files only (never generated code)",
                        onPatchApplied: patchLog.Add)
                ]
            };

            // Run the agent to apply patches
            try
            {
                await copilotAgentRunner.RunAsync(agentDefinition, ct);
            }
            catch (OperationCanceledException)
            {
                // Cancelled externally, re-throw
                throw;
            }
            catch (Exception agentEx)
            {
                // Agent exhausted its iteration budget without completing.
                logger.LogDebug(agentEx, "CopilotAgent terminated early");
            }

            // The patchLog was populated directly by the tool on each successful patch
            var appliedPatches = patchLog.ToList();

            logger.LogInformation("Patch application completed, patches applied: {PatchCount}", appliedPatches.Count);
            return appliedPatches;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply patches");
            return [];
        }
    }

    /// <summary>
    /// Updates Java package versions using the Java repository versioning scripts.
    /// Follows SetPackageVersion from azure-sdk-for-java/eng/scripts/Language-Settings.ps1,
    /// excluding changelog updates because changelog changes are handled by the base language workflow.
    /// </summary>
    protected override async Task<PackageOperationResponse> UpdatePackageVersionInFilesAsync(
        string packagePath,
        string version,
        string? releaseType,
        CancellationToken ct)
    {
        logger.LogInformation("Updating Java package version to {Version} in {PackagePath}", version, packagePath);

        try
        {
            if (!TryGetPomFilePath(packagePath, out var pomPath))
            {
                return PackageOperationResponse.CreateFailure(
                    $"No pom.xml file found at {pomPath}",
                    nextSteps: ["Ensure the package path contains a valid Maven project with pom.xml"]);
            }

            var scriptResult = await TryUpdateVersionUsingScriptsAsync(packagePath, pomPath, version, ct);

            if (!scriptResult.ScriptsAvailable || !scriptResult.Success)
            {
                return PackageOperationResponse.CreateFailure(
                    scriptResult.Message ?? "Failed to run Java versioning scripts.",
                    nextSteps:
                    [
                        "Run 'azsdk verify setup' to verify Python is available",
                        "Ensure eng/versioning/set_versions.py and eng/versioning/update_versions.py exist",
                        "Run scripts manually and verify version propagation"
                    ]);
            }

            return PackageOperationResponse.CreateSuccess(
                $"Version updated to {version} via Java versioning scripts.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update Java package version");
            return PackageOperationResponse.CreateFailure(
                $"Failed to update version: {ex.Message}",
                nextSteps: ["Check the pom.xml file format", "Ensure the file is not locked by another process"]);
        }
    }

    /// <summary>
    /// Attempts to run Java versioning scripts to update version source files and propagate version references.
    /// </summary>
    /// <param name="packagePath">Path to the Java package directory.</param>
    /// <param name="pomPath">Path to the package pom.xml.</param>
    /// <param name="version">Target version string.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ScriptUpdateResult"/> describing script availability, outcome, and details.</returns>
    private async Task<ScriptUpdateResult> TryUpdateVersionUsingScriptsAsync(
        string packagePath,
        string pomPath,
        string version,
        CancellationToken ct)
    {
        string repoRoot;
        try
        {
            repoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to discover repo root for {PackagePath}; script propagation skipped", packagePath);
            return ScriptUpdateResult.NotAvailable("Failed to discover repository root for Java versioning scripts.");
        }

        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            logger.LogDebug("Repo root not found for {PackagePath}; script propagation skipped", packagePath);
            return ScriptUpdateResult.NotAvailable("Repository root was not found for Java versioning scripts.");
        }

        var setVersionsScriptPath = Path.Combine(repoRoot, "eng", "versioning", "set_versions.py");
        var updateVersionsScriptPath = Path.Combine(repoRoot, "eng", "versioning", "update_versions.py");
        if (!File.Exists(setVersionsScriptPath) || !File.Exists(updateVersionsScriptPath))
        {
            logger.LogDebug("Java versioning scripts not found under repo root {RepoRoot}; script propagation skipped", repoRoot);
            return ScriptUpdateResult.NotAvailable("Java versioning scripts were not found under eng/versioning.");
        }

        var metadataResult = await TryReadPomMetadataAsync(pomPath, ct);
        if (!metadataResult.Success)
        {
            return ScriptUpdateResult.Failed(metadataResult.Error);
        }

        var metadata = metadataResult.Metadata!;
        if (string.IsNullOrWhiteSpace(metadata.GroupId))
        {
            return ScriptUpdateResult.Failed("No groupId found in pom.xml");
        }

        var groupId = metadata.GroupId;
        var artifactId = metadata.ArtifactId;
        var fullLibraryName = $"{groupId}:{artifactId}";
        logger.LogInformation("Running Java versioning scripts for {LibraryName}", fullLibraryName);

        var setVersionsResult = await pythonHelper.Run(new PythonOptions(
            executableName: "python",
            args:
            [
                setVersionsScriptPath,
                "--new-version", version,
                "--artifact-id", artifactId,
                "--group-id", groupId
            ],
            workingDirectory: repoRoot,
            timeout: versioningScriptTimeout), ct);

        if (setVersionsResult.ExitCode != 0)
        {
            logger.LogError("set_versions.py failed for {LibraryName} with exit code {ExitCode}", fullLibraryName, setVersionsResult.ExitCode);
            return ScriptUpdateResult.Failed($"set_versions.py failed: {setVersionsResult.Output}");
        }

        var updateVersionsResult = await pythonHelper.Run(new PythonOptions(
            executableName: "python",
            args:
            [
                updateVersionsScriptPath,
                "--library-list", fullLibraryName
            ],
            workingDirectory: repoRoot,
            timeout: versioningScriptTimeout), ct);

        if (updateVersionsResult.ExitCode != 0)
        {
            logger.LogError("update_versions.py failed for {LibraryName} with exit code {ExitCode}", fullLibraryName, updateVersionsResult.ExitCode);
            return ScriptUpdateResult.Failed($"update_versions.py failed: {updateVersionsResult.Output}");
        }

        return ScriptUpdateResult.Succeeded($"Version updated to {version} via Java versioning scripts for {fullLibraryName}.");
    }

    private static async Task<(bool Success, PomMetadata? Metadata, string Error)> TryReadPomMetadataAsync(
        string pomPath,
        CancellationToken ct)
    {
        try
        {
            using var stream = File.OpenRead(pomPath);
            var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
            var root = doc.Root;
            if (root == null)
            {
                return (false, null, "pom.xml has no root element");
            }

            var ns = root.Name.Namespace;

            var artifactId = root.Element(ns + "artifactId")?.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(artifactId))
            {
                return (false, null, "No artifactId found in pom.xml");
            }

            var version = root.Element(ns + "version")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(version))
            {
                var parent = root.Element(ns + "parent");
                version = parent?.Element(ns + "version")?.Value?.Trim();
            }

            var groupId = root.Element(ns + "groupId")?.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(groupId))
            {
                var parent = root.Element(ns + "parent");
                groupId = parent?.Element(ns + "groupId")?.Value?.Trim() ?? string.Empty;
            }

            var metadata = new PomMetadata(
                artifactId,
                string.IsNullOrWhiteSpace(version) ? null : version,
                groupId);

            return (true, metadata, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, null, $"Failed to parse pom.xml: {ex.Message}");
        }
    }
}
