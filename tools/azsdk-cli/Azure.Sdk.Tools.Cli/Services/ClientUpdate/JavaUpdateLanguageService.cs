// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Microagents.Tools;
using Azure.Sdk.Tools.Cli.Prompts.Templates;

namespace Azure.Sdk.Tools.Cli.Services.ClientUpdate;

/// <summary>
/// Java-specific update language service.
/// </summary>
public class JavaUpdateLanguageService : ClientUpdateLanguageServiceBase
{
    private readonly ILogger<JavaUpdateLanguageService> _logger;
    private readonly IMicroagentHostService _microagentHost;

    public JavaUpdateLanguageService(ILanguageSpecificResolver<ILanguageSpecificChecks> languageSpecificChecks, IMicroagentHostService microagentHost, ILogger<JavaUpdateLanguageService> logger) : base(languageSpecificChecks)
    {
        _logger = logger;
        _microagentHost = microagentHost;
    }

    private const string CustomizationDirName = "customization";

    public override Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath)
    {
        // TODO: implement file-level diff between oldGenerationPath and newGenerationPath.
        return Task.FromResult(new List<ApiChange>());
    }

    public override string GetCustomizationRootAsync(ClientUpdateSessionState session, string generationRoot, CancellationToken ct)
    {
        try
        {
            // In azure-sdk-for-java layout, generated code lives under:
            //   <pkgRoot>/azure-<package>-<service>/src
            // Customizations (single root) live under parallel directory:
            //   <pkgRoot>/azure-<package>-<service>/customization/src/main/java
            // Example (document intelligence):
            //   generated root: .../azure-ai-documentintelligence/src
            //   customization root: .../azure-ai-documentintelligence/customization/src/main/java
            _logger.LogInformation("Trying to resolve Java customization root from generationRoot '{GenerationRoot}'", generationRoot);
            var customizationSourceRoot = Path.Combine(generationRoot, CustomizationDirName, "src", "main", "java");
            var exists = Directory.Exists(customizationSourceRoot);
            _logger.LogInformation("Directory exists check result: {Exists}", exists);

            if (exists)
            {
                return customizationSourceRoot;
            }
            _logger.LogInformation("No customization directory found at either level, returning null");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve Java customization root from generationRoot '{GenerationRoot}'", generationRoot);
        }
        return null;
    }

    public override async Task<bool> ApplyPatchesAsync(
        string commitSha,
        string customizationRoot,
        string newGeneratedPath,
        string oldGeneratedPath,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Generating automated patches for customization files");
            _logger.LogInformation("Customization root: {CustomizationRoot}", customizationRoot);
            _logger.LogInformation("New generated path: {NewGeneratedPath}", newGeneratedPath);
            _logger.LogInformation("Old generated path: {OldGeneratedPath}", oldGeneratedPath);
            _logger.LogInformation("Commit SHA: {CommitSha}", commitSha);

            // Read all .java files under customizationRoot and concatenate their contents into customizationContent
            var customizationContentBuilder = new System.Text.StringBuilder();

            if (Directory.Exists(customizationRoot))
            {
                var javaFiles = Directory.GetFiles(customizationRoot, "*.java", SearchOption.AllDirectories);
                _logger.LogInformation("Found {FileCount} Java customization files in {Root}", javaFiles.Length, customizationRoot);

                foreach (var file in javaFiles)
                {
                    _logger.LogDebug("Reading customization file: {File}", file);
                    var fileContent = await File.ReadAllTextAsync(file, ct);
                    _logger.LogInformation("File {File} has {Lines} lines and {Characters} characters",
                        Path.GetFileName(file), fileContent.Split('\n').Length, fileContent.Length);

                    customizationContentBuilder.AppendLine($"// File: {file}");
                    customizationContentBuilder.AppendLine(fileContent);
                    customizationContentBuilder.AppendLine();
                }
            }
            else
            {
                _logger.LogWarning("Customization root directory does not exist: {Root}", customizationRoot);
                return false;
            }

            var customizationContent = customizationContentBuilder.ToString();

            // For now, using placeholder generated code - TODO: implement proper old/new code comparison  
            // oldGeneratedPath contains the backup of old generated code
            // newGeneratedPath contains the newly generated code
            var oldGeneratedCode = $"// Placeholder old generated code from: {oldGeneratedPath}";
            var newGeneratedCode = $"// Placeholder new generated code from: {newGeneratedPath}";

            // Build prompt for direct patch application using the java patch template
            var prompt = new JavaPatchGenerationTemplate(
                oldGeneratedCode,
                newGeneratedCode,
                customizationContent,
                customizationRoot,
                newGeneratedPath,
                commitSha).BuildPrompt();
            _logger.LogInformation("Generated prompt for patch analysis with {ContentLength} characters", prompt.Length);

            var agentDefinition = new Microagent<bool>
            {
                Instructions = prompt,
                Tools =
                [
                    new ReadFileTool(newGeneratedPath)
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
            _logger.LogInformation("Sending prompt to microagent for direct patch application");
            var patchApplicationSuccess = await _microagentHost.RunAgentToCompletion(agentDefinition, ct);
            _logger.LogInformation("Patch application completed with result: {Success}", patchApplicationSuccess);

            // Return the result of patch application
            return patchApplicationSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply automated patches");
            return false;
        }
    }
}
