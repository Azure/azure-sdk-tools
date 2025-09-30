// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services.ClientUpdate;

/// <summary>
/// Java-specific update language service.
/// </summary>
public class JavaUpdateLanguageService : ClientUpdateLanguageServiceBase
{
    private readonly ILogger<JavaUpdateLanguageService> _logger;

    public JavaUpdateLanguageService(ILanguageSpecificResolver<ILanguageSpecificChecks> languageSpecificChecks, ILogger<JavaUpdateLanguageService> logger) : base(languageSpecificChecks)
    {
        _logger = logger;
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

    private override async Task<List<CodePatch>> GenerateLlmPatchesAsync(
        string commitSha,
        string customizationRoot,
        string packagePath,
        CancellationToken ct) 
            {
            try
            {
                logger.LogInformation("Generating LLM patches for customization files");
                logger.LogInformation("Customization root: {CustomizationRoot}", customizationRoot);
                logger.LogInformation("Package path: {PackagePath}", packagePath);
                logger.LogInformation("Commit SHA: {CommitSha}", commitSha);

                // Read all .java files under customizationRoot and concatenate their contents into customizationContent
                var customizationContentBuilder = new System.Text.StringBuilder();
                var customizationFiles = new List<string>();
                
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
                        
                        // Store the absolute file path for use in patches
                        customizationFiles.Add(file);
                        
                        customizationContentBuilder.AppendLine($"// File: {file}");
                        customizationContentBuilder.AppendLine(fileContent);
                        customizationContentBuilder.AppendLine();
                    }
                }
                else
                {
                    logger.LogWarning("Customization root directory does not exist: {Root}", customizationRoot);
                }
                
                var customizationContent = customizationContentBuilder.ToString();
                
                // Read old (backup) and new generated code for comparison context
                var (oldGeneratedCode, newGeneratedCode) = await ReadGeneratedCodeComparisonAsync(packagePath, ct);
                
                // Build prompt for patch generation
                var prompt = BuildPatchGenerationPrompt(commitSha, customizationContent, packagePath, customizationRoot, oldGeneratedCode, newGeneratedCode);
                
                // Create microagent for patch generation
                var patchMicroagent = new Microagents.Microagent<List<CodePatch>>
                {
                    Instructions = prompt,
                    Model = "gpt-4",
                    MaxToolCalls = 10
                };

                logger.LogInformation("Creating microagent with model: {Model}, MaxToolCalls: {MaxToolCalls}", "gpt-4", 10);
                
                // Check if microagent host is available
                if (microagentHost == null)
                {
                    logger.LogError("MicroagentHost is null - cannot execute LLM patch generation");
                    return new List<CodePatch>();
                }

                // Generate patches using LLM
                try
                {
                    logger.LogInformation("Calling microagentHost.RunAgentToCompletion...");

                    var patches = await microagentHost.RunAgentToCompletion(patchMicroagent, ct);
                    
                    logger.LogInformation("Microagent execution completed. Result type: {ResultType}, Is null: {IsNull}", 
                        patches?.GetType().Name ?? "null", patches == null);
                        
                    // Enhanced debugging for the result
                    if (patches != null)
                    {
                        logger.LogInformation("Patches collection count: {Count}", patches.Count);
                        logger.LogInformation("Patches collection type: {Type}", patches.GetType().FullName);
                    }
                    else
                    {
                        logger.LogInformation("SUCCESS: Microagent returned {PatchCount} patches for review and application", patches.Count);
                        foreach (var patch in patches.Take(3)) // Log first few patch descriptions
                        {
                            logger.LogInformation("Patch: {Description}", patch.Description);
                        }
                    }
                    
                    return patches;
                }
                catch (Exception microagentEx)
                {
                    logger.LogError(microagentEx, "Exception during microagent execution: {Message}", microagentEx.Message);
                    logger.LogError("Microagent exception stack trace: {StackTrace}", microagentEx.StackTrace);
                    return new List<CodePatch>();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate LLM patches");
                return new List<CodePatch>();
            }
        }

