// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.IO;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Update;
using Microsoft.Extensions.Logging.Abstractions;

namespace Azure.Sdk.Tools.Cli.Tools;

[McpServerToolType, Description("Update customized SDK code after TypeSpec regeneration: creates/continues a session, diffs old vs new generated code, maps API changes to impacted customization files, applies patches.")]
public class TspClientUpdateTool : MCPTool
{
    private readonly ILogger<TspClientUpdateTool> logger;
    private readonly IOutputService output;

    // --- CLI options/args ---
    private readonly Argument<string> _specPathArg = new(name: "spec-path", description: "Path to the .tsp specification file") { Arity = ArgumentArity.ExactlyOne };
    // Removed --session option (single implicit session). Kept placeholder variable (unused) to minimize diff if reintroduced later.
    // private readonly Option<string> _sessionIdOpt = new(["--session", "-s"], () => string.Empty, "Existing session id (omit to create a new session)");
    private readonly Option<string> _languageOpt = new(["--language", "-l"], () => "java", "Target language (e.g. java, csharp)");
    private readonly Option<string> _stageOpt = new(["--stage"], () => string.Empty, "Run only a specific stage (regenerate,diff,map,merge,propose,apply,validate,all)");
    private readonly Option<bool> _resumeOpt = new(["--resume"], () => false, "Resume from existing session state");
    private readonly Option<bool> _finalizeOpt = new(["--finalize"], () => false, "When applying, perform final (non-dry-run) apply if a dry-run occurred");
    // Old/new generated code roots: old = current code baseline (before regeneration), new = location future regenerate will output to
    private readonly Option<string?> _generatedOldOpt = new(["--old-gen"], description: "Path to existing generated package currently customized (baseline for diff)");
    private readonly Option<string?> _generatedNewOpt = new(["--new-gen"], description: "Path to directory where new TypeSpec generation output will be produced");

    // Simplified session handling: single in-memory session only (no disk persistence)
    // TODO(#11645): Reintroduce pluggable session store (file/remote/memory) with manifest + pruning.
    private UpdateSessionState? _currentSession;

    private readonly IEnumerable<IUpdateLanguageService> _languageServices;

    public TspClientUpdateTool(ILogger<TspClientUpdateTool> logger, IOutputService output, IEnumerable<IUpdateLanguageService>? languageServices = null)
    {
        this.logger = logger;
        this.output = output;
        _languageServices = languageServices ?? new List<IUpdateLanguageService> { new JavaUpdateLanguageService() };
        CommandHierarchy = [ SharedCommandGroups.Tsp ];
    }

