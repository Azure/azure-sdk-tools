// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Linq;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Update;
using Update = Azure.Sdk.Tools.Cli.Services.Update;

namespace Azure.Sdk.Tools.Cli.Tools;

[McpServerToolType, Description("Update customized SDK code after TypeSpec regeneration: creates/continues a session, diffs old vs new generated code, maps API changes to impacted customization files, applies patches.")]
public class TspClientUpdateTool : MCPTool
{
    private readonly ILogger<TspClientUpdateTool> _logger;
    private readonly IOutputService _output;
    private readonly Func<string, Update.IUpdateLanguageService> _languageServiceFactory;

    // --- CLI options/args ---
    private readonly Argument<string> _specPathArg = new(name: "spec-path", description: "Path to the .tsp specification file") { Arity = ArgumentArity.ExactlyOne };
    // Removed --session option (single implicit session). Kept placeholder variable (unused) to minimize diff if reintroduced later.
    // private readonly Option<string> _sessionIdOpt = new(["--session", "-s"], () => string.Empty, "Existing session id (omit to create a new session)");
    private readonly Option<TspStageSelection> _stageOpt = new(["--stage"], description: "The stage to run (regenerate|diff|apply|all)") { IsRequired = true };
    private readonly Option<bool> _resumeOpt = new(["--resume"], () => false, "Resume from existing session state");
    private readonly Option<bool> _finalizeOpt = new(["--finalize"], () => false, "When applying, perform final (non-dry-run) apply if a dry-run occurred");
    // Old/new generated code roots: old = current code baseline (before regeneration), new = location future regenerate will output to
    private readonly Option<string?> _generatedNewOpt = new(["--new-gen"], () => "./tmpgen", "Path to directory where new TypeSpec generation output will be produced");

    // Simplified session handling: single in-memory session only (no disk persistence)
    // TODO(#11645): Reintroduce pluggable session store (file/remote/memory) with manifest + pruning.
    private UpdateSessionState? _currentSession;

    public TspClientUpdateTool(ILogger<TspClientUpdateTool> logger, IOutputService output, Func<string, Update.IUpdateLanguageService> languageServiceFactory)
    {
    _logger = logger;
    _output = output;
        _languageServiceFactory = languageServiceFactory;
        CommandHierarchy = [ SharedCommandGroups.Tsp ];
    }

