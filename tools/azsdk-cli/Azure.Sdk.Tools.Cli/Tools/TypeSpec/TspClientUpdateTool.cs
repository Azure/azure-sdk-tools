// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Services.ClientUpdate;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tools;

[McpServerToolType, Description("Update customized SDK code after TypeSpec regeneration: creates a new generation, provides LLM-guided analysis and recommendations for updating customization code.")]
public class TspClientUpdateTool(
    ILogger<TspClientUpdateTool> logger,
    IClientUpdateLanguageServiceResolver languageServiceResolver,
    ITspClientHelper tspClientHelper,
    Azure.Sdk.Tools.Cli.Microagents.IMicroagentHostService microagentHost
) : MCPTool
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.TypeSpec];

    private readonly Argument<string> updateCommitSha = new(name: "update-commit-sha", description: "SHA of the commit to apply update changes for") { Arity = ArgumentArity.ExactlyOne };
    protected override Command GetCommand() =>
        new("customized-update", "Update customized TypeSpec-generated client code. Creates a new generation, provides LLM-guided analysis and recommendations for updating customization code with applied changes.")
        {
            updateCommitSha,
            SharedOptions.PackagePath,
        };

    public override async Task<CommandResponse> HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        var spec = ctx.ParseResult.GetValueForArgument(updateCommitSha);
        var packagePath = ctx.ParseResult.GetValueForOption(SharedOptions.PackagePath);
        var newGenPath = ctx.ParseResult.GetValueForOption(newGenOpt);
        try
        {
            logger.LogInformation("Starting client update (CLI) for package at: {packagePath} with new-gen: {newGenPath}", packagePath, newGenPath);
            return await RunUpdateAsync(spec, packagePath, newGenPath, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI update failed");
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "ClientUpdateFailed" };
        }
    }

    [McpServerTool(Name = "azsdk_tsp_update"), Description("Update customized TypeSpec-generated client code")]
    public Task<TspClientUpdateResponse> UpdateAsync(string commitSha, string packagePath, CancellationToken ct = default)
        => RunUpdateAsync(commitSha, packagePath, newGenPath: null, ct);

    private async Task<TspClientUpdateResponse> RunUpdateAsync(string commitSha, string packagePath, string? newGenPath, CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Starting client update for package at: {packagePath} (regenDir: {regenDir})", packagePath, newGenPath);
            if (!Directory.Exists(packagePath))
            {
                return new TspClientUpdateResponse { ErrorCode = "1", ResponseError = $"Package path does not exist: {packagePath}" };
            }
            if (string.IsNullOrWhiteSpace(commitSha))
            {
                return new TspClientUpdateResponse { ErrorCode = "1", ResponseError = "Commit SHA is required." };
            }
            var resolved = await languageServiceResolver.ResolveAsync(packagePath, ct);
            if (resolved == null)
            {
                return new TspClientUpdateResponse { ErrorCode = "NoLanguageService", ResponseError = "Could not resolve a client update language service." };
            }
            return await UpdateCoreAsync(commitSha, packagePath, resolved, ct, newGenPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Update failed");
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = ex.GetType().Name };
        }
    }

    private async Task<TspClientUpdateResponse> UpdateCoreAsync(string commitSha, string packagePath, IClientUpdateLanguageService languageService, CancellationToken ct, string? newGenPath)
    {
        var session = new ClientUpdateSessionState { SpecPath = commitSha };

        // Create backup directory to preserve old generation for diff
        var backupDir = CreateBackupDirectory(packagePath);
        await BackupCurrentGeneration(packagePath, backupDir, ct);
        
        session.NewGeneratedPath = packagePath;

        // Locate the existing tsp-location.yaml file within the provided packagePath and overwrite the commit: value with the new sha
        var tspLocationPath = Path.Combine(packagePath, "tsp-location.yaml");
        if (File.Exists(tspLocationPath))
        {
            var tspLocationContent = await File.ReadAllTextAsync(tspLocationPath, ct);
            tspLocationContent = System.Text.RegularExpressions.Regex.Replace(
                tspLocationContent, 
                @"commit:\s+[a-f0-9]+", 
                $"commit: {commitSha}");
            await File.WriteAllTextAsync(tspLocationPath, tspLocationContent, ct);
        }
        logger.LogInformation("Generating new code into package path: {PackagePath}", packagePath);
        // Invoke tsp-client update
        var regenResult = await tspClientHelper.UpdateGenerationAsync(tspLocationPath, packagePath, isCli: false, ct);
        if (!regenResult.IsSuccessful)
        {
            session.LastStage = UpdateStage.Failed;
            return new TspClientUpdateResponse
            {
                Session = session,
                ErrorCode = "RegenerateFailed",
                ResponseError = regenResult.ResponseError
            };
        }
        session.LastStage = UpdateStage.Regenerated;

        var customizationRoot = languageService.GetCustomizationRootAsync(session, packagePath, ct);
         if (string.IsNullOrEmpty(customizationRoot))
        {
            // Nothing to update; proceed to exit
            logger.LogInformation("No customization root found; skipping update session " + session.LastStage);
            return await ValidateWithAutoFixAsync(session, languageService, ct);
        }

        logger.LogDebug("Using customization root: {CustomizationRoot}", customizationRoot);
        // Use LLM-guided analysis and patch application for customization updates
        var (guidance, patchesApplied, requiresReview) = await GenerateLlmGuidanceAndApplyPatchesAsync(commitSha, customizationRoot, packagePath, ct);
        session.LastStage = patchesApplied ? UpdateStage.Applied : UpdateStage.Mapped;
        session.RequiresManualIntervention = requiresReview;
        
        var message = patchesApplied 
            ? "LLM-guided patches applied successfully. Please review the changes and validate your customizations."
            : "LLM-guided analysis complete. Follow the provided next steps to update your customizations.";
        
        return new TspClientUpdateResponse
        {
            Session = session,
            Message = message,
            NextSteps = guidance
        };
    }

    // Move to file helpers
    private static string CreateBackupDirectory(string packagePath)
    {
        // Create a unique backup directory name with timestamp
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupDirName = $"_backup-old-{timestamp}";
        var backupPath = Path.Combine(packagePath, backupDirName);
        Directory.CreateDirectory(backupPath);
        return backupPath;
    }

    private static async Task BackupCurrentGeneration(string packagePath, string backupDir, CancellationToken ct)
    {
        // Copy the entire directory structure but only .java files to maintain same folder structure
        var sourceDir = new DirectoryInfo(packagePath);
        if (!sourceDir.Exists) 
        {
            return;
        }

        // Copy the complete directory structure starting from package root
        // This ensures backup has same structure: both have src/main/java/... 
        await CopyDirectorySelectiveAsync(sourceDir, new DirectoryInfo(backupDir), ct);
    }

    private static async Task CopyDirectorySelectiveAsync(DirectoryInfo source, DirectoryInfo target, CancellationToken ct)
    {
        if (!target.Exists)
        {
            target.Create();
        }

        // Copy all .java files (generated source files)
        foreach (var file in source.GetFiles("*.java"))
        {
            ct.ThrowIfCancellationRequested();
            var targetFile = Path.Combine(target.FullName, file.Name);
            await CopyFileAsync(file.FullName, targetFile, ct);
        }

        // Recursively copy subdirectories but exclude backup directories and focus on source structure
        foreach (var subDir in source.GetDirectories())
        {
            ct.ThrowIfCancellationRequested();
            
            // Skip backup directories to avoid infinite recursion
            if (subDir.Name.StartsWith("_backup-old-", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            
            var targetSubDir = target.CreateSubdirectory(subDir.Name);
            await CopyDirectorySelectiveAsync(subDir, targetSubDir, ct);
        }
    }

    private static async Task CopyFileAsync(string sourcePath, string targetPath, CancellationToken ct)
    {
        await using var sourceStream = File.OpenRead(sourcePath);
        await using var targetStream = File.Create(targetPath);
        await sourceStream.CopyToAsync(targetStream, ct);
    }

    private static async Task<TspClientUpdateResponse> ValidateWithAutoFixAsync(ClientUpdateSessionState session, IClientUpdateLanguageService languageService, CancellationToken ct)
    {
        var result = await languageService.ValidateAsync(session, ct);
        session.LastStage = UpdateStage.Validated;
        if (!result.Success)
        {
            // Attempt a single round of auto-fix for minimal model.
            var fixes = await languageService.ProposeFixesAsync(session, result.Errors, ct);
            if (fixes.Count > 0)
            {
                var retry = await languageService.ValidateAsync(session, ct);
                if (retry.Success)
                {
                    return CompleteClientUpdate(session, "Update pipeline complete (after fixes).");
                }
            }
            session.RequiresManualIntervention = true;
            return CompleteClientUpdate(session, "Validation failed – manual intervention required.");
        }
        return CompleteClientUpdate(session, "Update pipeline complete.");
    }

    private static TspClientUpdateResponse CompleteClientUpdate(ClientUpdateSessionState s, string message) => new()
    {
        Session = s,
        Message = message
    };

    private async Task<(List<string> guidance, bool patchesApplied, bool requiresReview)> GenerateLlmGuidanceAndApplyPatchesAsync(
        string commitSha, 
        string? customizationRoot, 
        string packagePath,
        CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Generating LLM guidance and patches for customization updates");

            // If no customization root, just provide guidance
            if (string.IsNullOrEmpty(customizationRoot) || !Directory.Exists(customizationRoot))
            {
                var basicGuidance = new List<string>
                {
                    "No customization files found. Consider:",
                    "1. Review the generated code changes in: " + packagePath,
                    "2. Check if you need to create new customizations",
                    "3. Rebuild and test your project"
                };
                return (basicGuidance, false, false);
            }

            // Generate patches using LLM
            logger.LogInformation("Starting LLM patch generation process...");
            var patches = await languageService.GenerateLlmPatchesAsync(commitSha, customizationRoot, packagePath, ct) ?? new List<CodePatch>();
            logger.LogInformation("LLM patch generation completed. Received {PatchCount} patches", patches.Count);
            
            var guidance = new List<string>();
            bool patchesApplied = false;
            bool requiresReview = false;

            if (patches.Any())
            {
                logger.LogInformation("Applying {PatchCount} LLM-generated patches", patches.Count);
                
                // Apply patches automatically
                var appliedCount = 0;
                var failedPatches = new List<string>();
                
                foreach (var patch in patches)
                {
                    try
                    {
                        logger.LogInformation("Processing patch for file: '{FilePath}' (Length: {Length}, IsNullOrEmpty: {IsEmpty})", patch.FilePath, patch.NewContent?.Length ?? 0, string.IsNullOrEmpty(patch.NewContent));
                        
                        if (await ApplyPatchToFileAsync(patch.FilePath, patch.OldContent, patch.NewContent))
                        {
                            appliedCount++;
                            logger.LogInformation("Successfully applied patch to {File}", patch.FilePath);
                        }
                        else
                        {
                            failedPatches.Add(patch.FilePath ?? "unknown");
                            logger.LogWarning("Failed to apply patch to {File}", patch.FilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        failedPatches.Add(patch.FilePath ?? "unknown");
                        logger.LogError(ex, "Error applying patch to {File}", patch.FilePath);
                    }
                }

                patchesApplied = appliedCount > 0;
                requiresReview = true; // Always require review after applying patches

                // Build guidance based on what happened
                if (appliedCount > 0)
                {
                    guidance.Add($"✅ Successfully applied {appliedCount} automated patches to your customization files.");
                    guidance.Add("");
                    guidance.Add("🔍 **REVIEW REQUIRED** - Please verify the following changes:");
                }

                if (failedPatches.Any())
                {
                    guidance.Add($"⚠️ {failedPatches.Count} patches could not be applied automatically:");
                    foreach (var file in failedPatches)
                    {
                        guidance.Add($"   - {file}");
                    }
                    guidance.Add("");
                }
                });
            }
            else
            {  
                logger.LogInformation("No patches were generated by the LLM.");
                requiresReview = true; // Require review if no patches were generated
                
                guidance.AddRange(new[]
                {
                    "No automatic patches could be generated. Manual review required:",
                    "1. Compare generated code changes with your customizations",
                    "2. Update import statements, class references, parameter names and API signatures as needed",
                    "3. Fix any compilation errors in customization files",
                    "4. Regenerate and validate the updated code customizations",
                    "5. Rebuild and test your project"
                });
            }
            
            return (guidance, patchesApplied, requiresReview);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate LLM guidance and patches");
            var errorGuidance = new List<string>
            {
                "⚠️ LLM patch generation failed. Manual review required:",
                $"Error: {ex.Message}",
                "1. Review the generated code changes in: " + packagePath,
                "2. Check your customization files for compilation errors",
                "3. Update import statements if needed", 
                "4. Rebuild and test your project",
                "5. Verify API compatibility with existing customizations"
            };
            return (errorGuidance, false, true);
        }
    }
}


