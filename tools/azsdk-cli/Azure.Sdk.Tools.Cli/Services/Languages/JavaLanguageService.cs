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

public sealed partial class JavaLanguageService : LanguageService
{
    public override SdkLanguage Language { get; } = SdkLanguage.Java;
    public override bool IsCustomizedCodeUpdateSupported => true;

    /// <summary>
    /// Java packages are identified by pom.xml files.
    /// </summary>
    protected override string[] PackageManifestPatterns => ["pom.xml"];

    private readonly ICopilotAgentRunner copilotAgentRunner;
    private readonly IMavenHelper mavenHelper;

    private const string CustomizationDirName = "customization";

    public JavaLanguageService(
        IProcessHelper processHelper,
        IGitHelper gitHelper,
        IMavenHelper mavenHelper,
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
    }

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
        var path = Path.Combine(packagePath, "pom.xml");
        if (!File.Exists(path))
        {
            logger.LogWarning("No pom.xml file found at {path}", path);
            return (null, null);
        }
        try
        {
            logger.LogTrace("Reading pom.xml file: {path}", path);
            using var stream = File.OpenRead(path);
            var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
            var root = doc.Root;
            if (root == null)
            {
                logger.LogWarning("pom.xml has no root element at {path}", path);
                return (null, null);
            }
            // Maven POM uses a default namespace; capture it to access elements.
            var ns = root.Name.Namespace;

            // Extract artifactId
            string? artifactId = root.Element(ns + "artifactId")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(artifactId))
            {
                logger.LogWarning("No artifactId found in pom.xml at {path}", path);
                artifactId = null;
            }
            else
            {
                logger.LogTrace("Found artifactId: {artifactId}", artifactId);
            }

            // Extract version
            string? version = root.Element(ns + "version")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(version))
            {
                // Fallback to parent version if project version not declared directly.
                var parent = root.Element(ns + "parent");
                version = parent?.Element(ns + "version")?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(version))
                {
                    logger.LogTrace("Found version from parent: {version}", version);
                }
            }
            else
            {
                logger.LogTrace("Found version: {version}", version);
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                logger.LogWarning("No version found in pom.xml at {path}", path);
                version = null;
            }

            return (artifactId, version);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading Java package info from {path}", path);
            return (null, null);
        }
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

    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(5);

    public override async Task<TestRunResponse> RunAllTests(string packagePath, CancellationToken ct = default)
    {
        logger.LogInformation("Starting test execution for Java project at: {PackagePath}", packagePath);

        // Run Maven tests using consistent command pattern
        var pomPath = Path.Combine(packagePath, "pom.xml");
        var result = await mavenHelper.Run(new("test", ["--no-transfer-progress"], pomPath, workingDirectory: packagePath, timeout: TestTimeout), ct);

        if (result.ExitCode == 0)
        {
            logger.LogInformation("Test execution completed successfully");
            return new TestRunResponse(result);
        }
        else
        {
            logger.LogWarning("Test execution failed with exit code {ExitCode}", result.ExitCode);
            return new TestRunResponse(result);
        }

    }

    public override Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath)
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
        string buildError,
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
                buildError,
                packagePath,
                customizationRoot,
                customizationFiles,
                patchFilePaths).BuildPrompt();

            // Single-pass agent: applies all patches it can in one run
            var agentDefinition = new CopilotAgent<string>
            {
                Instructions = prompt,
                MaxIterations = 25,
                Tools =
                [
                    FileTools.CreateReadFileTool(packagePath, includeLineNumbers: true,
                        description: "Read files from the package directory (generated code, customization files, etc.)"),
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

}
