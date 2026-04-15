// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.CopilotAgents.Tools;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using YamlDotNet.Serialization;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// Produces <see cref="PackageInfo"/> for .NET packages.
/// </summary>
public sealed partial class DotnetLanguageService: LanguageService
{
    private const string DotNetCommand = "dotnet";
    private const string RequiredDotNetVersion = "9.0.102"; // TODO - centralize this as part of env setup tool
    private const string GeneratedFolderName = "Generated";
    private static readonly TimeSpan CodeChecksTimeout = TimeSpan.FromMinutes(6);
    private static readonly TimeSpan AotCompatTimeout = TimeSpan.FromMinutes(5);

    private readonly IPowershellHelper powershellHelper;
    private readonly ICopilotAgentRunner copilotAgentRunner;

    public DotnetLanguageService(
        IProcessHelper processHelper,
        IPowershellHelper powershellHelper,
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
        this.powershellHelper = powershellHelper;
        this.copilotAgentRunner = copilotAgentRunner;
    }

    public override SdkLanguage Language { get; } = SdkLanguage.DotNet;
    public override bool IsCustomizedCodeUpdateSupported => true;

    // .NET packages have their main csproj in a src/ subdirectory
    protected override string[] PackageManifestPatterns => ["*.csproj"];

    protected override string? GetPackageRootFromManifest(string manifestPath)
    {
        // .NET layout: sdk/{service}/{folder}/src/{Name}.csproj
        // The folder name may differ from the package name (e.g., sdk/cognitiveservices/Knowledge.QnAMaker/).
        // We go up from src/ to return the package root directory.
        var directory = Path.GetDirectoryName(manifestPath);
        if (directory == null)
        {
            return null;
        }

        var dirName = Path.GetFileName(directory);

        // Only consider csproj files in src/ directories (skip tests/, perf/, samples/, stress/)
        if (!string.Equals(dirName, "src", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Path.GetDirectoryName(directory);
    }

    private static readonly string[] separator = new[] { "' '" };

    /// <summary>
    /// Gets the default samples directory path relative to the package path.
    /// </summary>
    /// <param name="packagePath">The package path</param>
    /// <returns>The default samples directory path</returns>
    private static string GetDefaultSamplesDirectory(string packagePath) => Path.Combine(packagePath, "tests", "samples");

    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        logger.LogDebug("Resolving .NET package info for path: {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = await packageInfoHelper.ParsePackagePathAsync(packagePath, ct);
        var (packageName, packageVersion, sdkType) = await TryGetPackageInfoAsync(fullPath, ct);

        if (packageName == null)
        {
            logger.LogWarning("Could not determine package name for .NET package at {fullPath}", fullPath);
        }
        if (packageVersion == null)
        {
            logger.LogWarning("Could not determine package version for .NET package at {fullPath}", fullPath);
        }
        if (sdkType == null)
        {
            logger.LogWarning("Could not determine SDK type for .NET package at {fullPath}", fullPath);
        }

        var samplesDirectory = FindSamplesDirectory(fullPath);

        var parsedSdkType = sdkType switch
        {
            "client" => SdkType.Dataplane,
            "mgmt" => SdkType.Management,
            "functions" => SdkType.Functions,
            _ => SdkType.Unknown
        };

        var model = new PackageInfo
        {
            PackagePath = fullPath,
            RepoRoot = repoRoot,
            RelativePath = relativePath,
            PackageName = packageName,
            PackageVersion = packageVersion,
            ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
            ServiceDirectory = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
            ArtifactName = packageName,
            Language = Models.SdkLanguage.DotNet,
            SamplesDirectory = samplesDirectory,
            SdkType = parsedSdkType
        };

        logger.LogDebug("Resolved .NET package: {packageName} v{packageVersion} at {relativePath} (as {parsedSdkType})",
            packageName ?? "(unknown)", packageVersion ?? "(unknown)", relativePath, parsedSdkType.ToString() ?? "(unknown)");

        return model;
    }

    private async Task<(string? Name, string? Version, string? SdkType)> TryGetPackageInfoAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            var csproj = Directory.GetFiles(Path.Combine(packagePath, "src"), "*.csproj").FirstOrDefault();

            if (csproj == null)
            {
                logger.LogWarning("No .csproj file found in {packagePath}", packagePath);
                return (null, null, null);
            }

            logger.LogTrace("Getting package info via MSBuild for: {csproj}", csproj);

            var result = await processHelper.Run(new ProcessOptions(
                command: "dotnet",
                args: ["msbuild", csproj, "-getTargetResult:GetPackageInfo", "-nologo"]
            ), ct);

            if (result == null || result.ExitCode != 0)
            {
                logger.LogTrace("MSBuild GetPackageInfo failed, returning null values");
                return (null, null, null);
            }

            // Parse JSON output
            using var jsonDoc = JsonDocument.Parse(result.Stdout);
            var targetResults = jsonDoc.RootElement.GetProperty("TargetResults");
            var getPackageInfo = targetResults.GetProperty("GetPackageInfo");
            var items = getPackageInfo.GetProperty("Items");

            // Identity field which contains the package info
            var identity = items[0].GetProperty("Identity").GetString();

            // Parse the identity string:  'pkgPath' 'serviceDir' 'pkgName' 'pkgVersion' 'sdkType' 'isNewSdk' 'dllFolder' 'AotCompatOptOut'
            var parts = identity?.Split(separator, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim('\'', ' '))
                .ToArray();

            if (parts?.Length >= 5) // for now we only need items in the first 5 positions
            {
                var name = parts[2]; // pkgName
                var version = parts[3]; // pkgVersion
                var sdkType = parts[4]; // sdkType

                // Validate we got actual values before returning
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(version))
                {
                    logger.LogTrace("Found package info via MSBuild: {name} v{version} ({sdkType})",
                        name, version, sdkType);

                    return (name, version, sdkType);
                }
            }

            logger.LogTrace("Unable to parse identity string, returning null values");
            return (null, null, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting .NET package info from {packagePath} using MSBuild GetPackageInfo target", packagePath);
            return (null, null, null);
        }
    }

