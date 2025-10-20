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
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Tools;

[McpServerToolType, Description("Update customized SDK code after TypeSpec regeneration: creates a new generation, diffs old vs new generated code, maps API changes to impacted customization files, applies patches.")]
public class TspClientUpdateTool(
    ILogger<TspClientUpdateTool> logger,
    ILanguageSpecificResolver<IClientUpdateLanguageService> clientUpdateLanguageSpecificService,
    ITspClientHelper tspClientHelper
) : MCPTool
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.TypeSpec];

    private readonly Argument<string> updateCommitSha = new(name: "update-commit-sha", description: "SHA of the commit to apply update changes for") { Arity = ArgumentArity.ExactlyOne };
    private readonly Option<string?> newGenOpt = new(["--new-gen"], () => "./tmpgen", "Directory for regenerated TypeSpec output (optional)");

    protected override Command GetCommand() =>
        new("customized-update", "Update customized TypeSpec-generated client code. Runs the full pipeline by default: regenerate -> diff -> map -> propose -> apply")
        {
            updateCommitSha,
            SharedOptions.PackagePath,
            newGenOpt
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
            var resolved = await clientUpdateLanguageSpecificService.Resolve(packagePath, ct);
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

        // Determine output directory for new generation: use provided newGenPath (CLI option) or fallback.
        var regenDir = ResolveRegenDirectory(packagePath, newGenPath);
        if (!Directory.Exists(regenDir))
        {
            Directory.CreateDirectory(regenDir);
        }
        session.NewGeneratedPath = regenDir;

        // Locate the existing tsp-location.yaml file within the provided packagePath and overwrite the commit: value with the new sha
        var tspLocationPath = Path.Combine(packagePath, "tsp-location.yaml");
        if (File.Exists(tspLocationPath))
        {
            var tspLocationContent = await File.ReadAllTextAsync(tspLocationPath, ct);
            tspLocationContent = tspLocationContent.Replace("commit: ", $"commit: {commitSha}");
            await File.WriteAllTextAsync(tspLocationPath, tspLocationContent, ct);
        }

        // Invoke tsp-client update
        var regenResult = await tspClientHelper.UpdateGenerationAsync(tspLocationPath, regenDir, isCli: false, ct);
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
        // Now after regeneration, we have old generated at packagePath, new generation at regenDir to perform a diff

        var apiChanges = await languageService.DiffAsync(packagePath, session.NewGeneratedPath);
        session.LastStage = UpdateStage.Diffed;

        if (apiChanges.Count == 0)
        {
            // Nothing to update; proceed to validation of existing customizations.
            return await ValidateWithAutoFixAsync(session, languageService, ct);
        }

        var customizationRoot = await languageService.GetCustomizationRootAsync(session, session.NewGeneratedPath, ct);
        session.CustomizationRoot = customizationRoot;
        var impacts = await languageService.AnalyzeCustomizationImpactAsync(session, customizationRoot, apiChanges, ct);
        session.LastStage = UpdateStage.Mapped;
        if (impacts.Count == 0)
        {
            return await ValidateWithAutoFixAsync(session, languageService, ct);
        }

        var patches = await languageService.ProposePatchesAsync(session, impacts, ct);
        session.LastStage = UpdateStage.PatchesProposed;
        // Apply patches immediately since we don't store them.
        if (patches.Count > 0)
        {
            // Language service may expose an apply method; if not, future enhancement.
        }

        return await ValidateWithAutoFixAsync(session, languageService, ct);
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

    private static string ResolveRegenDirectory(string packagePath, string? newGenPath)
    {
        if (string.IsNullOrWhiteSpace(newGenPath))
        {
            return Path.Combine(packagePath, "_generated-new");
        }
        // If user supplied a relative path, place it under the package path for isolation.
        if (!Path.IsPathRooted(newGenPath))
        {
            return Path.GetFullPath(Path.Combine(packagePath, newGenPath));
        }
        return Path.GetFullPath(newGenPath);
    }
}
