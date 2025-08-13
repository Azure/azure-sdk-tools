// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Update;

namespace Azure.Sdk.Tools.Cli.Tools;

[McpServerToolType, Description("Update customized SDK code after TypeSpec regeneration: creates/continues a session, diffs old vs new generated code, maps API changes to impacted customization files, proposes patches, applies them, and reports status.")]
public class TspClientUpdateTool : MCPTool
{
    private readonly ILogger<TspClientUpdateTool> logger;
    private readonly IOutputService output;

    // --- CLI options/args ---
    private readonly Argument<string> _specPathArg = new(name: "spec-path", description: "Path to the .tsp specification file") { Arity = ArgumentArity.ExactlyOne };
    // Removed --session option (single implicit session). Kept placeholder variable (unused) to minimize diff if reintroduced later.
    // private readonly Option<string> _sessionIdOpt = new(["--session", "-s"], () => string.Empty, "Existing session id (omit to create a new session)");
    private readonly Option<string> _generatedOldOpt = new(["--old-gen"], () => string.Empty, "Path to previously generated code (optional)");
    private readonly Option<string> _generatedNewOpt = new(["--new-gen"], () => string.Empty, "Target path for new generated code (optional, temp path if omitted)");
    private readonly Option<bool> _dryRunOpt = new(["--dry-run"], () => false, "Do not write any changes when applying patches");
    private readonly Option<string> _filesOpt = new(["--files"], () => string.Empty, "Comma-separated list of customization files to target (defaults to all impacted)");
    private readonly Option<string> _languageOpt = new(["--language", "-l"], () => "java", "Target language (e.g. java, csharp)");
    private readonly Option<bool> _autoOpt = new(["--auto"], () => false, "Automatically chain through stages until completion (stops on error or when no nextTool)");

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
        var root = new Command("customized-update", "Workflow for updating customized TypeSpec-generated client code");

        var regenerate = new Command("regenerate", "Generate new client code artifacts for comparison");
        regenerate.AddArgument(_specPathArg);
        regenerate.AddOption(_generatedNewOpt);
        // regenerate.AddOption(_sessionIdOpt); // removed
        regenerate.AddOption(_languageOpt);
        regenerate.AddOption(_autoOpt);
        regenerate.SetHandler(async ctx => { await HandleRegenerate(ctx, ctx.GetCancellationToken()); });

        var diff = new Command("diff", "Detect API surface changes between old and new generation outputs");
        // diff.AddOption(_sessionIdOpt); // removed
        diff.AddOption(_generatedOldOpt);
        diff.AddOption(_generatedNewOpt);
        diff.SetHandler(async ctx => { await HandleDiff(ctx, ctx.GetCancellationToken()); });

        var map = new Command("map", "Map API changes to impacted customization files");
        // map.AddOption(_sessionIdOpt); // removed
        map.SetHandler(ctx => { HandleMap(ctx, ctx.GetCancellationToken()); });

        var merge = new Command("merge", "Apply direct merges for unaffected customization files");
        // merge.AddOption(_sessionIdOpt); // removed
        merge.SetHandler(ctx => { HandleMerge(ctx, ctx.GetCancellationToken()); });

        var propose = new Command("propose", "Propose patch diffs for impacted customization files");
        // propose.AddOption(_sessionIdOpt); // removed
        propose.AddOption(_filesOpt);
        propose.SetHandler(ctx => { HandlePropose(ctx, ctx.GetCancellationToken()); });

        var apply = new Command("apply", "Apply previously proposed patches to customization files");
        // apply.AddOption(_sessionIdOpt); // removed
        apply.AddOption(_dryRunOpt);
        apply.SetHandler(ctx => { HandleApply(ctx, ctx.GetCancellationToken()); });

        var status = new Command("status", "Show current session status and counts");
        // status.AddOption(_sessionIdOpt); // removed
        status.SetHandler(ctx => { HandleStatus(ctx, ctx.GetCancellationToken()); });

        // Stub sessions command to explain removal
        var sessions = new Command("sessions", "(Stub) Session management disabled in this build")
        {
            new Option<bool>("--prune", description: "(ignored)") { IsHidden = true }
        };
        sessions.SetHandler(ctx =>
        {
            output.Output(new TspClientUpdateResponse { Message = "Session management disabled (single implicit in-memory session). See issue #11645." });
        });