    public override Command GetCommand()
    {
        var cmd = new Command(
            name: "customized-update",
            description: "Update customized TypeSpec-generated client code. Stages: regenerate -> diff -> apply (dry-run + finalize). Use --stage to run one stage; omit to run available stages in order; use --finalize to complete apply after a dry-run.");
        cmd.AddArgument(_specPathArg);
        cmd.AddOption(SharedOptions.PackagePath);
        cmd.AddOption(_stageOpt);
        cmd.AddOption(_resumeOpt);
        cmd.AddOption(_finalizeOpt);
        cmd.AddOption(_generatedNewOpt);
        cmd.SetHandler(async ctx => { await HandleUnified(ctx, ctx.GetCancellationToken()); });

        return cmd;
    }

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct) => await Task.CompletedTask;

    // --------------- MCP Methods ---------------
    [McpServerTool(Name = "azsdk_tsp_update"), Description("Unified update-customize-code workflow")] 
    public async Task<TspClientUpdateResponse> UnifiedUpdate(string specPath, IUpdateLanguageService languageService, TspStageSelection stage = TspStageSelection.All, bool resume = false, bool finalize = false, CancellationToken ct = default)
    {
            try
            {
            var session = resume ? _currentSession : null;
            if (session == null)
            {
                session = GetOrCreateSession(null);
            }
            // Ensure session records the language used (derived from the injected language service)
            session.Language = languageService.Language;
            if (!resume)
            {
                // Reset for a fresh run if not resuming
                session.RequiresFinalize = false;
                session.ApiChanges.Clear();
            }
            if (!string.IsNullOrWhiteSpace(specPath)) { session.SpecPath = specPath; }
            var runAll = stage == TspStageSelection.All;
            var ordered = new List<TspStageSelection> { TspStageSelection.Regenerate, TspStageSelection.Diff, TspStageSelection.Apply };
            string? nextStage = null;
            bool terminal = false;
            bool needsFinalize = false;
            
            foreach (var stageToRun in ordered)
            {
                if (!runAll && stageToRun != stage)
                {
                    // Skip stages not requested
                    continue;
                }
                // Enforce stage order via policy
                if (!StagePolicy.CanRun(session, stageToRun, out nextStage))
                {
                    break;
                }
                switch (stageToRun)
                {
                    case TspStageSelection.Regenerate:
                        if (StagePolicy.ShouldRun(session, TspStageSelection.Regenerate, resume, runAll))
                        {
                            var regenerateResult = await RegenerateCore(session.SpecPath, session.SessionId, null, ct);
                            if (!string.IsNullOrEmpty(regenerateResult.ResponseError))
                            {
                                return regenerateResult;
                            }
                            session = regenerateResult.Session!;
                        }
                        break;
                    case TspStageSelection.Diff:
                        if (StagePolicy.ShouldRun(session, TspStageSelection.Diff, resume, runAll))
                        {
                            var diffResult = await DiffCore(session.SessionId, languageService, null, null, ct);
                            if (!string.IsNullOrEmpty(diffResult.ResponseError))
                            {
                                return diffResult;
                            }
                            session = diffResult.Session!;
                        }
                        if (session.ApiChangeCount == 0)
                        {
                            terminal = true; // nothing else to do
                            goto Finish;
                        }
                        // Map + Propose even when only 'diff' is requested (single-file flow chains these steps)
                        {
                            var mapResult = await MapCore(session.SessionId, languageService, ct);
                            if (!string.IsNullOrEmpty(mapResult.ResponseError)) { return mapResult; }
                            session = mapResult.Session!;

                            var proposeResult = await ProposeCore(session.SessionId, languageService, null, ct);
                            if (!string.IsNullOrEmpty(proposeResult.ResponseError)) { return proposeResult; }
                            session = proposeResult.Session!;

                            // In single-stage mode suggest the next stage explicitly
                            if (!runAll)
                            {
                                nextStage = StagePolicy.ToStageKeyword(TspStageSelection.Apply);
                            }
                        }
                        break;
                    case TspStageSelection.Apply:
                        if (session.ApiChangeCount == 0) { terminal = true; goto Finish; }
                        if (StagePolicy.ShouldRun(session, TspStageSelection.Apply, resume, runAll) && session.LastStage < UpdateStage.AppliedDryRun)
                        {
                            var applyDryRunResult = ApplyCore(session.SessionId, dryRun: true, ct);
                            if (!string.IsNullOrEmpty(applyDryRunResult.ResponseError))
                            {
                                return applyDryRunResult;
                            }
                            session = applyDryRunResult.Session!;
                            needsFinalize = true;
                            if (!finalize)
                            {
                                // Stop after dry-run in all-mode unless finalize requested
                                if (runAll) { nextStage = StagePolicy.ToStageKeyword(TspStageSelection.Apply); }
                                goto Finish;
                            }
                        }
                        if (finalize && session.LastStage == UpdateStage.AppliedDryRun)
                        {
                            var applyFinalResult = ApplyCore(session.SessionId, dryRun: false, ct);
                            if (!string.IsNullOrEmpty(applyFinalResult.ResponseError))
                            {
                                return applyFinalResult;
                            }
                            session = applyFinalResult.Session!;
                            needsFinalize = false;
                        }
                        // don't mark terminal yet; allow validate stage
                        if (session.LastStage < UpdateStage.Applied)
                        {
                            nextStage = StagePolicy.ToStageKeyword(TspStageSelection.Apply); break;
                        }
                        // Validate stage
                        if (session.LastStage < UpdateStage.Validated)
                        {
                            var validateResult = await ValidateCore(session.SessionId, languageService, ct);
                            if (!string.IsNullOrEmpty(validateResult.ResponseError)) { return validateResult; }
                            session = validateResult.Session!;
                        }
                        terminal = true;
                        goto Finish;
                }
                if (!runAll)
                {
                    break; // single stage mode
                }
            }
        Finish:
            if (nextStage == null && !terminal)
            {
                nextStage = StagePolicy.NextHintAfter(session, runAll, needsFinalize);
            }
            return new TspClientUpdateResponse
            {
                Session = session,
                Message = terminal ? "Unified update complete." : "Unified update stage(s) executed.",
                NextStage = nextStage,
                NeedsFinalize = needsFinalize ? true : null,
                Terminal = terminal ? true : null
            };
        }
            catch (Exception ex)
        {
            _logger.LogError(ex, "Unified update failed for spec {specPath}", specPath);
            SetFailure();
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "UnifiedUpdateFailed" };
        }
    }

    // --------------- Internal Stage Methods ---------------
    private Task<TspClientUpdateResponse> RegenerateCore(string specPath, string? sessionId, string? newGeneratedPath, CancellationToken ct)
    {
            try
            {
            var session = GetOrCreateSession(sessionId);
            // placeholder : no real generation invoke logic here, delegate to TspClientTool/Service to update/regenerate command and populate session paths.
            // should regenerate to <newGeneratedPath> location
            session.Status = "Regenerated";
            session.LastStage = UpdateStage.Regenerated;
            // session timestamp removed for leaner session
            return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = $"Regenerate complete (language={session.Language})", NextStep = ComputeNextStep(session) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Regenerate failed: {specPath}", specPath);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "RegenerateFailed" });
        }
    }

    private async Task<TspClientUpdateResponse> DiffCore(string sessionId, IUpdateLanguageService languageService, string? oldGeneratedPath, string? newGeneratedPath, CancellationToken ct)
    {
        try
        {
            var session = RequireSession(sessionId);
            StagePolicy.EnsurePrereqOrThrow(session, UpdateStage.Regenerated, "diff", ComputeNextStep);
            var oldSymbols = new Dictionary<string, SymbolInfo>();
            var newSymbols = new Dictionary<string, SymbolInfo>();
            // TODO: populate oldSymbols/newSymbols via ExtractSymbolsAsync once baseline paths wired

            var apiChanges = await languageService.DiffAsync(oldSymbols, newSymbols);
            session.ApiChanges = apiChanges;
            session.ApiChangeCount = apiChanges.Count;
            // Reset mapping results (handled in map stage)
            session.ImpactedCustomizations = new List<CustomizationImpact>();
            session.Status = "Diffed";
            session.LastStage = UpdateStage.Diffed;
            // session timestamp removed for leaner session
            var mapSummary = session.ApiChangeCount == 0 ? "no changes" : "changes detected";
            return new TspClientUpdateResponse
            {
                Session = session,
                Message = $"Diff stage complete: {apiChanges.Count} API changes ({mapSummary})",
                NextStep = ComputeNextStep(session)
            };
        }
        catch (StageOrderException sox)
        {
            return new TspClientUpdateResponse { ResponseError = sox.Message, ErrorCode = sox.Code, NextStep = sox.SuggestedCommand };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Diff failed: {sessionId}", sessionId);
            SetFailure();
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "DiffFailed" };
        }
    }

    private TspClientUpdateResponse ApplyCore(string sessionId, bool dryRun, CancellationToken ct)
    {
        try
        {
            var session = RequireSession(sessionId);
            StagePolicy.EnsurePrereqOrThrow(session, UpdateStage.PatchesProposed, "apply", ComputeNextStep);
            // skip tracking per-file apply counts in lean mode
            session.Status = dryRun ? "AppliedDryRun" : "Applied";
            session.LastStage = dryRun ? UpdateStage.AppliedDryRun : UpdateStage.Applied;
            // session timestamp removed for leaner session
            if (dryRun) { session.RequiresFinalize = true; }
            else { session.RequiresFinalize = false; }
            return new TspClientUpdateResponse { Session = session, Message = dryRun ? "Apply dry-run complete (placeholder)." : "Apply finalized (placeholder)." };
        }
        catch (StageOrderException sox)
        {
            return new TspClientUpdateResponse { ResponseError = sox.Message, ErrorCode = sox.Code, NextStep = sox.SuggestedCommand };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Apply failed: {sessionId}", sessionId);
            SetFailure();
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "ApplyFailed" };
        }
    }

    private async Task<TspClientUpdateResponse> MapCore(string sessionId, IUpdateLanguageService languageService, CancellationToken ct)
    {
        try
        {
            var session = RequireSession(sessionId);
            StagePolicy.EnsurePrereqOrThrow(session, UpdateStage.Diffed, "map", ComputeNextStep);
            if (session.ApiChangeCount > 0)
            {
                var generationRoot = session.NewGeneratedPath;
                if (!string.IsNullOrWhiteSpace(generationRoot) && Directory.Exists(generationRoot))
                {
                    var customizationSource = await languageService.GetCustomizationRootAsync(session, generationRoot, ct).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(customizationSource))
                    {
                        session.CustomizationRoot = customizationSource;
                        var impacts = await languageService.AnalyzeCustomizationImpactAsync(session, customizationSource, session.ApiChanges, ct).ConfigureAwait(false);
                        if (impacts != null)
                        {
                            session.ImpactedCustomizations = impacts;
                            // Removed ImpactedCount assignment
                            // session.ImpactedCount = impacts.Count;
                        }
                    }
                }
            }
            session.Status = "Mapped";
            session.LastStage = UpdateStage.Mapped;
            // session timestamp removed for leaner session
        return new TspClientUpdateResponse { Session = session, Message = "Map stage complete (placeholder).", NextStep = ComputeNextStep(session) };
        }
        catch (StageOrderException sox)
        {
        return new TspClientUpdateResponse { ResponseError = sox.Message, ErrorCode = sox.Code, NextStep = sox.SuggestedCommand };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Internal stage Map failed: {sessionId}", sessionId);
            SetFailure();
        return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "MapFailed" };
        }
    }

    private async Task<TspClientUpdateResponse> ProposeCore(string sessionId, IUpdateLanguageService languageService, string? filesCsv, CancellationToken ct)
    {
        try
        {
            var session = RequireSession(sessionId);
            StagePolicy.EnsurePrereqOrThrow(session, UpdateStage.Mapped, "propose", ComputeNextStep);
            session.ProposedPatches.Clear();
            var impacts = session.ImpactedCustomizations;
            if (!string.IsNullOrWhiteSpace(filesCsv))
            {
                var filter = filesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
                impacts = impacts.Where(i => filter.Contains(i.File)).ToList();
            }
        var proposals = await languageService.ProposePatchesAsync(session, impacts, ct).ConfigureAwait(false);
            session.ProposedPatches.AddRange(proposals);
            session.Status = "PatchesProposed";
            session.LastStage = UpdateStage.PatchesProposed;
            // session timestamp removed for leaner session
            return new TspClientUpdateResponse { Session = session, Message = "Propose stage complete (placeholder).", NextStep = ComputeNextStep(session) };
        }
    catch (StageOrderException sox)
        {
            return new TspClientUpdateResponse { ResponseError = sox.Message, ErrorCode = sox.Code, NextStep = sox.SuggestedCommand };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Internal stage Propose failed: {sessionId}", sessionId);
            SetFailure();
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "ProposeFailed" };
        }
    }

    private async Task<TspClientUpdateResponse> ValidateCore(string sessionId, IUpdateLanguageService languageService, CancellationToken ct)
    {
        try
        {
            var session = RequireSession(sessionId);
            StagePolicy.EnsurePrereqOrThrow(session, UpdateStage.Applied, "validate", ComputeNextStep);

            const int MAX_ATTEMPTS = 3;
            while (true)
            {
                var (success, errors) = await languageService!.ValidateAsync(session, ct).ConfigureAwait(false);
                session.ValidationAttemptCount++;
                session.ValidationErrors = errors;
                session.ValidationSuccess = success;
                if (success)
                {
                    session.Status = "Validated";
                    session.LastStage = UpdateStage.Validated;
                    return new TspClientUpdateResponse { Session = session, Message = "Validation succeeded." };
                }

                // If we've reached max attempts, mark for manual intervention and return failure
                if (session.ValidationAttemptCount >= MAX_ATTEMPTS)
                {
                    session.Status = "ValidationFailed";
                    session.RequiresManualIntervention = true;
                    session.LastStage = UpdateStage.Validated;
                    return new TspClientUpdateResponse { Session = session, Message = $"Validation failed after {session.ValidationAttemptCount} attempts.", NextStage = ComputeNextStep(session) };
                }

                // Ask language service for conservative fixes and apply them to ProposedPatches as additional proposals
                var fixes = await languageService!.ProposeFixesAsync(session, errors, ct).ConfigureAwait(false);
                if (fixes == null || fixes.Count == 0)
                {
                    // No automatic fixes; try repo-level format/lint as a last-ditch attempt if available
                    if (!string.IsNullOrWhiteSpace(session.CustomizationRoot))
                    {
                        try
                        {
                            // best-effort: attempt format and lint helpers exposed by repo (if implemented)
                            var repo = ((Update.UpdateLanguageServiceBase?)languageService)?.GetType();
                            // we intentionally avoid a hard dependency; languages should implement ProposeFixesAsync when possible
                        }
                        catch { }
                    }
                    // No fixes proposed; loop will re-run validation until MAX_ATTEMPTS exhausted
                    continue;
                }

                // Merge fixes into session.ProposedPatches so they are visible/auditable
                session.ProposedPatches.AddRange(fixes);
                // Simulate applying fixes as dry-run by advancing stage but keep LastStage as Applied to allow re-validation
                session.Status = "AppliedDryRunWithFixes";
                // loop back to re-run validation
            }
        }
        catch (StageOrderException sox)
        {
            return new TspClientUpdateResponse { ResponseError = sox.Message, ErrorCode = sox.Code, NextStep = sox.SuggestedCommand };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Internal stageValidate failed: {sessionId}", sessionId);
            SetFailure();
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "ValidateFailed" };
        }
    }
    

    // --------------- Internal Methods ---------------
    private async Task HandleUnified(InvocationContext ctx, CancellationToken ct)
    {
            try
            {
            var spec = ctx.ParseResult.GetValueForArgument(_specPathArg);
            var stage = ctx.ParseResult.GetValueForOption(_stageOpt);
            var resume = ctx.ParseResult.GetValueForOption(_resumeOpt);
            var finalize = ctx.ParseResult.GetValueForOption(_finalizeOpt);
            var packagePath = ctx.ParseResult.GetValueForOption(SharedOptions.PackagePath);

            // Create language service
            IUpdateLanguageService languageService;
            try
            {
                languageService = _languageServiceFactory(Directory.Exists(packagePath) ? packagePath : Path.GetDirectoryName(packagePath)!);
                _logger.LogDebug($"Retrieved language service: {languageService.GetType().Name}");
            }
            catch (Exception ex)
            {
                SetFailure(1);
                _logger.LogError(ex, "Failed to create language service");
                ctx.ExitCode = ExitCode;
                _output.OutputError(new TspClientUpdateResponse { ResponseError = $"Unable to determine language for package at: {packagePath}. Error: {ex.Message}", ErrorCode = "LanguageDetectionFailed" });
                return;
            }
            var oldGen = packagePath;
            var newGen = ctx.ParseResult.GetValueForOption(_generatedNewOpt) ?? "./tmpgen";
            try
            {
                // Ensure the default or provided new-gen directory exists and attach to the session early
                Directory.CreateDirectory(newGen);
                var sess = GetOrCreateSession(null);
                sess.NewGeneratedPath = newGen;
                if (!string.IsNullOrWhiteSpace(oldGen))
                {
                    sess.OldGeneratedPath = oldGen;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to initialize new-gen directory at {newGen}", newGen);
            }

            var resp = await UnifiedUpdate(spec, languageService, stage, resume, finalize, ct);
            ctx.ExitCode = ExitCode; _output.Output(resp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while running update");
            SetFailure();
            ctx.ExitCode = ExitCode; _output.OutputError(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "UnifiedUpdateFailed" });
        }
    }

    private UpdateSessionState GetOrCreateSession(string? sessionId)
    {
        // Ignore provided id for now; single session lifecycle per process.
        if (_currentSession == null)
        {
            _currentSession = new UpdateSessionState();
        }
        return _currentSession;
    }

    private UpdateSessionState RequireSession(string? sessionId)
    {
        if (_currentSession == null)
        {
            throw new InvalidOperationException("No active session. Start one with: azsdk customized-update <spec-path> [--stage regenerate|diff|apply]");
        }
        // If caller supplied a different id, warn (but still allow since only one session exists)
        if (!string.IsNullOrWhiteSpace(sessionId) && !sessionId.Equals(_currentSession.SessionId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Only one in-memory session supported in this build. Active session id: {_currentSession.SessionId}.");
        }
        return _currentSession;
    }

    private string? ComputeNextStep(UpdateSessionState session)
    {
        return session.LastStage switch
        {
            UpdateStage.Regenerated => "Run diff",
            UpdateStage.Diffed => session.ApiChangeCount == 0 ? null : "Run map",
            UpdateStage.Mapped => "Run propose",
            UpdateStage.PatchesProposed => "Run apply (dry-run)",
            UpdateStage.AppliedDryRun => "Re-run apply to finalize",
            UpdateStage.Applied => "Run validate",
            _ => null
        };
    }
}