        private string BuildPatchGenerationPrompt(
            string commitSha,
            string customizationContent,
            string packagePath,
            string customizationRoot,
            string oldGeneratedCode,
            string newGeneratedCode)
        {
            var prompt = new System.Text.StringBuilder();
            
            prompt.AppendLine("# TASK: Find API changes and create patches for customization code");
            prompt.AppendLine();
            prompt.AppendLine("TypeSpec generated new Java client code. You need to update customization files to match the new API.");
            prompt.AppendLine();
            
            prompt.AppendLine("## OLD Generated Code:");
            prompt.AppendLine("```java");
            prompt.AppendLine(oldGeneratedCode);
            prompt.AppendLine("```");
            prompt.AppendLine();
            
            prompt.AppendLine("## NEW Generated Code:");
            prompt.AppendLine("```java");
            prompt.AppendLine(newGeneratedCode);
            prompt.AppendLine("```");
            prompt.AppendLine();
            
            prompt.AppendLine("## Customization Code (needs updates):");
            prompt.AppendLine("```java");
            prompt.AppendLine(customizationContent);
            prompt.AppendLine("```");
            prompt.AppendLine();
            
            prompt.AppendLine("## What you need to do:");
            prompt.AppendLine("1. **Compare OLD vs NEW**: Find what changed (method names, parameter names, class names)");
            prompt.AppendLine("2. **Find in customization**: Look for the OLD names in the customization code");
            prompt.AppendLine("3. **Create patches**: For each OLD name found, create a patch to update it to the NEW name");
            prompt.AppendLine();
            
            prompt.AppendLine("## Common changes to look for:");
            prompt.AppendLine("- Parameter name changes: `getParameterByName(\"oldName\")` → `getParameterByName(\"newName\")`");
            prompt.AppendLine("- Method name changes: `oldMethodName()` → `newMethodName()`");
            prompt.AppendLine("- Class name changes: `OldClassName` → `NewClassName`");
            prompt.AppendLine("- Import statement changes: `import com.azure.OldClass` → `import com.azure.NewClass`");
            prompt.AppendLine();
            
            prompt.AppendLine("## Example patch:");
            prompt.AppendLine("If OLD code had `beginAnalyzeDocument(analyzeRequest)` and NEW code has `beginAnalyzeDocument(analyzeDocumentRequest)`,");
            prompt.AppendLine("and customization has `getParameterByName(\"analyzeRequest\")`, create this patch:");
            prompt.AppendLine("```json");
            prompt.AppendLine("{");
            prompt.AppendLine($"  \"FilePath\": \"{Path.Combine(customizationRoot, "DocumentIntelligenceCustomizations.java").Replace("\\", "\\\\")}\",");
            prompt.AppendLine("  \"Description\": \"Update parameter name from analyzeRequest to analyzeDocumentRequest\",");
            prompt.AppendLine("  \"OldContent\": \"getParameterByName(\\\"analyzeRequest\\\")\",");
            prompt.AppendLine("  \"NewContent\": \"getParameterByName(\\\"analyzeDocumentRequest\\\")\"");
            prompt.AppendLine("}");
            prompt.AppendLine("```");
            prompt.AppendLine();
            
            prompt.AppendLine("## IMPORTANT:");
            prompt.AppendLine("- Only create patches for things ACTUALLY found in the customization code");
            prompt.AppendLine("- Use EXACT file paths with double backslashes for Windows");
            prompt.AppendLine("- Include enough context in OldContent/NewContent to uniquely identify the location");
            prompt.AppendLine("- If no changes are needed, return empty array: Exit({\"Result\": []})");
            prompt.AppendLine();
            
            prompt.AppendLine("## Your response format:");
            prompt.AppendLine("1. First, tell me what differences you found between OLD and NEW code");
            prompt.AppendLine("2. Then, tell me which of those differences exist in the customization code");
            prompt.AppendLine("3. Finally, call Exit({\"Result\": [array of patches]})");
            prompt.AppendLine();
            prompt.AppendLine("Exit format:");
            prompt.AppendLine("```json");
            prompt.AppendLine("{\"Result\": [{\"FilePath\": \"full\\\\path\", \"Description\": \"what changed\", \"OldContent\": \"exact old text\", \"NewContent\": \"exact new text\"}]}");
            prompt.AppendLine("```");
            prompt.AppendLine();
            prompt.AppendLine("⚠️ The OLD and NEW code ARE different - you MUST find what changed!");
            
            return prompt.ToString();
        }

        private async Task<bool> ApplyPatchToFileAsync(string filePath, string oldContent, string newContent)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    logger.LogWarning("Patch target file does not exist: {FilePath}", filePath);
                    return false;
                }

                var fileContent = await File.ReadAllTextAsync(filePath);
                
                if (!fileContent.Contains(oldContent))
                {
                    logger.LogWarning("Old content not found in file {FilePath}", filePath);
                    return false;
                }

                var updatedContent = fileContent.Replace(oldContent, newContent);
                await File.WriteAllTextAsync(filePath, updatedContent);
                
                logger.LogInformation("Successfully applied patch to {FilePath}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to apply patch to {FilePath}", filePath);
                return false;
            }
        }
}
