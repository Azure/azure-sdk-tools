// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Concurrent;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.CopilotAgents.Tools;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Prompts.Templates;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

public sealed partial class JavaScriptLanguageService : LanguageService
{
    private const string GeneratedFolderName = "generated";
    private readonly INpxHelper npxHelper;
    private readonly ICopilotAgentRunner copilotAgentRunner;

    public JavaScriptLanguageService(
        IProcessHelper processHelper,
        INpxHelper npxHelper,
        ICopilotAgentRunner copilotAgentRunner,
        IGitHelper gitHelper,
        ILogger<LanguageService> logger,
        ICommonValidationHelpers commonValidationHelpers,
        IPackageInfoHelper packageInfoHelper,
        IFileHelper fileHelper,
        ISpecGenSdkConfigHelper specGenSdkConfigHelper,
        IChangelogHelper changelogHelper)
        : base(processHelper, gitHelper, logger, commonValidationHelpers, packageInfoHelper, fileHelper, specGenSdkConfigHelper, changelogHelper)
    {
        this.npxHelper = npxHelper;
        this.copilotAgentRunner = copilotAgentRunner;
    }
    public override SdkLanguage Language { get; } = SdkLanguage.JavaScript;
    public override bool IsCustomizedCodeUpdateSupported => true;

    /// <summary>
    /// JavaScript packages are identified by package.json files.
    /// </summary>
    protected override string[] PackageManifestPatterns => ["package.json"];

    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        logger.LogDebug("Resolving JavaScript package info for path: {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = await packageInfoHelper.ParsePackagePathAsync(packagePath, ct);
        var (packageName, packageVersion, sdkType) = await TryGetPackageInfoAsync(fullPath, ct);

        if (packageName == null)
        {
            logger.LogWarning("Could not determine package name for JavaScript package at {fullPath}", fullPath);
        }
        if (packageVersion == null)
        {
            logger.LogWarning("Could not determine package version for JavaScript package at {fullPath}", fullPath);
        }
        if (sdkType == SdkType.Unknown)
        {
            logger.LogWarning("Could not determine SDK type for JavaScript package at {fullPath}", fullPath);
        }

        var model = new PackageInfo
        {
            PackagePath = fullPath,
            RepoRoot = repoRoot,
            RelativePath = relativePath,
            PackageName = packageName,
            PackageVersion = packageVersion,
            SdkType = sdkType,
            ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
            Language = SdkLanguage.JavaScript,
            SamplesDirectory = Path.Combine(fullPath, "samples-dev")
        };

        logger.LogDebug("Resolved JavaScript package: {packageName} v{packageVersion} (type {sdkType}) at {relativePath}",
            packageName ?? "(unknown)", packageVersion ?? "(unknown)", sdkType, relativePath);

        return model;
    }

    private async Task<(string? Name, string? Version, SdkType sdkType)> TryGetPackageInfoAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            var path = Path.Combine(packagePath, "package.json");
            if (!File.Exists(path))
            {
                logger.LogWarning("No package.json file found at {path}", path);
                return (null, null, SdkType.Unknown);
            }

