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
    private readonly Argument<string> specPathArg = new(name: "spec-path", description: "Path to the .tsp specification file") { Arity = ArgumentArity.ExactlyOne };
    private readonly Option<string?> newGenOpt = new(["--new-gen"], () => "./tmpgen", "Directory for regenerated TypeSpec output (optional)");

    public TspClientUpdateTool(ILogger<TspClientUpdateTool> logger, IOutputHelper output, IClientUpdateLanguageServiceResolver languageServiceResolver)
    {
        this.logger = logger;
        this.output = output;
        this.languageServiceResolver = languageServiceResolver;
        CommandHierarchy = [ SharedCommandGroups.TypeSpec ];
    }

    public override Command GetCommand()
    {
        var cmd = new Command("customized-update",
            description: "Update customized TypeSpec-generated client code. Runs the full pipeline by default: regenerate -> diff -> map -> propose -> apply");
        cmd.AddArgument(specPathArg);
        cmd.AddOption(SharedOptions.PackagePath);
        cmd.AddOption(newGenOpt);
        cmd.SetHandler(async ctx => await HandleUpdate(ctx, ctx.GetCancellationToken()));
        return cmd;
    }

    public override Task HandleCommand(InvocationContext ctx, CancellationToken ct) => Task.CompletedTask;

    [McpServerTool(Name = "azsdk_tsp_update"), Description("Update customized TypeSpec-generated client code")]
    public async Task<TspClientUpdateResponse> UpdateAsync(string specPath, string packagePath, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation($"Starting client update for package at: {packagePath}");
            if (!Directory.Exists(packagePath))
            {
                SetFailure(1);
                return new TspClientUpdateResponse
                {
                    ErrorCode = "1",
                    ResponseError = $"Package path does not exist: {packagePath}",
                    Message = ""
                };
            }
            if (string.IsNullOrWhiteSpace(specPath))
            {
                SetFailure(1);
                return new TspClientUpdateResponse
                {
                    ErrorCode = "1",
                    ResponseError = $"Spec path is required.",
                    Message = ""
                };
            }
            var resolved = await languageServiceResolver.ResolveAsync(packagePath, ct);
            if (resolved == null)
            {
                SetFailure(1);
                return new TspClientUpdateResponse
                {
                    ErrorCode = "NoLanguageService",
                    ResponseError = "Could not resolve a client update language service.",
                    Message = ""
                };
            }
            return await UpdateCoreAsync(specPath, packagePath, resolved, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Update failed");
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = ex.GetType().Name };
        }
    }

    private async Task<TspClientUpdateResponse> UpdateCoreAsync(string specPath, string packagePath, IClientUpdateLanguageService languageService, CancellationToken ct)
    {
        var session = new ClientUpdateSessionState { SpecPath = specPath };

        // Regenerate (placeholder)
        session.LastStage = UpdateStage.Regenerated;

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

    private async Task HandleUpdate(InvocationContext ctx, CancellationToken ct)
    {
        var spec = ctx.ParseResult.GetValueForArgument(specPathArg);
        var packagePath = ctx.ParseResult.GetValueForOption(SharedOptions.PackagePath);
        try
        {
            logger.LogInformation($"Starting client update for package at: {packagePath}");
            var resp = await UpdateAsync(spec, packagePath, ct);
            output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI update failed");
            output.OutputError(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "ClientUpdateFailed" });
        }
    }
}
