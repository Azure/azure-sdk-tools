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
    private readonly Func<string, IClientUpdateLanguageService> languageServiceFactory;
    private readonly Argument<string> specPathArg = new(name: "spec-path", description: "Path to the .tsp specification file") { Arity = ArgumentArity.ExactlyOne };
    private readonly Option<string?> newGenOpt = new(["--new-gen"], () => "./tmpgen", "Directory for regenerated TypeSpec output (optional)");

    public TspClientUpdateTool(ILogger<TspClientUpdateTool> logger, IOutputHelper output, Func<string, IClientUpdateLanguageService> languageServiceFactory)
    {
        this.logger = logger;
        this.output = output;
        this.languageServiceFactory = languageServiceFactory;
        CommandHierarchy = [ SharedCommandGroups.TypeSpec ];
    }

    public override Command GetCommand()
    {
        var cmd = new Command("customized-update",
            description: "Update customized TypeSpec-generated client code. Runs the full pipeline by default: regenerate -> diff -> map -> propose -> apply");
        cmd.AddArgument(specPathArg);
        cmd.AddOption(SharedOptions.PackagePath);
        cmd.AddOption(newGenOpt);
        cmd.SetHandler(async ctx => await HandleUnified(ctx, ctx.GetCancellationToken()));
        return cmd;
    }

    public override Task HandleCommand(InvocationContext ctx, CancellationToken ct) => Task.CompletedTask;

    [McpServerTool(Name = "azsdk_tsp_update"), Description("Update pipeline")] 
    public async Task<TspClientUpdateResponse> UnifiedUpdateAsync(string specPath, IClientUpdateLanguageService languageService, string? packagePath = null, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(specPath))
            {
                return new TspClientUpdateResponse { ResponseError = "Spec path required", ErrorCode = "MissingSpec" };
            }
            var session = new ClientUpdateSessionState { SpecPath = specPath, PackagePath = packagePath ?? string.Empty };

            // Regenerate (placeholder)
            session.LastStage = UpdateStage.Regenerated;
            session.Status = "Regenerated";

            // Diff
            var apiChanges = await languageService.DiffAsync(new Dictionary<string, SymbolInfo>(), new Dictionary<string, SymbolInfo>());
            session.ApiChanges = apiChanges;
            session.ApiChangeCount = apiChanges.Count;
            session.LastStage = UpdateStage.Diffed;
            session.Status = "Diffed";

            // Short-circuit: no API changes -> skip Map/Propose and go validate (still useful for drift checks)
            if (apiChanges.Count == 0)
            {
                session.Status = "NoApiChanges"; // informational; will be replaced by validation result
                return await ValidateWithAutoFixAsync(session, languageService, ct);
            }

            // Map
            var customizationRoot = await languageService.GetCustomizationRootAsync(session, session.NewGeneratedPath, ct);
            var impacts = await languageService.AnalyzeCustomizationImpactAsync(session, customizationRoot, session.ApiChanges, ct);
            session.ImpactedCustomizations = impacts;
            session.LastStage = UpdateStage.Mapped;
            session.Status = "Mapped";
            if (impacts.Count == 0)
            {
                // Short-circuit: no impacted customization files -> skip propose and validate
                session.Status = "NoCustomizationImpact"; // informational
                return await ValidateWithAutoFixAsync(session, languageService, ct);
            }

            // Propose
            var patches = await languageService.ProposePatchesAsync(session, impacts, ct);
            session.ProposedPatches = patches;
            session.LastStage = UpdateStage.PatchesProposed;
            session.Status = "PatchesProposed";
            if (patches.Count == 0)
            {
                session.Status = "NoPatchesProposed"; // informational
                return await ValidateWithAutoFixAsync(session, languageService, ct);
            }

            // Patches exist (not yet applied in this simplified pipeline) -> proceed to validation
            return await ValidateWithAutoFixAsync(session, languageService, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unified update failed");
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = ex.GetType().Name };
        }
    }

    private static async Task<TspClientUpdateResponse> ValidateWithAutoFixAsync(ClientUpdateSessionState session, IClientUpdateLanguageService languageService, CancellationToken ct)
    {
        const int MAX_ATTEMPTS = 3;
        while (true)
        {
            var result = await languageService.ValidateAsync(session, ct);
            session.ValidationAttemptCount++;
            session.ValidationSuccess = result.Success;
            session.ValidationErrors = result.Errors;
            session.LastStage = UpdateStage.Validated;
            session.Status = result.Success ? "Validated" : "ValidationFailed";
            if (result.Success)
            {
                return CompleteClientUpdate(session, "Update pipeline complete.");
            }
            if (session.ValidationAttemptCount >= MAX_ATTEMPTS)
            {
                session.RequiresManualIntervention = true;
                return CompleteClientUpdate(session, "Validation failed – manual intervention required.");
            }
            var fixes = await languageService.ProposeFixesAsync(session, result.Errors, ct);
            if (fixes.Count == 0)
            {
                session.RequiresManualIntervention = true;
                return CompleteClientUpdate(session, "Validation failed and no fixes could be proposed – manual intervention required.");
            }
            session.ProposedPatches.AddRange(fixes);
        }
    }

    private static TspClientUpdateResponse CompleteClientUpdate(ClientUpdateSessionState s, string message) => new()
    {
        Session = s,
    Message = message
    };

    private async Task HandleUnified(InvocationContext ctx, CancellationToken ct)
    {
        var spec = ctx.ParseResult.GetValueForArgument(specPathArg);
        var packagePath = ctx.ParseResult.GetValueForOption(SharedOptions.PackagePath) ?? Directory.GetCurrentDirectory();
        try
        {
            var svc = languageServiceFactory(packagePath);
            var resp = await UnifiedUpdateAsync(spec, svc, packagePath, ct);
            output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI update failed");
            output.OutputError(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "UnifiedUpdateFailed" });
        }
    }
}