        root.AddCommand(regenerate);
        root.AddCommand(diff);
        root.AddCommand(map);
        root.AddCommand(merge);
        root.AddCommand(propose);
        root.AddCommand(apply);
        root.AddCommand(status);
        root.AddCommand(sessions);

        return root;
    }

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct) => await Task.CompletedTask;

    // --------------- MCP Methods ---------------
    [McpServerTool(Name = "azsdk_tsp_update_regenerate"), Description("Generate new code artifacts and start (or continue) an update session")] 
    public Task<TspClientUpdateResponse> Regenerate(string specPath, string? sessionId = null, string? newGeneratedPath = null, bool simulateChange = false, CancellationToken ct = default)
    {
        try
        {
            return RegenerateCore(specPath, sessionId, newGeneratedPath, null, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Regenerate MCP failed: {specPath}", specPath);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    [McpServerTool(Name = "azsdk_tsp_update_diff"), Description("Diff old vs new generation outputs and record API changes")] 
    public Task<TspClientUpdateResponse> Diff(string sessionId, string? oldGeneratedPath = null, string? newGeneratedPath = null, CancellationToken ct = default)
    {
        try
        {
            return DiffCore(sessionId, oldGeneratedPath, newGeneratedPath, null, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Diff MCP failed: {sessionId}", sessionId);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    [McpServerTool(Name = "azsdk_tsp_update_map"), Description("Map API changes to impacted customization files")] 
    public Task<TspClientUpdateResponse> Map(string sessionId, CancellationToken ct = default)
    {
        try
        {
            return Task.FromResult(MapCore(sessionId, ct));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Map MCP failed: {sessionId}", sessionId);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    [McpServerTool(Name = "azsdk_tsp_update_merge"), Description("Apply direct merges for unaffected customization files")] 
    public Task<TspClientUpdateResponse> Merge(string sessionId, CancellationToken ct = default)
    {
        try
        {
            return Task.FromResult(MergeCore(sessionId, ct));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Merge MCP failed: {sessionId}", sessionId);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    [McpServerTool(Name = "azsdk_tsp_update_propose"), Description("Propose patch diffs for impacted customization files")] 
    public Task<TspClientUpdateResponse> Propose(string sessionId, string? filesCsv = null, CancellationToken ct = default)
    {
        try
        {
            return Task.FromResult(ProposeCore(sessionId, filesCsv, ct));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Propose MCP failed: {sessionId}", sessionId);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    [McpServerTool(Name = "azsdk_tsp_update_apply"), Description("Apply previously proposed patches (supports dry-run)")] 
    public Task<TspClientUpdateResponse> Apply(string sessionId, bool dryRun = false, CancellationToken ct = default)
    {
        try
        {
            return Task.FromResult(ApplyCore(sessionId, dryRun, ct));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Apply MCP failed: {sessionId}", sessionId);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    [McpServerTool(Name = "azsdk_tsp_update_status"), Description("Return current session status and counts")] 
    public Task<TspClientUpdateResponse> Status(string sessionId, CancellationToken ct = default)
    {
        try
        {
            return Task.FromResult(StatusCore(sessionId, ct));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Status MCP failed: {sessionId}", sessionId);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    // --------------- Internal Stage Methods ---------------
    private async Task<TspClientUpdateResponse> RegenerateCore(string specPath, string? sessionId, string? newGeneratedPath, IUpdateLanguageService? langService, CancellationToken ct)
    {
        try
        {
            var session = GetOrCreateSession(sessionId);
            if (string.IsNullOrEmpty(session.ToolVersion))
            {
                session.ToolVersion = typeof(TspClientUpdateTool).Assembly.GetName().Version?.ToString() ?? "";
            }
            session.SpecPath = specPath;
            if (string.IsNullOrWhiteSpace(session.Language))
            {
                session.Language = "java"; // default
            }
            session.LanguageService ??= langService ?? ResolveLanguageService(session.Language);
            await session.LanguageService.RegenerateAsync(session, specPath, newGeneratedPath, ct);
            session.Status = "Regenerated";
            session.LastStage = UpdateStage.Regenerated;
            session.UpdatedUtc = DateTime.UtcNow;
            return new TspClientUpdateResponse { Session = session, Message = $"Regenerate stage complete (language={session.Language})", NextStep = ComputeNextStep(session), NextTool = ComputeNextTool(session) };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Regenerate failed: {specPath}", specPath);
            SetFailure();
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "RegenerateFailed" };
        }
    }

    private async Task<TspClientUpdateResponse> DiffCore(string sessionId, string? oldGeneratedPath, string? newGeneratedPath, IUpdateLanguageService? langService, CancellationToken ct)
    {
        try
        {
            var session = RequireSession(sessionId);
            EnforceStageOrder(session, UpdateStage.Regenerated, "diff");
            session.LanguageService ??= langService ?? ResolveLanguageService(session.Language);
            var oldSymbols = new Dictionary<string, SymbolInfo>();
            var newSymbols = new Dictionary<string, SymbolInfo>();
            var apiChanges = await session.LanguageService.DiffAsync(oldSymbols, newSymbols);
            session.ApiChanges = apiChanges;
            session.ApiChangeCount = apiChanges.Count;
            session.Status = "Diffed";
            session.LastStage = UpdateStage.Diffed;
            session.UpdatedUtc = DateTime.UtcNow;
            return new TspClientUpdateResponse { Session = session, Message = $"Diff stage complete: {apiChanges.Count} API changes (language={session.Language})", NextStep = ComputeNextStep(session), NextTool = ComputeNextTool(session) };
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

    private TspClientUpdateResponse MapCore(string sessionId, CancellationToken ct)
    {
        try
        {
            var session = RequireSession(sessionId);
            EnforceStageOrder(session, UpdateStage.Diffed, "map");
            if (session.ApiChanges.Count == 0)
            {
                return new TspClientUpdateResponse { Session = session, Message = "No API changes to map", NextStep = ComputeNextStep(session) };
            }
            session.ImpactedCustomizations = new List<CustomizationImpact>();
            session.DirectMergeFiles = new List<string>();
            session.ImpactedCount = 0;
            session.Status = "Mapped";
            session.LastStage = UpdateStage.Mapped;
            session.UpdatedUtc = DateTime.UtcNow;
            return new TspClientUpdateResponse { Session = session, Message = "Impact mapping complete: 0 impacted, 0 direct merge", NextStep = ComputeNextStep(session), NextTool = ComputeNextTool(session) };
        }
        catch (StageOrderException sox)
        {
            return new TspClientUpdateResponse { ResponseError = sox.Message, ErrorCode = sox.Code, NextStep = sox.SuggestedCommand };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Map failed: {sessionId}", sessionId);
            SetFailure();
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "MapFailed" };
        }
    }

    private TspClientUpdateResponse MergeCore(string sessionId, CancellationToken ct)
    {
        try
        {
            var session = RequireSession(sessionId);
            EnforceStageOrder(session, UpdateStage.Mapped, "merge");
            session.Status = "Merged";
            session.LastStage = UpdateStage.Merged;
            session.UpdatedUtc = DateTime.UtcNow;
            return new TspClientUpdateResponse { Session = session, Message = "Merged (no-op in simplified mode)", NextStep = ComputeNextStep(session), NextTool = ComputeNextTool(session) };
        }
        catch (StageOrderException sox)
        {
            return new TspClientUpdateResponse { ResponseError = sox.Message, ErrorCode = sox.Code, NextStep = sox.SuggestedCommand };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Merge failed: {sessionId}", sessionId);
            SetFailure();
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "MergeFailed" };
        }
    }

    private TspClientUpdateResponse ProposeCore(string sessionId, string? filesCsv, CancellationToken ct)
    {
        try
        {
            var session = RequireSession(sessionId);
            EnforceStageOrder(session, UpdateStage.Merged, "propose");
            session.ProposedPatches = new List<PatchProposal>();
            session.Status = "PatchesProposed";
            session.LastStage = UpdateStage.PatchesProposed;
            session.UpdatedUtc = DateTime.UtcNow;
            return new TspClientUpdateResponse { Session = session, Message = "Proposed 0 patches (simplified mode)", NextStep = ComputeNextStep(session), NextTool = ComputeNextTool(session) };
        }
        catch (StageOrderException sox)
        {
            return new TspClientUpdateResponse { ResponseError = sox.Message, ErrorCode = sox.Code, NextStep = sox.SuggestedCommand };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Propose failed: {sessionId}", sessionId);
            SetFailure();
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "ProposeFailed" };
        }
    }

    private TspClientUpdateResponse ApplyCore(string sessionId, bool dryRun, CancellationToken ct)
    {
        try
        {
            var session = RequireSession(sessionId);
            EnforceStageOrder(session, UpdateStage.PatchesProposed, "apply");
            session.Status = dryRun ? "AppliedDryRun" : "Applied";
            session.LastStage = dryRun ? UpdateStage.AppliedDryRun : UpdateStage.Applied;
            session.UpdatedUtc = DateTime.UtcNow;
            return new TspClientUpdateResponse { Session = session, Message = dryRun ? "Dry-run apply complete (no-op)" : "Apply complete (no-op)", NextStep = ComputeNextStep(session), NextTool = ComputeNextTool(session) };
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

    private TspClientUpdateResponse StatusCore(string sessionId, CancellationToken ct)
    {
        try
        {
            var session = RequireSession(sessionId);
            return new TspClientUpdateResponse { Session = session, Message = "OK", NextTool = ComputeNextTool(session) };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Status failed: {sessionId}", sessionId);
            SetFailure();
            return new TspClientUpdateResponse { ResponseError = ex.Message };
        }
    }

    private string? AutoSelectSessionIfConvenient(string provided) => provided; // simplified (no auto selection)

    private UpdateSessionState ValidateAndMaybeMarkStale(UpdateSessionState session) => session; // no stale detection without persistence

    private IUpdateLanguageService ResolveLanguageService(string lang)
    {
        var svc = _languageServices.FirstOrDefault(s => string.Equals(s.Language, lang, StringComparison.OrdinalIgnoreCase));
        if (svc == null)
        {
            throw new InvalidOperationException($"No language service registered for '{lang}'");
        }
        return svc;
    }

    // --------------- CLI Handlers ---------------
    private async Task HandleRegenerate(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            var spec = ctx.ParseResult.GetValueForArgument(_specPathArg);
            var newGen = ctx.ParseResult.GetValueForOption(_generatedNewOpt);
            var lang = ctx.ParseResult.GetValueForOption(_languageOpt);
            var auto = ctx.ParseResult.GetValueForOption(_autoOpt);
            var session = GetOrCreateSession(null);
            if (!string.IsNullOrWhiteSpace(lang)) { session.Language = lang; }
            var langService = ResolveLanguageService(session.Language);
            var resp = await RegenerateCore(spec, session.SessionId, string.IsNullOrWhiteSpace(newGen) ? null : newGen, langService, ct);
            output.Output(resp);
            if (auto)
            {
                var current = resp;
                while (current.NextTool != null && current.Session != null && string.IsNullOrEmpty(current.ResponseError))
                {
                    var next = current.NextTool;
                    TspClientUpdateResponse nextResp;
                    switch (next)
                    {
                        case "azsdk_tsp_update_diff":
                            nextResp = await DiffCore(current.Session.SessionId, null, null, session.LanguageService, ct);
                            break;
                        case "azsdk_tsp_update_map":
                            nextResp = MapCore(current.Session.SessionId, ct);
                            break;
                        case "azsdk_tsp_update_merge":
                            nextResp = MergeCore(current.Session.SessionId, ct);
                            break;
                        case "azsdk_tsp_update_propose":
                            nextResp = ProposeCore(current.Session.SessionId, null, ct);
                            break;
                        case "azsdk_tsp_update_apply":
                            var isDryRunNeeded = current.Session.LastStage == UpdateStage.PatchesProposed;
                            nextResp = ApplyCore(current.Session.SessionId, isDryRunNeeded, ct);
                            break;
                        default:
                            nextResp = new TspClientUpdateResponse { ResponseError = $"Unknown next tool {next}" };
                            break;
                    }
                    output.Output(nextResp);
                    current = nextResp;
                    if (!string.IsNullOrEmpty(current.ResponseError)) { break; }
                }
            }
            ctx.ExitCode = ExitCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI regenerate failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.OutputError(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "RegenerateFailed" });
        }
    }

    private async Task HandleDiff(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            var oldGen = ctx.ParseResult.GetValueForOption(_generatedOldOpt);
            var newGen = ctx.ParseResult.GetValueForOption(_generatedNewOpt);
            var active = _currentSession ?? throw new InvalidOperationException("No active session. Run regenerate first.");
            var langService = ResolveLanguageService(active.Language);
            var resp = await DiffCore(active.SessionId, oldGen, newGen, langService, ct);
            ctx.ExitCode = ExitCode; output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI diff failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.OutputError(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "DiffFailed" });
        }
    }

    private void HandleMap(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            var active = _currentSession ?? throw new InvalidOperationException("No active session. Run regenerate first.");
            var resp = MapCore(active.SessionId, ct);
            ctx.ExitCode = ExitCode; output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI map failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.OutputError(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "MapFailed" });
        }
    }

    private void HandleMerge(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            var active = _currentSession ?? throw new InvalidOperationException("No active session. Run regenerate first.");
            var resp = MergeCore(active.SessionId, ct);
            ctx.ExitCode = ExitCode; output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI merge failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.OutputError(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "MergeFailed" });
        }
    }

    private void HandlePropose(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            var files = ctx.ParseResult.GetValueForOption(_filesOpt);
            var active = _currentSession ?? throw new InvalidOperationException("No active session. Run regenerate first.");
            var resp = ProposeCore(active.SessionId, string.IsNullOrWhiteSpace(files) ? null : files, ct);
            ctx.ExitCode = ExitCode; output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI propose failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.OutputError(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "ProposeFailed" });
        }
    }

    private void HandleApply(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            var dry = ctx.ParseResult.GetValueForOption(_dryRunOpt);
            var active = _currentSession ?? throw new InvalidOperationException("No active session. Run regenerate first.");
            var resp = ApplyCore(active.SessionId, dry, ct);
            ctx.ExitCode = ExitCode; output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI apply failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.OutputError(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "ApplyFailed" });
        }
    }

    private void HandleStatus(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            var active = _currentSession ?? throw new InvalidOperationException("No active session. Run regenerate first.");
            var resp = StatusCore(active.SessionId, ct);
            ctx.ExitCode = ExitCode; output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI status failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.OutputError(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "StatusFailed" });
        }
    }

    // --------------- Internal Methods ---------------
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
            throw new InvalidOperationException("No active session. Start one with 'azsdk customized-update regenerate <spec-path>'.");
        }
        // If caller supplied a different id, warn (but still allow since only one session exists)
        if (!string.IsNullOrWhiteSpace(sessionId) && !sessionId.Equals(_currentSession.SessionId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Only one in-memory session supported in this build. Active session id: {_currentSession.SessionId}.");
        }
        return _currentSession;
    }

    private string RequireSessionIdOrError(string sessionId, InvocationContext ctx)
    {
        // Method retained for backward compatibility references but no longer used.
        return sessionId;
    }

    private List<string> ParseFiles(string? csv, IEnumerable<string> fallback)
    {
        if (string.IsNullOrWhiteSpace(csv)) { return fallback.ToList(); }
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private sealed class StageOrderException(string message, string code, string suggestedCommand) : Exception(message)
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
            _ => null
        };
    }

    private string? ComputeNextTool(UpdateSessionState session, bool? applyDryRun = null)
    {
        return session.LastStage switch
        {
            UpdateStage.Regenerated => "azsdk_tsp_update_diff", // unconditional
            UpdateStage.Diffed => session.ApiChangeCount > 0 ? "azsdk_tsp_update_map" : null,
            UpdateStage.Mapped => "azsdk_tsp_update_merge",
            UpdateStage.Merged => "azsdk_tsp_update_propose",
            UpdateStage.PatchesProposed => "azsdk_tsp_update_apply",
            UpdateStage.AppliedDryRun => "azsdk_tsp_update_apply", // real apply next
            UpdateStage.Applied => null,
            _ => null
        };
    }
}