            logger.LogTrace("Reading package.json from {path}", path);
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            }, ct);

            string? name = null;
            string? version = null;
            SdkType sdkType = SdkType.Unknown;

            if (doc.RootElement.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
            {
                var nameValue = nameProp.GetString();
                if (!string.IsNullOrWhiteSpace(nameValue))
                {
                    name = nameValue;
                    logger.LogTrace("Found package name: {name}", name);
                }
            }
            else
            {
                logger.LogTrace("No name property found in package.json");
            }

            if (doc.RootElement.TryGetProperty("version", out var versionProp) && versionProp.ValueKind == JsonValueKind.String)
            {
                var versionValue = versionProp.GetString();
                if (!string.IsNullOrWhiteSpace(versionValue))
                {
                    version = versionValue;
                    logger.LogTrace("Found version: {version}", version);
                }
            }
            else
            {
                logger.LogTrace("No version property found in package.json");
            }

            if (doc.RootElement.TryGetProperty("sdk-type", out var sdkTypeProp) && sdkTypeProp.ValueKind == JsonValueKind.String)
            {
                var sdkTypeValue = sdkTypeProp.GetString();
                if (!string.IsNullOrWhiteSpace(sdkTypeValue))
                {
                    sdkType = sdkTypeValue switch
                    {
                        "client" => SdkType.Dataplane,
                        "mgmt" => SdkType.Management,
                        _ => SdkType.Unknown,
                    };
                    logger.LogTrace("Found SDK type: {sdkType}", sdkType);
                }
            }
            else
            {
                logger.LogTrace("No sdk-type property found in package.json");
            }

            return (name, version, sdkType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading JavaScript package info from {packagePath}", packagePath);
            return (null, null, SdkType.Unknown);
        }
    }

    public override async Task<TestRunResponse> RunAllTests(string packagePath, TestMode testMode = TestMode.Playback, IDictionary<string, string>? liveTestEnvironment = null, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var envVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TEST_MODE"] = testMode.ToString().ToLowerInvariant()
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
            ? ProcessOptions.DEFAULT_PROCESS_TIMEOUT
            : TimeSpan.FromMinutes(10);

        var result = await processHelper.Run(new ProcessOptions(
                command: "npm",
                args: ["run", "test"],
                workingDirectory: packagePath,
                timeout: timeout,
                environmentVariables: envVars
            ),
            ct
        );

        var response = new TestRunResponse(result);

        // After successful record mode, push test assets to the assets repo
        if (testMode == TestMode.Record && result.ExitCode == 0)
        {
            await PushTestAssets(packagePath, response, ct);
        }

        return response;
    }

    protected override async Task PushTestAssets(string packagePath, TestRunResponse response, CancellationToken ct)
    {
        var assetsJsonPath = Path.Combine(packagePath, "assets.json");
        if (!File.Exists(assetsJsonPath))
        {
            logger.LogInformation("No assets.json found in {packagePath}, skipping asset push", packagePath);
            return;
        }

        logger.LogInformation("Pushing recorded test assets for {packagePath}", packagePath);

        try
        {
            var pushResult = await npxHelper.Run(new NpxOptions(
                    package: null,
                    args: ["dev-tool", "test-proxy", "push", "-a", "assets.json"],
                    workingDirectory: packagePath
                ),
                ct
            );

            if (pushResult.ExitCode == 0)
            {
                logger.LogInformation("Successfully pushed test assets");
            }
            else
            {
                logger.LogWarning("Asset push failed with exit code {exitCode}: {output}", pushResult.ExitCode, pushResult.Output);
                response.NextSteps ??= [];
                response.NextSteps.Add($"Asset push failed (exit code {pushResult.ExitCode}). You may need to push assets manually using 'npx dev-tool test-proxy push -a assets.json'");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push test assets. Is test-proxy installed?");
            response.NextSteps ??= [];
            response.NextSteps.Add("Could not push test assets automatically. Ensure @azure-tools/dev-tool is available and try running 'npx dev-tool test-proxy push -a assets.json' manually");
        }
    }

    public override async Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo, string? ArtifactPath)> PackAsync(
        string packagePath, string? outputPath = null, int timeoutMinutes = 30, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Packing JavaScript SDK project at: {PackagePath}", packagePath);

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

            var args = new List<string> { "pack" };
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                args.AddRange(["--pack-destination", outputPath]);
            }

            var result = await processHelper.Run(new ProcessOptions(
                    command: "pnpm",
                    args: args.ToArray(),
                    workingDirectory: fullPath,
                    timeout: TimeSpan.FromMinutes(timeoutMinutes)
                ),
                ct
            );

            if (result.ExitCode != 0)
            {
                var errorMessage = $"pnpm pack failed with exit code {result.ExitCode}. Output:\n{result.Output}";
                logger.LogError("{ErrorMessage}", errorMessage);
                return (false, errorMessage, packageInfo, null);
            }

            // pnpm pack outputs the tarball filename to stdout
            var artifactPath = ExtractTarballPath(result.Output, fullPath, outputPath);
            logger.LogInformation("Pack completed successfully. Artifact: {ArtifactPath}", artifactPath ?? "(unknown)");
            return (true, null, packageInfo, artifactPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while packing JavaScript SDK");
            return (false, $"An error occurred: {ex.Message}", null, null);
        }
    }

    /// <summary>
    /// Attempts to extract the .tgz file path from pnpm pack output.
    /// </summary>
    private static string? ExtractTarballPath(string output, string packagePath, string? outputPath)
    {
        // pnpm pack typically outputs the tarball filename
        var lines = output.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            {
                var dir = outputPath ?? packagePath;
                var candidatePath = Path.Combine(dir, trimmed);
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
                // The line itself might be a full path
                if (File.Exists(trimmed))
                {
                    return trimmed;
                }
            }
        }

        // Fallback: look for .tgz files in the directory
        var searchDir = outputPath ?? packagePath;
        if (Directory.Exists(searchDir))
        {
            var tgzFiles = Directory.GetFiles(searchDir, "*.tgz", SearchOption.TopDirectoryOnly);
            if (tgzFiles.Length > 0)
            {
                return tgzFiles.OrderByDescending(File.GetLastWriteTimeUtc).First();
            }
        }

        return null;
    }

    public override string? HasCustomizations(string packagePath, CancellationToken ct)
    {
        // In azure-sdk-for-js, the presence of a "generated" folder at the same level
        // as package.json indicates the package has customizations (code outside generated/).

        try
        {
            var generatedFolder = Path.Combine(packagePath, GeneratedFolderName);
            if (Directory.Exists(generatedFolder))
            {
                // If generated folder exists, customizations are everything outside it
                var srcDir = Path.Combine(packagePath, "src");
                if (Directory.Exists(srcDir))
                {
                    logger.LogDebug("Found JavaScript customization root at {SrcDir}", srcDir);
                    return srcDir;
                }
                // Fall back to package path if no src folder
                return packagePath;
            }

            logger.LogDebug("No JavaScript generated folder found in {PackagePath}", packagePath);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error searching for JavaScript customization files in {PackagePath}", packagePath);
            return null;
        }
    }

    /// <summary>
    /// Applies patches to customization files in <c>src/</c> based on build errors.
    /// Handles TypeScript compiler errors and merge conflict markers left by
    /// <c>dev-tool customization apply</c>.
    /// </summary>
    public override async Task<List<AppliedPatch>> ApplyPatchesAsync(
        string customizationRoot,
        string packagePath,
        string buildContext,
        CancellationToken ct)
    {
        try
        {
            // Always normalize to src/ - we only patch customization files there, never generated/
            customizationRoot = Path.Combine(packagePath, "src");

            if (!Directory.Exists(customizationRoot))
            {
                logger.LogDebug("Customization root does not exist: {Root}", customizationRoot);
                return [];
            }

            var generatedDirectorySegment = Path.DirectorySeparatorChar + GeneratedFolderName + Path.DirectorySeparatorChar;
            var nodeModulesDirectorySegment = Path.DirectorySeparatorChar + "node_modules" + Path.DirectorySeparatorChar;

            // Collect TypeScript and JavaScript files in src/ only, excluding generated/ and node_modules/
            string[] jsExtensions = ["*.ts", "*.tsx", "*.mts", "*.cts", "*.js", "*.jsx", "*.mjs", "*.cjs"];
            var tsFiles = jsExtensions
                .SelectMany(ext => Directory.GetFiles(customizationRoot, ext, SearchOption.AllDirectories))
                .Where(f => !f.Contains(nodeModulesDirectorySegment) && !f.Contains(generatedDirectorySegment))
                .Distinct()
                .ToArray();

            if (tsFiles.Length == 0)
            {
                logger.LogDebug("No TypeScript/JavaScript files found in customization root: {Root}", customizationRoot);
                return [];
            }

            var patchLog = new ConcurrentBag<AppliedPatch>();

            var readFilePaths = tsFiles.Select(f => Path.GetRelativePath(packagePath, f)).ToList();
            var patchFilePaths = tsFiles.Select(f => Path.GetRelativePath(customizationRoot, f)).ToList();

            var prompt = new JavaScriptErrorDrivenPatchTemplate(
                buildContext, packagePath, customizationRoot, readFilePaths, patchFilePaths).BuildPrompt();

            var agent = new CopilotAgent<string>
            {
                Instructions = prompt,
                MaxIterations = 25,
                Tools =
                [
                    FileTools.CreateGrepSearchTool(packagePath,
                        description: "Search for text or regex patterns in files. Use this to find specific symbols or references without reading entire files."),
                    FileTools.CreateReadFileTool(packagePath, includeLineNumbers: true,
                        description: "Read files from the package directory (generated code, src/ customization files, etc.)"),
                    CodePatchTools.CreateCodePatchTool(customizationRoot,
                        description: "Apply code patches to src/ customization files only (never generated/ files)",
                        onPatchApplied: patchLog.Add)
                ]
            };

            try
            {
                await copilotAgentRunner.RunAsync(agent, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception agentEx)
            {
                logger.LogDebug(agentEx, "CopilotAgent terminated early");
            }

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