    /// <summary>
    /// Finds the samples directory by looking for folders under tests that contain files with "#region Snippet:" in their content
    /// </summary>
    /// <param name="packagePath">The package path to search under</param>
    /// <returns>The path to the samples directory, or a default path if not found</returns>
    private string FindSamplesDirectory(string packagePath)
    {
        try
        {
            var testsPath = Path.Combine(packagePath, "tests");
            if (!Directory.Exists(testsPath))
            {
                logger.LogTrace("Tests directory not found at {testsPath}", testsPath);
                return GetDefaultSamplesDirectory(packagePath);
            }

            // Get all subdirectories under tests (sorted for consistent behavior across platforms)
            var testSubdirectories = Directory.GetDirectories(testsPath).OrderBy(d => d).ToArray();

            foreach (var directory in testSubdirectories)
            {
                // Look for .cs files containing "#region Snippet:" in their content
                var sampleFiles = Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly)
                    .Where(file =>
                    {
                        try
                        {
                            var content = File.ReadAllText(file);
                            return content.Contains("#region Snippet:", StringComparison.Ordinal);
                        }
                        catch
                        {
                            return false;
                        }
                    })
                    .ToArray();

                if (sampleFiles.Length > 0)
                {
                    logger.LogTrace("Found samples directory at {directory} with {count} files containing snippet regions",
                        directory, sampleFiles.Length);
                    return directory;
                }
            }

            logger.LogTrace("No samples directory found under {testsPath}, using default", testsPath);
            return GetDefaultSamplesDirectory(packagePath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error searching for samples directory under {packagePath}, using default", packagePath);
            return GetDefaultSamplesDirectory(packagePath);
        }
    }

