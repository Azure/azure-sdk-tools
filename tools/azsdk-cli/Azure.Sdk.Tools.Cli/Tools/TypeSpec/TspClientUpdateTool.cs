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
    ITspClientHelper tspClientHelper
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
        try
        {
            logger.LogInformation("Starting client update (CLI) for package at: {packagePath}", packagePath);
            return await RunUpdateAsync(spec, packagePath, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI update failed");
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "ClientUpdateFailed" };
        }
    }

    [McpServerTool(Name = "azsdk_tsp_update"), Description("Update customized TypeSpec-generated client code")]
    public Task<TspClientUpdateResponse> UpdateAsync(string commitSha, string packagePath, CancellationToken ct = default)
        => RunUpdateAsync(commitSha, packagePath, ct);

    private async Task<TspClientUpdateResponse> RunUpdateAsync(string commitSha, string packagePath, CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Starting client update for package at: {packagePath}", packagePath);
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
            return await UpdateCoreAsync(commitSha, packagePath, resolved, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Update failed");
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = ex.GetType().Name };
        }
    }

    private async Task<TspClientUpdateResponse> UpdateCoreAsync(string commitSha, string packagePath, IClientUpdateLanguageService languageService, CancellationToken ct)
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
        // Use automated analysis and patch application for customization updates
        var (guidance, patchesApplied, requiresReview) = await GenerateGuidanceAndApplyPatchesAsync(commitSha, customizationRoot, packagePath, backupDir, languageService, ct);
        
        // If patches were applied, regenerate the code to ensure customizations are properly integrated
        if (patchesApplied)
        {
            logger.LogInformation("Patches were applied. Regenerating code to validate customizations...");
            var regenAfterPatchResult = await RegenerateAfterPatchesAsync(tspLocationPath, packagePath, ct);
            if (!regenAfterPatchResult.Success)
            {
                logger.LogWarning("Code regeneration after patches failed: {Error}", regenAfterPatchResult.ErrorMessage);
                guidance.Insert(0, "⚠️ Code regeneration after patches failed. Manual intervention required.");
                guidance.Insert(1, $"Error: {regenAfterPatchResult.ErrorMessage}");
                guidance.Insert(2, "");
                requiresReview = true;
                session.RequiresManualIntervention = true;
            }
            else
            {
                logger.LogInformation("Code regeneration after patches completed successfully");
                
                // TODO: Add validation step after patch application
                // Run validation to ensure customizations work properly with newly generated code
                logger.LogInformation("Running validation after patch application...");
                var validationResult = await languageService.ValidateAsync(session, ct);
                
                if (validationResult.Success)
                {
                    logger.LogInformation("Validation completed successfully after patch application");
                    guidance.Insert(0, "✅ Code regenerated and validated successfully after applying patches.");
                    guidance.Insert(1, "");
                }
                else
                {
                    logger.LogWarning("Validation failed after patch application: {Errors}", string.Join(", ", validationResult.Errors));
                    guidance.Insert(0, "⚠️ Code regenerated but validation failed after applying patches.");
                    guidance.Insert(1, $"Validation errors: {string.Join(", ", validationResult.Errors)}");
                    guidance.Insert(2, "");
                    requiresReview = true;
                    session.RequiresManualIntervention = true;
                }
            }
        }
        
        // Clean up backup directory - it was only needed for LLM reference during patch generation
        try
        {
            if (Directory.Exists(backupDir))
            {
                logger.LogDebug("Cleaning up backup directory: {BackupDir}", backupDir);
                Directory.Delete(backupDir, recursive: true);
                logger.LogInformation("Successfully cleaned up backup directory");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clean up backup directory: {BackupDir}", backupDir);
            // Don't fail the operation if cleanup fails - this is just housekeeping
        }
        
        session.LastStage = patchesApplied ? UpdateStage.Applied : UpdateStage.Mapped;
        session.RequiresManualIntervention = requiresReview;

        var message = patchesApplied
            ? "Automated patches applied successfully. Please review the changes and validate your customizations."
            : "Automated analysis complete. Follow the provided next steps to update your customizations.";

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

    private async Task<(bool Success, string? ErrorMessage)> RegenerateAfterPatchesAsync(string tspLocationPath, string packagePath, CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Running tsp-client update after applying customization patches");
            
            // Run tsp-client update again to regenerate code with the updated customizations
            var regenResult = await tspClientHelper.UpdateGenerationAsync(tspLocationPath, packagePath, isCli: false, ct);
            
            if (!regenResult.IsSuccessful)
            {
                logger.LogError("Code regeneration failed after patches: {Error}", regenResult.ResponseError);
                return (false, regenResult.ResponseError ?? "Code regeneration failed");
            }
            
            logger.LogInformation("Code regeneration completed successfully after applying patches");
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception during code regeneration after patches");
            return (false, ex.Message);
        }
    }

    private static async Task<TspClientUpdateResponse> ValidateWithAutoFixAsync(ClientUpdateSessionState session, IClientUpdateLanguageService languageService, CancellationToken ct)
    {
        var result = await languageService.ValidateAsync(session, ct);
        session.LastStage = UpdateStage.Validated;
        if (!result.Success)
        {
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

    private async Task<(List<string> guidance, bool patchesApplied, bool requiresReview)> GenerateGuidanceAndApplyPatchesAsync(
        string commitSha,
        string? customizationRoot,
        string newGeneratedPath,
        string oldGeneratedPath,
        IClientUpdateLanguageService languageService,
        CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Generating guidance and applying patches for customization updates");

            // If no customization root, just provide guidance
            if (string.IsNullOrEmpty(customizationRoot) || !Directory.Exists(customizationRoot))
            {
                var basicGuidance = new List<string>
                {
                    "ℹ️ No customization files found.",
                    "1. Review generated code changes",
                    "2. Create customizations if needed"
                };
                return (basicGuidance, false, false);
            }

            // Apply patches automatically
            logger.LogInformation("Starting automatic patch application process...");
            var patchesApplied = await languageService.ApplyLlmPatchesAsync(commitSha, customizationRoot, newGeneratedPath, oldGeneratedPath, ct);
            logger.LogInformation("Automatic patch application completed with result: {Success}", patchesApplied);

            var guidance = new List<string>();
            bool requiresReview = true; // Always require review after automatic changes

            if (patchesApplied)
            {
                // Build guidance for successful patch application
                guidance.Add("✅ Patches applied automatically and code regenerated with validation.");
                guidance.Add("");
                guidance.Add("📋 Next steps:");
                guidance.Add("1. Review applied changes in customization files");
                guidance.Add("2. Review generated code after customization updates to ensure it meets your code requirements");
                guidance.Add("3. Fix any remaining issues if needed");
                guidance.Add("4. Open a pull request");
            }
            else
            {
                logger.LogInformation("Automatic patch application was not successful or no patches were needed.");

                guidance.AddRange(new[]
                {
                    "⚠️ Manual review required - automatic patches unsuccessful or not needed.",
                    "1. Compare generated code with your customizations",
                    "2. Update customization files manually",
                    "3. Regenerate with updated customization code to ensure it meets your code requirements",
                    "4. Open a pull request with your changes"
                });
            }

            return (guidance, patchesApplied, requiresReview);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate guidance and apply patches");
            var errorGuidance = new List<string>
            {
                "❌ Automatic patch application failed. Manual review required.",
                $"Error: {ex.Message}",
                "1. Review generated code changes",
                "2. Update customization files manually",
                "3. Open a pull request with your changes"
            };
            return (errorGuidance, false, true);
        }
    }


}


