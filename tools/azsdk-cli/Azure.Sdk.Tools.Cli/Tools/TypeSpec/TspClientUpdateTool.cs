// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Services.Update;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tools;

[McpServerToolType, Description("Update customized SDK code after TypeSpec regeneration: creates a new generation, diffs old vs new generated code, maps API changes to impacted customization files, applies patches.")]
public class TspClientUpdateTool : MCPTool
{
    private readonly ILogger<TspClientUpdateTool> logger;
    private readonly IOutputHelper output;
    private readonly Func<string, IUpdateLanguageService> languageServiceFactory;
    private readonly Argument<string> specPathArg = new(name: "spec-path", description: "Path to the .tsp specification file") { Arity = ArgumentArity.ExactlyOne };
    private readonly Option<string?> newGenOpt = new(["--new-gen"], () => "./tmpgen", "Directory for regenerated TypeSpec output (optional)");

    public TspClientUpdateTool(ILogger<TspClientUpdateTool> logger, IOutputHelper output, Func<string, IUpdateLanguageService> languageServiceFactory)
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
    public async Task<TspClientUpdateResponse> UnifiedUpdateAsync(string specPath, IUpdateLanguageService languageService, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(specPath))
            {
                return new TspClientUpdateResponse { ResponseError = "Spec path required", ErrorCode = "MissingSpec" };
            }
            var session = new UpdateSessionState { SpecPath = specPath };

            // Regenerate (placeholder)
            session.LastStage = UpdateStage.Regenerated;
            session.Status = "Regenerated";

            // Diff
            var apiChanges = await languageService.DiffAsync(new Dictionary<string, SymbolInfo>(), new Dictionary<string, SymbolInfo>());
            session.ApiChanges = apiChanges;
            session.ApiChangeCount = apiChanges.Count;
            session.LastStage = UpdateStage.Diffed;
            session.Status = "Diffed";
            // Continue pipeline even if no API changes so validation & auto-fix logic can run.

            // Map
            var customizationRoot = session.NewGeneratedPath; // placeholder path usage
            var impacts = await languageService.AnalyzeCustomizationImpactAsync(session, customizationRoot, session.ApiChanges, ct);
            session.ImpactedCustomizations = impacts;
            session.LastStage = UpdateStage.Mapped;
            session.Status = "Mapped";
            // Even if no impacts, continue through remaining stages for a consistent single-pass experience.
            if (impacts.Count == 0)
            {
                session.Status = "NoCustomizationImpact"; // informational; continue
            }

            // Propose
            var patches = await languageService.ProposePatchesAsync(session, impacts, ct);
            session.ProposedPatches = patches;
            session.LastStage = UpdateStage.PatchesProposed;
            session.Status = "PatchesProposed";
            if (patches.Count == 0)
            {
                session.Status = "NoPatchesProposed"; // informational; continue to validation
            }

            // Apply
            session.LastStage = UpdateStage.Applied;
            session.Status = "Applied";

            // Validate (with limited auto-fix loop)
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
                    return Done(session, "Update pipeline complete.");
                }
                if (session.ValidationAttemptCount >= MAX_ATTEMPTS)
                {
                    session.RequiresManualIntervention = true;
                    return Done(session, "Validation failed – manual intervention required.");
                }
                var fixes = await languageService.ProposeFixesAsync(session, result.Errors, ct);
                if (fixes.Count == 0)
                {
                    continue; // retry (simulated) until attempts exhausted
                }
                session.ProposedPatches.AddRange(fixes);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unified update failed");
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = ex.GetType().Name };
        }
    }

    private static TspClientUpdateResponse Done(UpdateSessionState s, string message) => new()
    {
        Session = s,
        Message = message,
        Terminal = true
    };

    private async Task HandleUnified(InvocationContext ctx, CancellationToken ct)
    {
        var spec = ctx.ParseResult.GetValueForArgument(specPathArg);
        var packagePath = ctx.ParseResult.GetValueForOption(SharedOptions.PackagePath) ?? Directory.GetCurrentDirectory();
        try
        {
            var svc = languageServiceFactory(packagePath);
            var resp = await UnifiedUpdateAsync(spec, svc, ct);
            output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI update failed");
            output.OutputError(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "UnifiedUpdateFailed" });
        }
    }
}