    public override async Task<TestRunResponse> RunAllTests(string packagePath, TestMode testMode = TestMode.Playback, IDictionary<string, string>? liveTestEnvironment = null, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var testModeValue = testMode.ToString().ToLowerInvariant();
        var testFramework = DetectTestFramework(packagePath);

        var envVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Merge caller-provided environment first so that mode variables set below
        // always take precedence and cannot be silently overridden by a .env file.
        if (liveTestEnvironment != null)
        {
            foreach (var (key, value) in liveTestEnvironment)
            {
                envVars[key] = value;
            }
        }

        switch (testFramework)
        {
            case DotnetTestFramework.AzureCoreTestFramework:
                logger.LogInformation("Detected Azure.Core.TestFramework, setting AZURE_TEST_MODE={testMode}", testModeValue);
                envVars["AZURE_TEST_MODE"] = testModeValue;
                break;
            case DotnetTestFramework.ClientModelTestFramework:
                logger.LogInformation("Detected Microsoft.ClientModel.TestFramework, setting CLIENTMODEL_TEST_MODE={testMode}", testModeValue);
                envVars["CLIENTMODEL_TEST_MODE"] = testModeValue;
                break;
            default:
                // Could not determine — set both to be safe
                logger.LogInformation("Could not detect test framework, setting both AZURE_TEST_MODE and CLIENTMODEL_TEST_MODE={testMode}", testModeValue);
                envVars["AZURE_TEST_MODE"] = testModeValue;
                envVars["CLIENTMODEL_TEST_MODE"] = testModeValue;
                break;
        }

        // Use caller-provided timeout if specified, otherwise use mode-based defaults
        timeout ??= testMode == TestMode.Playback
            ? ProcessOptions.DEFAULT_PROCESS_TIMEOUT
            : TimeSpan.FromMinutes(10);

        var testsPath = Path.Combine(packagePath, "tests");
        var workingDirectory = Directory.Exists(testsPath) ? testsPath : packagePath;

        var result = await processHelper.Run(new ProcessOptions(
                command: "dotnet",
                args: ["test"],
                workingDirectory: workingDirectory,
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

    /// <summary>
    /// Detects which test framework a .NET test project uses by examining .csproj references.
    /// </summary>
    /// <remarks>
    /// Azure.Core.TestFramework is not a released NuGet package — it's referenced via ProjectReference
    /// in azure-sdk-for-net. The reference may be a direct path (e.g. ..\..\Azure.Core.TestFramework.csproj)
    /// or an MSBuild variable (e.g. $(AzureCoreTestFramework)).
    ///
    /// Microsoft.ClientModel.TestFramework is a released NuGet package referenced via PackageReference.
    /// </remarks>
    internal DotnetTestFramework DetectTestFramework(string packagePath)
    {
        try
        {
            var testsPath = Path.Combine(packagePath, "tests");
            var searchDir = Directory.Exists(testsPath) ? testsPath : packagePath;

            if (!Directory.Exists(searchDir))
            {
                logger.LogDebug("Directory {searchDir} does not exist, cannot detect test framework", searchDir);
                return DotnetTestFramework.Unknown;
            }

            var csprojFiles = Directory.GetFiles(searchDir, "*.csproj", SearchOption.AllDirectories);
            if (csprojFiles.Length == 0)
            {
                logger.LogDebug("No .csproj files found in {searchDir}, cannot detect test framework", searchDir);
                return DotnetTestFramework.Unknown;
            }

            foreach (var csprojFile in csprojFiles)
            {
                var doc = XDocument.Load(csprojFile);

                // Check ProjectReferences for Azure.Core.TestFramework
                // Handles both direct paths and MSBuild variables:
                //   <ProjectReference Include="..\..\Azure.Core.TestFramework\Azure.Core.TestFramework.csproj" />
                //   <ProjectReference Include="$(AzureCoreTestFramework)" />
                var projectRefs = doc.Descendants()
                    .Where(e => e.Name.LocalName == "ProjectReference")
                    .Select(e => e.Attribute("Include")?.Value ?? "");

                foreach (var refValue in projectRefs)
                {
                    if (refValue.Contains("Azure.Core.TestFramework", StringComparison.OrdinalIgnoreCase) ||
                        refValue.Contains("AzureCoreTestFramework", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogDebug("Found Azure.Core.TestFramework reference in {csproj}: {ref}", csprojFile, refValue);
                        return DotnetTestFramework.AzureCoreTestFramework;
                    }
                }

                // Check PackageReferences for Microsoft.ClientModel.TestFramework
                //   <PackageReference Include="Microsoft.ClientModel.TestFramework" />
                var packageRefs = doc.Descendants()
                    .Where(e => e.Name.LocalName == "PackageReference")
                    .Select(e => e.Attribute("Include")?.Value ?? "");

                foreach (var refValue in packageRefs)
                {
                    if (refValue.Equals("Microsoft.ClientModel.TestFramework", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogDebug("Found Microsoft.ClientModel.TestFramework reference in {csproj}: {ref}", csprojFile, refValue);
                        return DotnetTestFramework.ClientModelTestFramework;
                    }
                }
            }

            logger.LogDebug("No known test framework reference found in {searchDir}", searchDir);
            return DotnetTestFramework.Unknown;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to detect test framework from .csproj files in {packagePath}", packagePath);
            return DotnetTestFramework.Unknown;
        }
    }

    internal enum DotnetTestFramework
    {
        Unknown,
        AzureCoreTestFramework,
        ClientModelTestFramework
    }


    public override async Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo, string? ArtifactPath)> PackAsync(
        string packagePath, string? outputPath = null, int timeoutMinutes = 30, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Packing .NET SDK project at: {PackagePath}", packagePath);

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
                args.AddRange(["--output", outputPath]);
            }

            var result = await processHelper.Run(new ProcessOptions(
                    command: DotNetCommand,
                    args: args.ToArray(),
                    workingDirectory: fullPath,
                    timeout: TimeSpan.FromMinutes(timeoutMinutes)
                ),
                ct
            );

            if (result.ExitCode != 0)
            {
                var errorMessage = $"dotnet pack failed with exit code {result.ExitCode}. Output:\n{result.Output}";
                logger.LogError("{ErrorMessage}", errorMessage);
                return (false, errorMessage, packageInfo, null);
            }

            // Try to find the generated .nupkg path from the output
            var artifactPath = ExtractNupkgPath(result.Output, fullPath, outputPath);
            logger.LogInformation("Pack completed successfully. Artifact: {ArtifactPath}", artifactPath ?? "(unknown)");
            return (true, null, packageInfo, artifactPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while packing .NET SDK");
            return (false, $"An error occurred: {ex.Message}", null, null);
        }
    }

    /// <summary>
    /// Attempts to extract the .nupkg file path from dotnet pack output.
    /// </summary>
    private static string? ExtractNupkgPath(string output, string packagePath, string? outputPath)
    {
        // dotnet pack typically outputs a line like:
        // Successfully created package '/path/to/Package.1.0.0.nupkg'.
        var lines = output.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Contains(".nupkg", StringComparison.OrdinalIgnoreCase))
            {
                // Try to extract the path between single quotes
                var startIdx = trimmed.IndexOf('\'');
                var endIdx = trimmed.LastIndexOf('\'');
                if (startIdx >= 0 && endIdx > startIdx)
                {
                    return trimmed.Substring(startIdx + 1, endIdx - startIdx - 1);
                }
            }
        }

        // Fallback: look in the output directory for .nupkg files
        var searchDir = outputPath ?? Path.Combine(packagePath, "bin", "Release");
        if (Directory.Exists(searchDir))
        {
            var nupkgFiles = Directory.GetFiles(searchDir, "*.nupkg", SearchOption.TopDirectoryOnly);
            if (nupkgFiles.Length > 0)
            {
                return nupkgFiles.OrderByDescending(File.GetLastWriteTimeUtc).First();
            }
        }

        return null;
    }

    protected override void ApplyLanguageCiParameters(PackageInfo packageInfo)
    {
        var parameters = packageInfoHelper.GetLanguageCiParameters<DotnetCiPipelineYamlParameters>(packageInfo)
            ?? new DotnetCiPipelineYamlParameters();

        packageInfo.CiParameters.BuildSnippets = parameters.BuildSnippets;
    }

    /// <summary>
    /// Updates package metadata including CI YAML provisioning.
    /// Creates a new ci.yml (dataplane) or ci.mgmt.yml (management) if none exists
    /// for the service directory, or appends the package as an artifact if the CI file already exists.
    /// </summary>
    public override async Task<PackageOperationResponse> UpdateMetadataAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            var packageInfo = await GetPackageInfo(packagePath, ct);
            var packageName = packageInfo.PackageName;

            if (string.IsNullOrEmpty(packageName))
            {
                return PackageOperationResponse.CreateFailure(
                    "Could not determine package name from the provided path.",
                    packageInfo);
            }

            var repoRoot = packageInfo.RepoRoot;
            if (string.IsNullOrEmpty(repoRoot))
            {
                return PackageOperationResponse.CreateFailure(
                    "Could not determine repository root from the provided path.",
                    packageInfo);
            }

            var serviceDirectory = packageInfo.ServiceName;
            if (string.IsNullOrEmpty(serviceDirectory))
            {
                return PackageOperationResponse.CreateFailure(
                    "Could not determine service directory from the provided path.",
                    packageInfo);
            }

            // Only provision CI YAML for client/dataplane and management SDKs.
            // Other SDK types (functions, unknown) use different pipelines.
            if (packageInfo.SdkType != SdkType.Dataplane && packageInfo.SdkType != SdkType.Management)
            {
                logger.LogInformation(
                    "Skipping CI YAML provisioning for SDK type {SdkType} at {PackagePath}",
                    packageInfo.SdkType,
                    packagePath);
                return PackageOperationResponse.CreateFailure(
                    $"CI YAML provisioning is only supported for dataplane and management SDKs (type was '{packageInfo.SdkType}'). No changes were made.",
                    packageInfo);
            }

            var isManagement = packageInfo.SdkType == SdkType.Management;
            var ciFileName = isManagement ? "ci.mgmt.yml" : "ci.yml";
            var ciYamlPath = Path.Combine(repoRoot, "sdk", serviceDirectory, ciFileName);

            if (!File.Exists(ciYamlPath))
            {
                var ciContent = isManagement
                    ? CreateMgmtCiYaml(serviceDirectory, packageName)
                    : CreateClientCiYaml(serviceDirectory, packageName);
                await File.WriteAllTextAsync(ciYamlPath, ciContent, ct);

                logger.LogInformation("Created new {CiFileName} at {CiYamlPath}", ciFileName, ciYamlPath);
                return PackageOperationResponse.CreateSuccess(
                    $"Created {ciFileName} for service '{serviceDirectory}' with artifact '{packageName}'. CI file path: {ciYamlPath}",
                    packageInfo);
            }
            else
            {
                var existingYaml = await File.ReadAllTextAsync(ciYamlPath, ct);

                if (CiYamlHasArtifact(existingYaml, packageName))
                {
                    logger.LogInformation("Artifact '{PackageName}' already exists in {CiYamlPath}", packageName, ciYamlPath);
                    return PackageOperationResponse.CreateSuccess(
                        $"Artifact '{packageName}' already exists in {ciFileName} ({ciYamlPath}). No changes needed.",
                        packageInfo);
                }

                var updatedYaml = AddArtifactToCiYaml(existingYaml, packageName);
                if (updatedYaml == null)
                {
                    return PackageOperationResponse.CreateFailure(
                        $"Could not find Artifacts section in existing {ciFileName} to append the new package.",
                        packageInfo);
                }

                await File.WriteAllTextAsync(ciYamlPath, updatedYaml, ct);

                logger.LogInformation("Added artifact '{PackageName}' to {CiYamlPath}", packageName, ciYamlPath);
                return PackageOperationResponse.CreateSuccess(
                    $"Added artifact '{packageName}' to existing {ciFileName} ({ciYamlPath}).",
                    packageInfo);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating CI YAML for package at {PackagePath}", packagePath);
            return PackageOperationResponse.CreateFailure($"Error updating CI YAML for package at {packagePath}: {ex.Message}");
        }
    }