    public override Command GetCommand()
    {
        var cmd = new Command(
            name: "customized-update",
            description: "Update customized TypeSpec-generated client code. Stages: regenerate -> diff -> apply (dry-run + finalize). Use --stage to run one stage; omit to run available stages in order; use --finalize to complete apply after a dry-run.");
        cmd.AddArgument(_specPathArg);
        cmd.AddOption(_languageOpt);
        cmd.AddOption(_stageOpt);
        cmd.AddOption(_resumeOpt);
        cmd.AddOption(_finalizeOpt);
        cmd.AddOption(_generatedOldOpt);
        cmd.AddOption(_generatedNewOpt);
        cmd.SetHandler(async ctx => { await HandleUnified(ctx, ctx.GetCancellationToken()); });
        return cmd;
    }

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct) => await Task.CompletedTask;

    // --------------- MCP Methods ---------------
    [McpServerTool(Name = "azsdk_tsp_update"), Description("Unified update-customize-code workflow (supports stage / resume)")] 
    public async Task<TspClientUpdateResponse> UnifiedUpdate(string specPath, string? stage = null, bool resume = false, bool finalize = false, CancellationToken ct = default)
    {
        try
        {
            var session = resume ? _currentSession : null;
            if (session == null)
            {
                session = GetOrCreateSession(null);
            }
            if (!resume)
            {
                // Reset for a fresh run if not resuming
                session.CompletedStages.Clear();
                session.RequiresFinalize = false;
                session.ApiChanges.Clear();
            }
            if (!string.IsNullOrWhiteSpace(specPath)) { session.SpecPath = specPath; }
            var normalizedStage = string.IsNullOrWhiteSpace(stage) ? "all" : stage.ToLowerInvariant();
            var runAll = normalizedStage is "all" or "";
            var ordered = new List<string> { "regenerate", "diff", "map", "merge", "propose", "apply", "validate" };
            string? nextStage = null;
            bool terminal = false;
            bool needsFinalize = false;
            foreach (var s in ordered)
            {
                if (!runAll && !string.Equals(s, normalizedStage, StringComparison.OrdinalIgnoreCase))
                {
                    // Skip stages not requested
                    continue;
                }
                // Enforce stage order
                if (s == "diff" && session.LastStage < UpdateStage.Regenerated) { nextStage = "regenerate"; break; }
                if (s == "map" && session.LastStage < UpdateStage.Diffed) { nextStage = "diff"; break; }
                if (s == "merge" && session.LastStage < UpdateStage.Mapped) { nextStage = "map"; break; }
                if (s == "propose" && session.LastStage < UpdateStage.Merged) { nextStage = "merge"; break; }
                if (s == "apply" && session.LastStage < UpdateStage.PatchesProposed) { nextStage = "propose"; break; }
                if (s == "validate" && session.LastStage < UpdateStage.Applied) { nextStage = session.LastStage == UpdateStage.AppliedDryRun ? "apply" : "apply"; break; }
                switch (s)
                {
                    case "regenerate":
                        if (session.LastStage < UpdateStage.Regenerated || !resume || runAll)
                        {
                            var r = await RegenerateCore(session.SpecPath, session.SessionId, null, ct);
                            if (!string.IsNullOrEmpty(r.ResponseError))
                            {
                                return r;
                            }
                            session = r.Session!;
                        }
                        break;
                    case "diff":
                        if (session.LastStage < UpdateStage.Diffed || runAll)
                        {
                            var d = await DiffCore(session.SessionId, null, null, ct);
                            if (!string.IsNullOrEmpty(d.ResponseError))
                            {
                                return d;
                            }
                            session = d.Session!;
                        }
                        if (session.ApiChangeCount == 0)
                        {
                            terminal = true; // nothing else to do
                            goto Finish;
                        }
                        break;
                    case "map":
                        if (session.ApiChangeCount == 0)
                        {
                            terminal = true; goto Finish;
                        }
                        if (session.LastStage < UpdateStage.Mapped || runAll)
                        {
                            var m = await MapCore(session.SessionId, ct);
                            if (!string.IsNullOrEmpty(m.ResponseError)) { return m; }
                            session = m.Session!;
                        }
                        break;
                    case "merge":
                        if (session.ApiChangeCount == 0)
                        {
                            terminal = true; goto Finish;
                        }
                        if (session.LastStage < UpdateStage.Merged || runAll)
                        {
                            var mg = await MergeCore(session.SessionId, ct);
                            if (!string.IsNullOrEmpty(mg.ResponseError)) { return mg; }
                            session = mg.Session!;
                        }
                        break;
                    case "propose":
                        if (session.ApiChangeCount == 0)
                        {
                            terminal = true; goto Finish;
                        }
                        if (session.LastStage < UpdateStage.PatchesProposed || runAll)
                        {
                            var p = await ProposeCore(session.SessionId, null, ct);
                            if (!string.IsNullOrEmpty(p.ResponseError)) { return p; }
                            session = p.Session!;
                        }
                        break;
                    case "apply":
                        if (session.ApiChangeCount == 0) { terminal = true; goto Finish; }
                        if (session.LastStage < UpdateStage.PatchesProposed)
                        {
                            nextStage = "propose"; break;
                        }
                        if (session.LastStage < UpdateStage.AppliedDryRun)
                        {
                            var aDry = ApplyCore(session.SessionId, dryRun: true, ct);
                            if (!string.IsNullOrEmpty(aDry.ResponseError))
                            {
                                return aDry;
                            }
                            session = aDry.Session!;
                            needsFinalize = true;
                            if (!finalize)
                            {
                                // Stop after dry-run in all-mode unless finalize requested
                                if (runAll) { nextStage = "apply"; }
                                goto Finish;
                            }
                        }
                        if (finalize && session.LastStage == UpdateStage.AppliedDryRun)
                        {
                            var a = ApplyCore(session.SessionId, dryRun: false, ct);
                            if (!string.IsNullOrEmpty(a.ResponseError))
                            {
                                return a;
                            }
                            session = a.Session!;
                            needsFinalize = false;
                        }
                        // don't mark terminal yet; allow validate stage
                        break;
                    case "validate":
                        if (session.LastStage < UpdateStage.Applied)
                        {
                            nextStage = session.LastStage == UpdateStage.AppliedDryRun ? "apply" : "apply"; break;
                        }
                        if (session.LastStage < UpdateStage.Validated || runAll)
                        {
                            var v = await ValidateCore(session.SessionId, ct);
                            if (!string.IsNullOrEmpty(v.ResponseError)) { return v; }
                            session = v.Session!;
                        }
                        terminal = true;
                        break;
                }
                if (!runAll)
                {
                    break; // single stage mode
                }
            }
        Finish:
            if (nextStage == null && !terminal)
            {
                nextStage = needsFinalize ? "apply" : (session.LastStage == UpdateStage.Applied ? "validate" : null);
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
            logger.LogError(ex, "Unified update failed for spec {specPath}", specPath);
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
            // placeholder : no real generation invoke logic here, delegate to TspClientTool update/regenerate command and populate session paths.
            session.Status = "Regenerated";
            session.LastStage = UpdateStage.Regenerated;
            session.UpdatedUtc = DateTime.UtcNow;
            return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = $"Regenerate placeholder complete (language={session.Language})", NextStep = ComputeNextStep(session) });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Regenerate failed: {specPath}", specPath);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "RegenerateFailed" });
        }
    }

    private async Task<TspClientUpdateResponse> DiffCore(string sessionId, string? oldGeneratedPath, string? newGeneratedPath, CancellationToken ct)
    {
        try
        {
            var session = RequireSession(sessionId);
            EnforceStageOrder(session, UpdateStage.Regenerated, "diff");
            // Defensive: ensure language still set (some tests invoke MCP methods directly)
            if (string.IsNullOrWhiteSpace(session.Language))
            {
                var firstLang = _languageServices.FirstOrDefault();
                if (firstLang != null) { session.Language = firstLang.Language; }
            }
            var ls = EnsureLanguageService(session);
            var oldSymbols = new Dictionary<string, SymbolInfo>();
            var newSymbols = new Dictionary<string, SymbolInfo>();
            // TODO: populate oldSymbols/newSymbols via ExtractSymbolsAsync once baseline paths wired
            var apiChanges = await ls.DiffAsync(oldSymbols, newSymbols);
            session.ApiChanges = apiChanges;
            session.ApiChangeCount = apiChanges.Count;
            // Reset mapping results (handled in map stage)
            session.ImpactedCustomizations = new List<CustomizationImpact>();
            session.ImpactedCount = 0;
            session.Status = "Diffed";
            session.LastStage = UpdateStage.Diffed;
            session.UpdatedUtc = DateTime.UtcNow;
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
            logger.LogError(ex, "Diff failed: {sessionId}", sessionId);
            SetFailure();
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "DiffFailed" };
        }
    }

    private TspClientUpdateResponse ApplyCore(string sessionId, bool dryRun, CancellationToken ct)
    {
        try
        {
            var session = RequireSession(sessionId);
            if (session.LastStage < UpdateStage.PatchesProposed)
            {
                throw new StageOrderException("Cannot run 'apply' before proposing patches.", "InvalidStageOrder", ComputeNextStep(session) ?? "Run propose");
            }
            if (dryRun)
            {
                session.PatchesAppliedSuccess = session.ProposedPatches.Count;
                session.PatchesAppliedFailed = 0;
            }
            session.Status = dryRun ? "AppliedDryRun" : "Applied";
            session.LastStage = dryRun ? UpdateStage.AppliedDryRun : UpdateStage.Applied;
            session.UpdatedUtc = DateTime.UtcNow;
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
            logger.LogError(ex, "Apply failed: {sessionId}", sessionId);
            SetFailure();
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "ApplyFailed" };
        }
    }

    private Task<TspClientUpdateResponse> MapCore(string sessionId, CancellationToken ct)
    {
        try
        {
            var session = RequireSession(sessionId);
            EnforceStageOrder(session, UpdateStage.Diffed, "map");
            if (session.ApiChangeCount > 0)
            {
                var ls = EnsureLanguageService(session);
                var generationRoot = session.NewGeneratedPath;
                if (!string.IsNullOrWhiteSpace(generationRoot) && Directory.Exists(generationRoot))
                {
                    var root = ls.GetCustomizationRootAsync(session, generationRoot, ct).GetAwaiter().GetResult();
                    if (!string.IsNullOrWhiteSpace(root))
                    {
                        session.CustomizationRoot = root;
                        var impacts = ls.AnalyzeCustomizationImpactAsync(session, root, session.ApiChanges, ct).GetAwaiter().GetResult();
                        if (impacts != null)
                        {
                            session.ImpactedCustomizations = impacts;
                            session.ImpactedCount = impacts.Count;
                        }
                    }
                }
            }
            session.Status = "Mapped";
            session.LastStage = UpdateStage.Mapped;
            session.UpdatedUtc = DateTime.UtcNow;
        return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = "Map stage complete (placeholder).", NextStep = ComputeNextStep(session) });
        }
        catch (StageOrderException sox)
        {
        return Task.FromResult(new TspClientUpdateResponse { ResponseError = sox.Message, ErrorCode = sox.Code, NextStep = sox.SuggestedCommand });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Map failed: {sessionId}", sessionId);
            SetFailure();
        return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "MapFailed" });
        }
    }

    private Task<TspClientUpdateResponse> MergeCore(string sessionId, CancellationToken ct)
    {
        try
        {
            var session = RequireSession(sessionId);
            EnforceStageOrder(session, UpdateStage.Mapped, "merge");
            var ls = EnsureLanguageService(session);
            var direct = ls.DetectDirectMergeFilesAsync(session, session.CustomizationRoot, ct).GetAwaiter().GetResult();
            session.DirectMergeFiles = direct;
            session.Status = "Merged";
            session.LastStage = UpdateStage.Merged;
            session.UpdatedUtc = DateTime.UtcNow;
        return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = "Merge stage complete (placeholder).", NextStep = ComputeNextStep(session) });
        }
        catch (StageOrderException sox)
        {
        return Task.FromResult(new TspClientUpdateResponse { ResponseError = sox.Message, ErrorCode = sox.Code, NextStep = sox.SuggestedCommand });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Merge failed: {sessionId}", sessionId);
            SetFailure();
        return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "MergeFailed" });
        }
    }

    private Task<TspClientUpdateResponse> ProposeCore(string sessionId, string? filesCsv, CancellationToken ct)
    {
        try
        {
            var session = RequireSession(sessionId);
            EnforceStageOrder(session, UpdateStage.Merged, "propose");
            session.ProposedPatches.Clear();
            var ls = EnsureLanguageService(session);
            var impacts = session.ImpactedCustomizations;
            if (!string.IsNullOrWhiteSpace(filesCsv))
            {
                var filter = filesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
                impacts = impacts.Where(i => filter.Contains(i.File)).ToList();
            }
            var proposals = ls.ProposePatchesAsync(session, impacts, session.DirectMergeFiles, ct).GetAwaiter().GetResult();
            session.ProposedPatches.AddRange(proposals);
            session.Status = "PatchesProposed";
            session.LastStage = UpdateStage.PatchesProposed;
            session.UpdatedUtc = DateTime.UtcNow;
            return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = "Propose stage complete (placeholder).", NextStep = ComputeNextStep(session) });
        }
        catch (StageOrderException sox)
        {
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = sox.Message, ErrorCode = sox.Code, NextStep = sox.SuggestedCommand });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Propose failed: {sessionId}", sessionId);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "ProposeFailed" });
        }
    }

    private Task<TspClientUpdateResponse> ValidateCore(string sessionId, CancellationToken ct)
    {
        try
        {
            var session = RequireSession(sessionId);
            if (session.LastStage < UpdateStage.Applied)
            {
                throw new StageOrderException("Cannot validate before apply is finalized.", "InvalidStageOrder", ComputeNextStep(session) ?? "Run apply");
            }
            var ls = EnsureLanguageService(session);
            var (success, errors) = ls.ValidateAsync(session, ct).GetAwaiter().GetResult();
            session.ValidationErrors = errors;
            session.ValidationSuccess = success;
            session.Status = success ? "Validated" : "ValidationFailed";
            session.LastStage = UpdateStage.Validated;
            session.UpdatedUtc = DateTime.UtcNow;
            return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = success ? "Validation succeeded." : $"Validation failed: {errors.Count} error(s)." });
        }
        catch (StageOrderException sox)
        {
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = sox.Message, ErrorCode = sox.Code, NextStep = sox.SuggestedCommand });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Validate failed: {sessionId}", sessionId);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "ValidateFailed" });
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
            // force option removed
            var lang = ctx.ParseResult.GetValueForOption(_languageOpt);
            var oldGen = ctx.ParseResult.GetValueForOption(_generatedOldOpt);
            var newGen = ctx.ParseResult.GetValueForOption(_generatedNewOpt);
            if (!string.IsNullOrWhiteSpace(oldGen) || !string.IsNullOrWhiteSpace(newGen) || !string.IsNullOrWhiteSpace(lang))
            {
                // Ensure session exists so we can stash paths/language prior to unified workflow reset logic
                var s = GetOrCreateSession(null);
                if (!string.IsNullOrWhiteSpace(lang)) { s.Language = lang; }
                if (!string.IsNullOrWhiteSpace(oldGen)) { s.OldGeneratedPath = Path.GetFullPath(oldGen!); }
                if (!string.IsNullOrWhiteSpace(newGen)) { s.NewGeneratedPath = Path.GetFullPath(newGen!); }
            }
            var resp = await UnifiedUpdate(spec, stage, resume, finalize, ct);
            ctx.ExitCode = ExitCode; output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI unified update failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.OutputError(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "UnifiedFailed" });
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

    private UpdateSessionState RequireSession(string sessionId)
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

    internal sealed class StageOrderException(string message, string code, string suggestedCommand) : Exception(message)
    {
        public string Code { get; } = code;
        public string SuggestedCommand { get; } = suggestedCommand;
    }

    private void EnforceStageOrder(UpdateSessionState session, UpdateStage requiredPriorStage, string currentCommand)
    {
        if (session.LastStage < requiredPriorStage)
        {
            var needed = requiredPriorStage.ToString();
            var suggestion = ComputeNextStep(session) ?? "(run previous stage)";
            throw new StageOrderException($"Cannot run '{currentCommand}' before completing stage '{needed}'.", "InvalidStageOrder", suggestion);
        }
    }

    private string? ComputeNextStep(UpdateSessionState session)
    {
        return session.LastStage switch
        {
            UpdateStage.Regenerated => "Run diff",
            UpdateStage.Diffed => session.ApiChangeCount == 0 ? null : "Run map",
            UpdateStage.Mapped => "Run merge",
            UpdateStage.Merged => "Run propose",
            UpdateStage.PatchesProposed => "Run apply (dry-run)",
            UpdateStage.AppliedDryRun => "Re-run apply to finalize",
            UpdateStage.Applied => "Run validate",
            _ => null
        };
    }

    private IUpdateLanguageService ResolveLanguageService(string lang)
    {
        var svc = _languageServices.FirstOrDefault(s => string.Equals(s.Language, lang, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No language service registered for '{lang}'");
        return svc;
    }

    private IUpdateLanguageService EnsureLanguageService(UpdateSessionState session)
    {
        if (session.LanguageService != null)
        {
            if (!string.Equals(session.LanguageService.Language, session.Language, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Session language changed from '{session.LanguageService.Language}' to '{session.Language}' after service resolution; mid-session language changes are unsupported.");
            }
            return session.LanguageService;
        }
        var resolved = ResolveLanguageService(session.Language);
        session.LanguageService = resolved; // cache
        return resolved;
    }
}
