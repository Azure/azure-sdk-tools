// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Services.ClientUpdate;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tools;

[McpServerToolType, Description("Update customized SDK code after TypeSpec regeneration: creates a new generation, diffs old vs new generated code, maps API changes to impacted customization files, applies patches.")]
public class TspClientUpdateTool : MCPTool
{
    private readonly ILogger<TspClientUpdateTool> logger;
    private readonly IOutputHelper output;
    private readonly IClientUpdateLanguageServiceResolver languageServiceResolver;
    private readonly ITspClientHelper tspClientHelper;
    private readonly Argument<string> updateCommitSha = new(name: "update-commit-sha", description: "SHA of the commit to apply update changes for") { Arity = ArgumentArity.ExactlyOne };

    public TspClientUpdateTool(ILogger<TspClientUpdateTool> logger, IOutputHelper output, IClientUpdateLanguageServiceResolver languageServiceResolver, ITspClientHelper tspClientHelper)
    {
        this.logger = logger;
        this.output = output;
        this.languageServiceResolver = languageServiceResolver;
        this.tspClientHelper = tspClientHelper;
        CommandHierarchy = [ SharedCommandGroups.TypeSpec ];
    }

    public override Command GetCommand()
    {
        var cmd = new Command("customized-update",
            description: "Update customized TypeSpec-generated client code. Runs the full pipeline by default: regenerate -> diff -> map -> propose -> apply");
        cmd.AddArgument(updateCommitSha);
        cmd.AddOption(SharedOptions.PackagePath);
        cmd.SetHandler(async ctx => await HandleUpdate(ctx, ctx.GetCancellationToken()));
        return cmd;
    }

    public override Task HandleCommand(InvocationContext ctx, CancellationToken ct) => Task.CompletedTask;

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
                SetFailure(1);
                return new TspClientUpdateResponse { ErrorCode = "1", ResponseError = $"Package path does not exist: {packagePath}" };
            }
            if (string.IsNullOrWhiteSpace(commitSha))
            {
                SetFailure(1);
                return new TspClientUpdateResponse { ErrorCode = "1", ResponseError = "Commit SHA is required." };
            }
            var resolved = await languageServiceResolver.ResolveAsync(packagePath, ct);
            if (resolved == null)
            {
                SetFailure(1);
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
        // Invoke tsp-client update to generate directly into package path
        var regenResult = await tspClientHelper.UpdateGenerationAsync(tspLocationPath, packagePath, isCli: false, ct);
        if (!regenResult.IsSuccessful)
        {
            SetFailure(1);
            session.LastStage = UpdateStage.Failed;
            return new TspClientUpdateResponse
            {
                Session = session,
                ErrorCode = "RegenerateFailed",
                ResponseError = regenResult.ResponseError
            };
        }
        session.LastStage = UpdateStage.Regenerated;

        // Now after regeneration, we have old generated at backupDir, new generation at packagePath to perform a diff
        var apiChanges = await languageService.DiffAsync(backupDir, session.NewGeneratedPath);
        session.LastStage = UpdateStage.Diffed;

        // Clean up temporary backup directory after diff is complete
        try
        {
            if (Directory.Exists(backupDir))
            {
                Directory.Delete(backupDir, recursive: true);
                logger.LogInformation("Cleaned up temporary backup directory: {BackupDir}", backupDir);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clean up temporary backup directory: {BackupDir}", backupDir);
        }

        if (apiChanges.Count == 0)
        {
            // Nothing to update; proceed to validation of existing customizations.
            logger.LogInformation("No API changes detected; skipping update, session " + session.LastStage);
            return await ValidateWithAutoFixAsync(session, languageService, ct);
        }

        var customizationRoot = languageService.GetCustomizationRootAsync(session, packagePath, ct);
        logger.LogInformation("Customization root result: '{CustomizationRoot}'", customizationRoot ?? "null");
        
        if (string.IsNullOrEmpty(customizationRoot))
        {
            // Nothing to update; proceed to exit
            logger.LogInformation("No customization root found; skipping update session " + session.LastStage);
            return await ValidateWithAutoFixAsync(session, languageService, ct);
        }
        
        logger.LogInformation("Using customization root: {CustomizationRoot}", customizationRoot);
        session.CustomizationRoot = customizationRoot;
        
        var analysisResult = await languageService.AnalyzeAndProposePatchesAsync(session, customizationRoot, apiChanges, ct);
        var impacts = analysisResult.Item1;
        var patches = analysisResult.Item2;
        session.LastStage = UpdateStage.Mapped;
        
        if (impacts.Count == 0)
        {
            logger.LogInformation("No impacted customizations detected; skipping patch application, session " + session.LastStage);
            return await ValidateWithAutoFixAsync(session, languageService, ct);
        }

        session.LastStage = UpdateStage.PatchesProposed;
        
        // Apply patches immediately since we don't store them.
        if (patches.Count > 0)
        {
            logger.LogInformation("Applying {PatchCount} patches to customization files", patches.Count);
            var applyResult = await languageService.ApplyPatchesAsync(session, patches, ct);
            
            if (!applyResult.Success)
            {
                logger.LogWarning("Failed to apply {FailedCount}/{TotalCount} patches", 
                    applyResult.FailedPatchesCount, applyResult.TotalPatches);
                
                // Log specific errors for troubleshooting
                foreach (var error in applyResult.Errors.Take(5)) // Limit to avoid log spam
                {
                    logger.LogWarning("Patch application error: {Error}", error);
                }
            }
            else
            {
                logger.LogInformation("Successfully applied all {PatchCount} patches", applyResult.SuccessfulPatches);
            }
            
            session.LastStage = UpdateStage.Applied;
        }

        return await ValidateWithAutoFixAsync(session, languageService, ct);
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

    private async Task HandleUpdate(InvocationContext ctx, CancellationToken ct)
    {
        var spec = ctx.ParseResult.GetValueForArgument(updateCommitSha);
        var packagePath = ctx.ParseResult.GetValueForOption(SharedOptions.PackagePath);
        try
        {
            logger.LogInformation("Starting client update (CLI) for package at: {packagePath}", packagePath);
            var resp = await RunUpdateAsync(spec, packagePath, ct);
            output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI update failed");
            output.OutputError(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "ClientUpdateFailed" });
        }
    }

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
}