    #region .NET CI YAML provisioning

    private const string DotNetCiYamlTemplate =
        """
        # NOTE: Please refer to https://aka.ms/azsdk/engsys/ci-yaml before editing this file.

        trigger:
          branches:
            include:
            - main
            - hotfix/*
            - release/*
          paths:
            include:
            - sdk/{serviceDirectory}/

        pr:
          branches:
            include:
            - main
            - feature/*
            - hotfix/*
            - release/*
          paths:
            include:
            - sdk/{serviceDirectory}/

        extends:
          template: /eng/pipelines/templates/stages/archetype-sdk-client.yml
          parameters:
            ServiceDirectory: {serviceDirectory}
            ArtifactName: packages
            Artifacts:
            - name: {packageName}
              safeName: {safeName}
        """;

    private static string CreateClientCiYaml(string serviceDirectory, string packageName)
    {
        var safeName = GenerateSafeName(packageName);
        return DotNetCiYamlTemplate
            .Replace("{serviceDirectory}", serviceDirectory)
            .Replace("{packageName}", packageName)
            .Replace("{safeName}", safeName)
            + "\n";
    }

    private const string DotNetMgmtCiYamlTemplate =
        """
         # NOTE: Please refer to https://aka.ms/azsdk/engsys/ci-yaml before editing this file.
 
         trigger: none
         extends:
           template: /eng/pipelines/templates/stages/archetype-sdk-client.yml
           parameters:
             SDKType: mgmt
             ServiceDirectory: {serviceDirectory}
             BuildSnippets: false
             LimitForPullRequest: true
             Artifacts:
             - name: {packageName}
               safeName: {safeName}
         """;

