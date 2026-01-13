// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Microagents.Tools;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Prompts.Templates;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

public sealed partial class JavaLanguageService : LanguageService
{
    public override SdkLanguage Language { get; } = SdkLanguage.Java;
    public override bool IsCustomizedCodeUpdateSupported => true;
    private readonly IMicroagentHostService microagentHost;
    private readonly IMavenHelper _mavenHelper;
    private const string CustomizationDirName = "customization";

    public JavaLanguageService(
        IProcessHelper processHelper,
        IGitHelper gitHelper,
        IMavenHelper mavenHelper,
        IMicroagentHostService microagentHost,
        ILogger<LanguageService> logger,
        ICommonValidationHelpers commonValidationHelpers,
        IFileHelper fileHelper,
        ISpecGenSdkConfigHelper specGenSdkConfigHelper)
        : base(processHelper, gitHelper, logger, commonValidationHelpers, fileHelper, specGenSdkConfigHelper)
    {
        this.microagentHost = microagentHost;
        this._mavenHelper = mavenHelper;
    }

    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        logger.LogDebug("Resolving Java package info for path: {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = PackagePathParser.Parse(gitHelper, packagePath);
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
        var result = await _mavenHelper.Run(new("test", ["--no-transfer-progress"], pomPath, workingDirectory: packagePath, timeout: TestTimeout), ct);

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

    public override string GetCustomizationRoot(string generationRoot, CancellationToken ct)
    {
        try
        {
            // In azure-sdk-for-java layout, generated code lives under:
            //   <pkgRoot>/azure-<package>-<service>/src
            // Customizations live under parallel directory:
            //   <pkgRoot>/azure-<package>-<service>/customization/src/main/java
            // Example (document intelligence):
            //   generated root: .../azure-ai-documentintelligence/src
            //   customization root: .../azure-ai-documentintelligence/customization/src/main/java
            logger.LogInformation("Trying to resolve Java customization root from generationRoot '{GenerationRoot}'", generationRoot);

            var customizationSourceRoot = Path.Combine(generationRoot, CustomizationDirName, "src", "main", "java");
            var exists = Directory.Exists(customizationSourceRoot);
            logger.LogInformation("Checking customization path: {CustomizationPath}, exists: {Exists}", customizationSourceRoot, exists);

            if (exists)
            {
                return customizationSourceRoot;
            }
            logger.LogInformation("No customization directory found, returning null");
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to resolve Java customization root from generationRoot '{GenerationRoot}'", generationRoot);
        }
        return null;
    }

    public override async Task<bool> ApplyPatchesAsync(
        string commitSha,
        string customizationRoot,
        string packagePath,
        CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Generating automated patches for customization files");
            logger.LogInformation("Customization root: {CustomizationRoot}", customizationRoot);
            logger.LogInformation("Package path: {PackagePath}", packagePath);
            logger.LogInformation("Commit SHA: {CommitSha}", commitSha);

            // Read all .java files under customizationRoot and concatenate their contents into customizationContent
            var customizationContentBuilder = new System.Text.StringBuilder();

            if (Directory.Exists(customizationRoot))
            {
                var javaFiles = Directory.GetFiles(customizationRoot, "*.java", SearchOption.AllDirectories);
                logger.LogInformation("Found {FileCount} Java customization files in {Root}", javaFiles.Length, customizationRoot);

                foreach (var file in javaFiles)
                {
                    logger.LogDebug("Reading customization file: {File}", file);
                    var fileContent = await File.ReadAllTextAsync(file, ct);
                    logger.LogInformation("File {File} has {Lines} lines and {Characters} characters",
                        Path.GetFileName(file), fileContent.Split('\n').Length, fileContent.Length);

                    // Use relative path from customizationRoot for the LLM to reference
                    var relativePath = Path.GetRelativePath(customizationRoot, file);
                    customizationContentBuilder.AppendLine($"// File: {relativePath}");
                    customizationContentBuilder.AppendLine(fileContent);
                    customizationContentBuilder.AppendLine();
                }
            }
            else
            {
                logger.LogWarning("Customization root directory does not exist: {Root}", customizationRoot);
                return false;
            }

            var customizationContent = customizationContentBuilder.ToString();

            // For now, using placeholder generated code - TODO: implement proper old/new code comparison  
            // Future enhancement: read actual generated files and compare them
            var oldGeneratedCode = "// TODO: Read actual old generated code for comparison";
            var newGeneratedCode = "// TODO: Read actual new generated code for comparison";

            // Build prompt for direct patch application using the java patch template
            var prompt = new JavaPatchGenerationTemplate(
                oldGeneratedCode,
                newGeneratedCode,
                packagePath,
                customizationContent,
                customizationRoot,
                commitSha).BuildPrompt();
            logger.LogInformation("Generated prompt for patch analysis with {ContentLength} characters", prompt.Length);

            var agentDefinition = new Microagent<bool>
            {
                Instructions = prompt,
                Tools =
                [
                    new ReadFileTool(packagePath)
                    {
                        Name = "ReadFile",
                        Description = "Read files from the package directory (generated code, customization files, etc.)"
                    },
                    new ClientCustomizationCodePatchTool(customizationRoot)
                    {
                        Name = "ClientCustomizationCodePatch",
                        Description = "Apply code patches directly to customization files"
                    }
                ]
            };

            // Use microagent system to apply patches directly
            logger.LogInformation("Sending prompt to microagent for direct patch application");
            var patchApplicationSuccess = await microagentHost.RunAgentToCompletion(agentDefinition, ct);
            logger.LogInformation("Patch application completed with result: {Success}", patchApplicationSuccess);

            // Return the result of patch application
            return patchApplicationSuccess;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply automated patches");
            return false;
        }
    }

    public override List<SetupRequirements.Requirement> GetRequirements(string packagePath, Dictionary<string, List<SetupRequirements.Requirement>> categories, CancellationToken ct = default)
    {
        return categories.TryGetValue("java", out var requirements) ? requirements : new List<SetupRequirements.Requirement>();
    }
}