    private static string CreateMgmtCiYaml(string serviceDirectory, string packageName)
    {
        var safeName = GenerateSafeName(packageName);
        return DotNetMgmtCiYamlTemplate
            .Replace("{serviceDirectory}", serviceDirectory)
            .Replace("{packageName}", packageName)
            .Replace("{safeName}", safeName)
            + "\n";
    }

    private static string? AddArtifactToCiYaml(string existingYaml, string packageName)
    {
        var safeName = GenerateSafeName(packageName);

        // Find the last artifact entry and insert after it, matching its indentation.
        var lastArtifactMatch = Regex.Match(
            existingYaml,
            @"^(?<indent>\s*)-\s+name:\s+\S+[^\S\r\n]*(?:\r?\n[^\S\r\n]+(?!-\s+name:)\S[^\r\n]*)*",
            RegexOptions.Multiline | RegexOptions.RightToLeft);

        if (lastArtifactMatch.Success)
        {
            var indent = lastArtifactMatch.Groups["indent"].Value;
            var artifactEntry = $"{indent}- name: {packageName}\n{indent}  safeName: {safeName}";
            var insertPosition = lastArtifactMatch.Index + lastArtifactMatch.Length;
            return existingYaml.Insert(insertPosition, "\n" + artifactEntry);
        }

        // Fallback: look for just "Artifacts:" and append after it
        var artifactsHeaderMatch = Regex.Match(existingYaml, @"^(?<indent>\s*)Artifacts:\s*\r?\n", RegexOptions.Multiline);
        if (artifactsHeaderMatch.Success)
        {
            var indent = artifactsHeaderMatch.Groups["indent"].Value;
            var artifactEntry = $"{indent}- name: {packageName}\n{indent}  safeName: {safeName}";
            var insertPosition = artifactsHeaderMatch.Index + artifactsHeaderMatch.Length;
            return existingYaml.Insert(insertPosition, artifactEntry + "\n");
        }

        return null;
    }

    private static bool CiYamlHasArtifact(string yamlContent, string packageName)
    {
        return Regex.IsMatch(yamlContent, $@"-\s+name:\s+{Regex.Escape(packageName)}\s*$", RegexOptions.Multiline);
    }

    private static string GenerateSafeName(string packageName)
    {
        return packageName.Replace(".", "");
    }

    #endregion

    public override string? HasCustomizations(string packagePath, CancellationToken ct = default)
    {
        // In azure-sdk-for-net, generated code lives in the Generated folder.
        // Customizations are partial types defined outside the Generated folder.
        // Example: sdk/ai/Azure.AI.DocumentIntelligence/src/
        //   - Generated/ (generated code)
        //   - Customized/ or other folders (customization code with partial classes)

        try
        {
            var srcDir = Path.Combine(packagePath, "src");
            if (!Directory.Exists(srcDir))
            {
                return null;
            }

            var generatedDirMarker = Path.DirectorySeparatorChar + GeneratedFolderName + Path.DirectorySeparatorChar;
            var csFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains(generatedDirMarker, StringComparison.OrdinalIgnoreCase));

            foreach (var file in csFiles)
            {
                try
                {
                    foreach (var line in File.ReadLines(file))
                    {
                        if (line.Contains("partial class"))
                        {
                            logger.LogDebug("Found .NET partial class in {FilePath}", file);
                            return srcDir;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to read file {FilePath} for partial class detection", file);
                }
            }

            logger.LogDebug("No .NET partial classes found in {PackagePath}", packagePath);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error searching for .NET customization files in {PackagePath}", packagePath);
            return null;
        }
    }

    internal sealed class DotnetCiPipelineYamlParameters : CiPipelineYamlParametersBase
    {
        [YamlMember(Alias = "BuildSnippets")]
        public bool? BuildSnippets { get; set; } = true;
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

            // Get the list of customization files, excluding generated code
            var generatedDirMarker = Path.DirectorySeparatorChar + GeneratedFolderName + Path.DirectorySeparatorChar;
            var csFiles = Directory.GetFiles(customizationRoot, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(generatedDirMarker, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (csFiles.Length == 0)
            {
                logger.LogDebug("No customization .cs files found in: {Root}", customizationRoot);
                return [];
            }

            // Collect patches directly from the tool as they succeed
            var patchLog = new ConcurrentBag<AppliedPatch>();

            // Build both relative-path lists in a single pass:
            //  - customizationFiles: relative to packagePath (for the ReadFile tool)
            //  - patchFilePaths:     relative to customizationRoot (for the CodePatch tool)
            var customizationFiles = new List<string>(csFiles.Length);
            var patchFilePaths = new List<string>(csFiles.Length);
            foreach (var f in csFiles)
            {
                customizationFiles.Add(Path.GetRelativePath(packagePath, f));
                patchFilePaths.Add(Path.GetRelativePath(customizationRoot, f));
            }

            // Build error-driven prompt for patch agent
            var prompt = new DotnetErrorDrivenPatchTemplate(
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
                        onPatchApplied: patchLog.Add),
                    FileTools.CreateRenameFileTool(customizationRoot,
                        description: "Rename a customization file (e.g., when a class is renamed, the file should be renamed to match). Paths are relative to the customization root.",
                        onFileRenamed: (oldPath, newPath) => patchLog.Add(new AppliedPatch(newPath, $"Renamed file from {oldPath} to {newPath}", 1)))
                ]
            };

            // Run the agent to apply patches
            try
            {
                await copilotAgentRunner.RunAsync(agentDefinition, ct);
            }
            catch (OperationCanceledException)
            {
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
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply patches");
            return [];
        }
    }
}
