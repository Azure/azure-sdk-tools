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
using System.Text.Json;

namespace Azure.Sdk.Tools.Cli.Tools;

[McpServerToolType, Description("Update customized SDK code after TypeSpec regeneration: creates/continues a session, diffs old vs new generated code, maps API changes to impacted customization files, proposes patches, applies them, and reports status.")]
public class TspClientUpdateTool : MCPTool
{
    private readonly ILogger<TspClientUpdateTool> logger;
    private readonly IOutputService output;

    // --- CLI options/args ---
    private readonly Argument<string> _specPathArg = new(name: "spec-path", description: "Path to the .tsp specification file") { Arity = ArgumentArity.ExactlyOne };
    private readonly Option<string> _sessionIdOpt = new(["--session", "-s"], () => string.Empty, "Existing session id (omit to create a new session)");
    private readonly Option<string> _generatedOldOpt = new(["--old-gen"], () => string.Empty, "Path to previously generated code (optional)");
    private readonly Option<string> _generatedNewOpt = new(["--new-gen"], () => string.Empty, "Target path for new generated code (optional, temp path if omitted)");
    private readonly Option<bool> _dryRunOpt = new(["--dry-run"], () => false, "Do not write any changes when applying patches");
    private readonly Option<string> _filesOpt = new(["--files"], () => string.Empty, "Comma-separated list of customization files to target (defaults to all impacted)");
    private readonly Option<string> _languageOpt = new(["--language", "-l"], () => "java", "Target language (e.g. java, csharp)");
    private readonly Option<bool> _autoOpt = new(["--auto"], () => false, "Automatically chain through stages until completion (stops on error or when no nextTool)");

    // Non-static session store to avoid global mutable state
    private readonly Dictionary<string, UpdateSessionState> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string SessionDir = Path.Combine(Directory.GetCurrentDirectory(), ".tspupdate-sessions");
    private static readonly string ManifestPath = Path.Combine(SessionDir, "sessions-manifest.json");
    private static readonly int SessionSchemaVersion = 1;
    private static readonly string[] InProgressStatuses = ["Regenerated", "Diffed", "Mapped", "Merged", "PatchesProposed", "AppliedDryRun"];

    private readonly IEnumerable<IUpdateLanguageService> _languageServices;

    public TspClientUpdateTool(ILogger<TspClientUpdateTool> logger, IOutputService output, IEnumerable<IUpdateLanguageService>? languageServices = null)
    {
        this.logger = logger;
        this.output = output;
        _languageServices = languageServices ?? new List<IUpdateLanguageService> { new JavaUpdateLanguageService() };
    }

    public override Command GetCommand()
    {
        var root = new Command("customized-update", "Workflow for updating customized TypeSpec-generated client code");

        var regenerate = new Command("regenerate", "Generate new client code artifacts for comparison");
        regenerate.AddArgument(_specPathArg);
        regenerate.AddOption(_generatedNewOpt);
        regenerate.AddOption(_sessionIdOpt);
        regenerate.AddOption(_languageOpt);
        regenerate.AddOption(_autoOpt);
        regenerate.SetHandler(async ctx => { await HandleRegenerate(ctx, ctx.GetCancellationToken()); });

        var diff = new Command("diff", "Detect API surface changes between old and new generation outputs");
        diff.AddOption(_sessionIdOpt);
        diff.AddOption(_generatedOldOpt);
        diff.AddOption(_generatedNewOpt);
        diff.SetHandler(async ctx => { await HandleDiff(ctx, ctx.GetCancellationToken()); });

        var map = new Command("map", "Map API changes to impacted customization files");
        map.AddOption(_sessionIdOpt);
        map.SetHandler(async ctx => { await HandleMap(ctx, ctx.GetCancellationToken()); });

        var merge = new Command("merge", "Apply direct merges for unaffected customization files");
        merge.AddOption(_sessionIdOpt);
        merge.SetHandler(async ctx => { await HandleMerge(ctx, ctx.GetCancellationToken()); });

        var propose = new Command("propose", "Propose patch diffs for impacted customization files");
        propose.AddOption(_sessionIdOpt);
        propose.AddOption(_filesOpt);
        propose.SetHandler(async ctx => { await HandlePropose(ctx, ctx.GetCancellationToken()); });

        var apply = new Command("apply", "Apply previously proposed patches to customization files");
        apply.AddOption(_sessionIdOpt);
        apply.AddOption(_dryRunOpt);
        apply.SetHandler(async ctx => { await HandleApply(ctx, ctx.GetCancellationToken()); });

        var status = new Command("status", "Show current session status and counts");
        status.AddOption(_sessionIdOpt);
        status.SetHandler(async ctx => { await HandleStatus(ctx, ctx.GetCancellationToken()); });

        var sessionsList = new Command("sessions", "List all known update sessions (from manifest)");
        var olderThanOpt = new Option<int>("--older-than", () => 0, "Prune: age threshold in days (with --prune)");
        var pruneOpt = new Option<bool>("--prune", () => false, "Prune sessions older than --older-than days");
        var redactOpt = new Option<bool>("--redact-paths", () => true, "Redact sensitive paths in listing");
        sessionsList.AddOption(pruneOpt);
        sessionsList.AddOption(olderThanOpt);
        sessionsList.AddOption(redactOpt);
        sessionsList.SetHandler(async ctx => { await HandleListSessions(ctx, ctx.GetCancellationToken()); });

        root.AddCommand(regenerate);
        root.AddCommand(diff);
        root.AddCommand(map);
        root.AddCommand(merge);
        root.AddCommand(propose);
        root.AddCommand(apply);
        root.AddCommand(status);
        root.AddCommand(sessionsList);

        CommandHierarchy = [ SharedCommandGroups.EngSys ];
        return root;
    }

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct) => await Task.CompletedTask;

    // --------------- MCP Methods ---------------
    [McpServerTool(Name = "tsp_update_regenerate"), Description("Generate new code artifacts and start (or continue) an update session")] 
    public async Task<TspClientUpdateResponse> Regenerate(string specPath, string? sessionId = null, string? newGeneratedPath = null, bool simulateChange = false, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            // simulateChange parameter retained for backward compatibility in signature but ignored.
            return await DoRegenerate(specPath, sessionId, newGeneratedPath, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Regenerate MCP failed: {specPath}", specPath);
            SetFailure();
            return new TspClientUpdateResponse { ResponseError = ex.Message };
        }
    }

    [McpServerTool(Name = "tsp_update_diff"), Description("Diff old vs new generation outputs and record API changes")] 
    public async Task<TspClientUpdateResponse> Diff(string sessionId, string? oldGeneratedPath = null, string? newGeneratedPath = null, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            return await DoDiff(sessionId, oldGeneratedPath, newGeneratedPath, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Diff MCP failed: {sessionId}", sessionId);
            SetFailure();
            return new TspClientUpdateResponse { ResponseError = ex.Message };
        }
    }

    [McpServerTool(Name = "tsp_update_map"), Description("Map API changes to impacted customization files")] 
    public async Task<TspClientUpdateResponse> Map(string sessionId, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            return await DoMap(sessionId, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Map MCP failed: {sessionId}", sessionId);
            SetFailure();
            return new TspClientUpdateResponse { ResponseError = ex.Message };
        }
    }

    [McpServerTool(Name = "tsp_update_merge"), Description("Apply direct merges for unaffected customization files")] 
    public async Task<TspClientUpdateResponse> Merge(string sessionId, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            return await DoMerge(sessionId, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Merge MCP failed: {sessionId}", sessionId);
            SetFailure();
            return new TspClientUpdateResponse { ResponseError = ex.Message };
        }
    }

    [McpServerTool(Name = "tsp_update_propose"), Description("Propose patch diffs for impacted customization files")] 
    public async Task<TspClientUpdateResponse> Propose(string sessionId, string? filesCsv = null, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            return await DoPropose(sessionId, filesCsv, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Propose MCP failed: {sessionId}", sessionId);
            SetFailure();
            return new TspClientUpdateResponse { ResponseError = ex.Message };
        }
    }

    [McpServerTool(Name = "tsp_update_apply"), Description("Apply previously proposed patches (supports dry-run)")] 
    public async Task<TspClientUpdateResponse> Apply(string sessionId, bool dryRun = false, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            return await DoApply(sessionId, dryRun, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Apply MCP failed: {sessionId}", sessionId);
            SetFailure();
            return new TspClientUpdateResponse { ResponseError = ex.Message };
        }
    }

    [McpServerTool(Name = "tsp_update_status"), Description("Return current session status and counts")] 
    public async Task<TspClientUpdateResponse> Status(string sessionId, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            return await DoStatus(sessionId, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Status MCP failed: {sessionId}", sessionId);
            SetFailure();
            return new TspClientUpdateResponse { ResponseError = ex.Message };
        }
    }

    // --------------- Internal Stage Methods ---------------
    private Task<TspClientUpdateResponse> DoRegenerate(string specPath, string? sessionId, string? newGeneratedPath, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
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
            if (!string.IsNullOrWhiteSpace(session.NewGeneratedPath) && string.IsNullOrWhiteSpace(session.OldGeneratedPath) && Directory.Exists(session.NewGeneratedPath))
            {
                var snapshot = Path.Combine(SessionDir, session.SessionId + "-old-snapshot");
                CopyDirectory(session.NewGeneratedPath, snapshot);
                session.OldGeneratedPath = snapshot;
            }
            var langService = ResolveLanguageService(session.Language);
            langService.RegenerateAsync(session, specPath, newGeneratedPath, ct).GetAwaiter().GetResult();
            session.Status = "Regenerated";
            session.LastStage = UpdateStage.Regenerated;
            session.UpdatedUtc = DateTime.UtcNow;
            SaveSessionToDisk(session);
            return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = $"Regenerate stage complete (language={session.Language})", NextStep = ComputeNextStep(session), NextTool = ComputeNextTool(session) });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Regenerate failed: {specPath}", specPath);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "RegenerateFailed" });
        }
    }

    private Task<TspClientUpdateResponse> DoDiff(string sessionId, string? oldGeneratedPath, string? newGeneratedPath, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var session = RequireSession(sessionId);
            EnforceStageOrder(session, UpdateStage.Regenerated, "diff");
            if (!string.IsNullOrWhiteSpace(oldGeneratedPath)) { session.OldGeneratedPath = oldGeneratedPath; }
            if (!string.IsNullOrWhiteSpace(newGeneratedPath)) { session.NewGeneratedPath = newGeneratedPath; }
            var langService = ResolveLanguageService(session.Language);
            var oldSymbols = string.IsNullOrWhiteSpace(session.OldGeneratedPath) ? new Dictionary<string, SymbolInfo>() : langService.ExtractSymbolsAsync(session.OldGeneratedPath, ct).GetAwaiter().GetResult();
            var newSymbols = string.IsNullOrWhiteSpace(session.NewGeneratedPath) ? new Dictionary<string, SymbolInfo>() : langService.ExtractSymbolsAsync(session.NewGeneratedPath, ct).GetAwaiter().GetResult();
            var apiChanges = langService.DiffAsync(oldSymbols, newSymbols).GetAwaiter().GetResult();
            session.ApiChanges = apiChanges;
            session.ApiChangeCount = apiChanges.Count;
            session.Status = "Diffed";
            session.LastStage = UpdateStage.Diffed;
            session.UpdatedUtc = DateTime.UtcNow;
            SaveSessionToDisk(session);
            return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = $"Diff stage complete: {apiChanges.Count} API changes (language={session.Language})", NextStep = ComputeNextStep(session), NextTool = ComputeNextTool(session) });
        }
        catch (StageOrderException sox)
        {
            // Provide next tool based on current loaded session state
            var sess = _sessions.ContainsKey(sessionId) ? _sessions[sessionId] : null;
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = sox.Message, ErrorCode = sox.Code, NextStep = sox.SuggestedCommand, NextTool = sess != null ? ComputeNextTool(sess) : null });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Diff failed: {sessionId}", sessionId);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "DiffFailed" });
        }
    }

    private Task<TspClientUpdateResponse> DoMap(string sessionId, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var session = RequireSession(sessionId);
            EnforceStageOrder(session, UpdateStage.Diffed, "map");
            if (session.ApiChanges.Count == 0)
            {
                return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = "No API changes to map", NextStep = ComputeNextStep(session) });
            }
            var customDir = Path.Combine(Directory.GetCurrentDirectory(), "src", "custom");
            var langService = ResolveLanguageService(session.Language);
            // Attempt richer impact analysis first
            var impacts = langService.AnalyzeCustomizationImpactAsync(session, customDir, session.ApiChanges, ct).GetAwaiter().GetResult();
            // Fallback heuristics: if no impacts and RelatedGeneratedId present in changes, map files containing symbol simple name
            if (impacts.Count == 0 && Directory.Exists(customDir))
            {
                var symbolNames = session.ApiChanges.Select(c => c.Symbol.Split('.').Last()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                foreach (var file in Directory.GetFiles(customDir, session.Language == "java" ? "*.java" : "*.*", SearchOption.AllDirectories))
                {
                    var text = File.ReadAllText(file);
                    var matched = symbolNames.Where(n => text.Contains(n)).ToList();
                    if (matched.Count > 0)
                    {
                        impacts.Add(new CustomizationImpact { File = file, Reasons = matched.Select(m => $"References {m}").ToList() });
                    }
                }
            }
            var directMerge = new List<string>();
            if (Directory.Exists(customDir))
            {
                var allCustom = Directory.GetFiles(customDir, session.Language == "java" ? "*.java" : "*.*", SearchOption.AllDirectories).ToList();
                var impactedSet = impacts.Select(i => i.File).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var f in allCustom)
                {
                    if (!impactedSet.Contains(f)) { directMerge.Add(f); }
                }
            }
            else if (impacts.Count == 0)
            {
                impacts.Add(new CustomizationImpact { File = Path.Combine(customDir, session.Language == "java" ? "ClientExtensions.java" : "ClientExtensions.txt"), Reasons = ["Dummy impact (custom folder missing)"] });
                directMerge.Add(Path.Combine(customDir, session.Language == "java" ? "UnchangedHelper.java" : "UnchangedHelper.txt"));
            }
            session.ImpactedCustomizations = impacts;
            session.DirectMergeFiles = directMerge;
            session.ImpactedCount = impacts.Count;
            session.Status = "Mapped";
            session.LastStage = UpdateStage.Mapped;
            session.UpdatedUtc = DateTime.UtcNow;
            SaveSessionToDisk(session);
            return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = $"Impact mapping complete: {impacts.Count} impacted, {directMerge.Count} direct merge (language={session.Language})", NextStep = ComputeNextStep(session), NextTool = ComputeNextTool(session) });
        }
        catch (StageOrderException sox)
        {
            var sess = _sessions.ContainsKey(sessionId) ? _sessions[sessionId] : null;
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = sox.Message, ErrorCode = sox.Code, NextStep = sox.SuggestedCommand, NextTool = sess != null ? ComputeNextTool(sess) : null });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Map failed: {sessionId}", sessionId);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "MapFailed" });
        }
    }

    private Task<TspClientUpdateResponse> DoMerge(string sessionId, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var session = RequireSession(sessionId);
            EnforceStageOrder(session, UpdateStage.Mapped, "merge");
            if (session.DirectMergeFiles.Count == 0)
            {
                return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = "No direct merge files", NextStep = ComputeNextStep(session) });
            }
            // Simulate: copy direct merge files to a backup/merged folder
            var mergedDir = Path.Combine(Directory.GetCurrentDirectory(), "src", "custom", "merged");
            Directory.CreateDirectory(mergedDir);
            foreach (var file in session.DirectMergeFiles)
            {
                var dest = Path.Combine(mergedDir, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }
            session.Status = "Merged";
            session.LastStage = UpdateStage.Merged;
            session.UpdatedUtc = DateTime.UtcNow;
            SaveSessionToDisk(session);
            return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = $"Merged {session.DirectMergeFiles.Count} files to {mergedDir}", NextStep = ComputeNextStep(session), NextTool = ComputeNextTool(session) });
        }
        catch (StageOrderException sox)
        {
            var sess = _sessions.ContainsKey(sessionId) ? _sessions[sessionId] : null;
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = sox.Message, ErrorCode = sox.Code, NextStep = sox.SuggestedCommand, NextTool = sess != null ? ComputeNextTool(sess) : null });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Merge failed: {sessionId}", sessionId);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "MergeFailed" });
        }
    }

    private Task<TspClientUpdateResponse> DoPropose(string sessionId, string? filesCsv, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var session = RequireSession(sessionId);
            EnforceStageOrder(session, UpdateStage.Merged, "propose");
            var targetFiles = ParseFiles(filesCsv, session.ImpactedCustomizations.Select(i => i.File));
            foreach (var f in targetFiles)
            {
                if (session.ProposedPatches.Any(p => p.File.Equals(f, StringComparison.OrdinalIgnoreCase))) { continue; }
                // Generate a simple diff: replace 'int bar' with 'string bar' in impacted files
                if (File.Exists(f))
                {
                    var content = File.ReadAllText(f);
                    var newContent = content.Replace("int bar", "string bar");
                    var diff = $"--- a/{f}\n+++ b/{f}\n@@ -1,1 +1,1 @@\n- {content}\n+ {newContent}\n";
                    session.ProposedPatches.Add(new PatchProposal
                    {
                        File = f,
                        Diff = diff,
                        Rationale = "Update for API signature change"
                    });
                }
                else
                {
                    session.ProposedPatches.Add(new PatchProposal
                    {
                        File = f,
                        Diff = $"--- a/{f}\n+++ b/{f}\n@@ -1,1 +1,1 @@\n- (file missing)\n+ (new file)\n",
                        Rationale = "File missing, would create new"
                    });
                }
            }
            session.Status = "PatchesProposed";
            session.LastStage = UpdateStage.PatchesProposed;
            session.UpdatedUtc = DateTime.UtcNow;
            SaveSessionToDisk(session);
            return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = $"Proposed {targetFiles.Count} patches", NextStep = ComputeNextStep(session), NextTool = ComputeNextTool(session) });
        }
        catch (StageOrderException sox)
        {
            var sess = _sessions.ContainsKey(sessionId) ? _sessions[sessionId] : null;
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = sox.Message, ErrorCode = sox.Code, NextStep = sox.SuggestedCommand, NextTool = sess != null ? ComputeNextTool(sess) : null });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Propose failed: {sessionId}", sessionId);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "ProposeFailed" });
        }
    }

    private Task<TspClientUpdateResponse> DoApply(string sessionId, bool dryRun, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var session = RequireSession(sessionId);
            EnforceStageOrder(session, UpdateStage.PatchesProposed, "apply");
            if (session.ProposedPatches.Count == 0)
            {
                return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = "No patches to apply", NextStep = ComputeNextStep(session) });
            }
            int applied = 0;
            int failed = 0;
            foreach (var patch in session.ProposedPatches)
            {
                if (dryRun) { continue; }
                try
                {
                    if (File.Exists(patch.File))
                    {
                        var content = File.ReadAllText(patch.File);
                        var newContent = content.Replace("int bar", "string bar");
                        File.WriteAllText(patch.File, newContent);
                        applied++;
                    }
                }
                catch
                {
                    failed++;
                }
            }
            session.PatchesAppliedSuccess += applied;
            session.PatchesAppliedFailed += failed;
            session.Status = dryRun ? "AppliedDryRun" : "Applied";
            session.LastStage = dryRun ? UpdateStage.AppliedDryRun : UpdateStage.Applied;
            session.UpdatedUtc = DateTime.UtcNow;
            SaveSessionToDisk(session);
            var msg = dryRun ? "Dry-run apply complete" : $"Applied {applied} patches (failed {failed})";
            return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = msg, NextStep = ComputeNextStep(session), NextTool = ComputeNextTool(session) });
        }
        catch (StageOrderException sox)
        {
            var sess = _sessions.ContainsKey(sessionId) ? _sessions[sessionId] : null;
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = sox.Message, ErrorCode = sox.Code, NextStep = sox.SuggestedCommand, NextTool = sess != null ? ComputeNextTool(sess) : null });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Apply failed: {sessionId}", sessionId);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "ApplyFailed" });
        }
    }

    private Task<TspClientUpdateResponse> DoStatus(string sessionId, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var session = RequireSession(sessionId);
            return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = "OK", NextTool = ComputeNextTool(session) });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Status failed: {sessionId}", sessionId);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    // --------------- CLI Handlers ---------------
    private async Task HandleRegenerate(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            var spec = ctx.ParseResult.GetValueForArgument(_specPathArg);
            var sid = AutoSelectSessionIfConvenient(ctx.ParseResult.GetValueForOption(_sessionIdOpt));
            var newGen = ctx.ParseResult.GetValueForOption(_generatedNewOpt);
            var lang = ctx.ParseResult.GetValueForOption(_languageOpt);
            var auto = ctx.ParseResult.GetValueForOption(_autoOpt);
            var session = string.IsNullOrWhiteSpace(sid) ? GetOrCreateSession(null) : GetOrCreateSession(sid);
            if (!string.IsNullOrWhiteSpace(lang)) { session.Language = lang; SaveSessionToDisk(session); }
            var resp = await DoRegenerate(spec, session.SessionId, string.IsNullOrWhiteSpace(newGen) ? null : newGen, ct);
            output.Output(resp);
            if (auto)
            {
                // loop using NextTool field
                var current = resp;
                while (current.NextTool != null && current.Session != null && string.IsNullOrEmpty(current.ResponseError))
                {
                    ct.ThrowIfCancellationRequested();
                    var next = current.NextTool;
                    TspClientUpdateResponse nextResp;
                    switch (next)
                    {
                        case "tsp_update_diff":
                            nextResp = await DoDiff(current.Session.SessionId, null, null, ct);
                            break;
                        case "tsp_update_map":
                            nextResp = await DoMap(current.Session.SessionId, ct);
                            break;
                        case "tsp_update_merge":
                            nextResp = await DoMerge(current.Session.SessionId, ct);
                            break;
                        case "tsp_update_propose":
                            nextResp = await DoPropose(current.Session.SessionId, null, ct);
                            break;
                        case "tsp_update_apply":
                            // For auto mode: first apply dry-run, then real apply automatically if dry-run succeeded
                            var isDryRunNeeded = current.Session.LastStage == UpdateStage.PatchesProposed;
                            nextResp = await DoApply(current.Session.SessionId, isDryRunNeeded, ct);
                            // If we just did dry-run, loop will continue because NextTool will still be tsp_update_apply
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
            ctx.ExitCode = ExitCode; output.Output(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "RegenerateFailed" });
        }
    }

    private async Task HandleDiff(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            var sid = AutoSelectSessionIfConvenient(ctx.ParseResult.GetValueForOption(_sessionIdOpt));
            var oldGen = ctx.ParseResult.GetValueForOption(_generatedOldOpt);
            var newGen = ctx.ParseResult.GetValueForOption(_generatedNewOpt);
            var resp = await DoDiff(RequireSessionIdOrError(sid, ctx), oldGen, newGen, ct);
            ctx.ExitCode = ExitCode; output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI diff failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.Output(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "DiffFailed" });
        }
    }

    private async Task HandleMap(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            var sid = AutoSelectSessionIfConvenient(ctx.ParseResult.GetValueForOption(_sessionIdOpt));
            var resp = await DoMap(RequireSessionIdOrError(sid, ctx), ct);
            ctx.ExitCode = ExitCode; output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI map failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.Output(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "MapFailed" });
        }
    }

    private async Task HandleMerge(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            var sid = AutoSelectSessionIfConvenient(ctx.ParseResult.GetValueForOption(_sessionIdOpt));
            var resp = await DoMerge(RequireSessionIdOrError(sid, ctx), ct);
            ctx.ExitCode = ExitCode; output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI merge failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.Output(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "MergeFailed" });
        }
    }

    private async Task HandlePropose(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            var sid = AutoSelectSessionIfConvenient(ctx.ParseResult.GetValueForOption(_sessionIdOpt));
            var files = ctx.ParseResult.GetValueForOption(_filesOpt);
            var resp = await DoPropose(RequireSessionIdOrError(sid, ctx), string.IsNullOrWhiteSpace(files) ? null : files, ct);
            ctx.ExitCode = ExitCode; output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI propose failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.Output(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "ProposeFailed" });
        }
    }

    private async Task HandleApply(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            var sid = AutoSelectSessionIfConvenient(ctx.ParseResult.GetValueForOption(_sessionIdOpt));
            var dry = ctx.ParseResult.GetValueForOption(_dryRunOpt);
            var resp = await DoApply(RequireSessionIdOrError(sid, ctx), dry, ct);
            ctx.ExitCode = ExitCode; output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI apply failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.Output(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "ApplyFailed" });
        }
    }

    private async Task HandleStatus(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            var sid = AutoSelectSessionIfConvenient(ctx.ParseResult.GetValueForOption(_sessionIdOpt));
            var resp = await DoStatus(RequireSessionIdOrError(sid, ctx), ct);
            ctx.ExitCode = ExitCode; output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI status failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.Output(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "StatusFailed" });
        }
    }

    private async Task HandleListSessions(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var prune = ctx.ParseResult.GetValueForOption(new Option<bool>("--prune"));
            var older = ctx.ParseResult.GetValueForOption(new Option<int>("--older-than"));
            var redact = ctx.ParseResult.GetValueForOption(new Option<bool>("--redact-paths"));
            var entries = LoadManifest();
            if (prune && older > 0)
            {
                var cutoff = DateTime.UtcNow.AddDays(-older);
                var kept = entries.Where(e => e.UpdatedUtc >= cutoff).ToList();
                var removed = entries.Count - kept.Count;
                entries = kept;
                SaveManifest(entries);
                output.Output(new TspClientUpdateResponse { Message = $"Pruned {removed} sessions older than {older}d" });
            }
            var lines = entries.Count == 0 ? "No sessions found" : string.Join('\n', entries.Select(e => FormatManifestEntry(e, redact)));
            output.Output(new TspClientUpdateResponse { Message = lines });
            await Task.CompletedTask;
            ctx.ExitCode = ExitCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI sessions list failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.Output(new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "SessionsListFailed" });
        }
    }

    // --------------- Internal Methods ---------------
    private UpdateSessionState GetOrCreateSession(string? sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            // Try in-memory first
            if (_sessions.TryGetValue(sessionId, out var existing)) { return existing; }
            // Try loading from disk
            var loaded = LoadSessionFromDisk(sessionId);
            if (loaded != null)
            {
                _sessions[sessionId] = loaded;
                return loaded;
            }
        }
        var session = new UpdateSessionState();
        _sessions[session.SessionId] = session;
        SaveSessionToDisk(session);
        return session;
    }

    private UpdateSessionState RequireSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) { throw new ArgumentException("sessionId required. Provide --session <id> or start a new session via 'azsdk customized-update regenerate <spec-path>'."); }
        if (_sessions.TryGetValue(sessionId, out var session)) { return ValidateAndMaybeMarkStale(session); }
        var loaded = LoadSessionFromDisk(sessionId);
        if (loaded != null)
        {
            _sessions[sessionId] = loaded;
            return ValidateAndMaybeMarkStale(loaded);
        }
        throw new InvalidOperationException($"Unknown session: {sessionId}. To create a new session run 'azsdk customized-update regenerate <spec-path>'. To discover existing sessions run 'azsdk customized-update sessions'.");
    }

    // --------------- Session Persistence ---------------
    private void SaveSessionToDisk(UpdateSessionState session)
    {
        Directory.CreateDirectory(SessionDir);
        var path = Path.Combine(SessionDir, session.SessionId + ".json");
        session.SchemaVersion = SessionSchemaVersion;
        session.UpdatedUtc = DateTime.UtcNow;
        // atomic write with lock
        var tmp = path + ".tmp";
        var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
        using var fsLock = AcquireFileLock(path + ".lock");
        File.WriteAllText(tmp, json);
        if (File.Exists(path)) { File.Delete(path); }
        File.Move(tmp, path);
        UpsertManifest(session);
    }

    private record SessionManifestEntry
    (
        string SessionId,
        string Status,
        string? SpecPath,
        DateTime CreatedUtc,
        DateTime UpdatedUtc
    );

    private List<SessionManifestEntry> LoadManifest()
    {
        if (!File.Exists(ManifestPath))
        {
            return new List<SessionManifestEntry>();
        }
        try
        {
            var json = File.ReadAllText(ManifestPath);
            var list = JsonSerializer.Deserialize<List<SessionManifestEntry>>(json) ?? new List<SessionManifestEntry>();
            return list;
        }
        catch
        {
            return new List<SessionManifestEntry>();
        }
    }

    private void SaveManifest(List<SessionManifestEntry> entries)
    {
        Directory.CreateDirectory(SessionDir);
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        var tmp = ManifestPath + ".tmp";
        using var fsLock = AcquireFileLock(ManifestPath + ".lock");
        File.WriteAllText(tmp, json);
        if (File.Exists(ManifestPath)) { File.Delete(ManifestPath); }
        File.Move(tmp, ManifestPath);
    }

    private void UpsertManifest(UpdateSessionState session)
    {
        var entries = LoadManifest();
        var existing = entries.FirstOrDefault(e => e.SessionId.Equals(session.SessionId, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            entries.Add(new SessionManifestEntry(session.SessionId, session.Status ?? string.Empty, session.SpecPath, DateTime.UtcNow, DateTime.UtcNow));
        }
        else
        {
            entries = entries.Where(e => !e.SessionId.Equals(session.SessionId, StringComparison.OrdinalIgnoreCase)).ToList();
            entries.Add(existing with { Status = session.Status ?? existing.Status, SpecPath = session.SpecPath ?? existing.SpecPath, UpdatedUtc = DateTime.UtcNow });
        }
        SaveManifest(entries.OrderBy(e => e.CreatedUtc).ToList());
    }

    private UpdateSessionState? LoadSessionFromDisk(string sessionId)
    {
        var path = Path.Combine(SessionDir, sessionId + ".json");
        if (!File.Exists(path)) { return null; }
        var json = File.ReadAllText(path);
        var session = JsonSerializer.Deserialize<UpdateSessionState>(json);
        if (session == null) { return null; }
        if (session.SchemaVersion != SessionSchemaVersion)
        {
            throw new InvalidOperationException($"Unsupported session schema {session.SchemaVersion}. Expected {SessionSchemaVersion}.");
        }
        return ValidateAndMaybeMarkStale(session);
    }

    private string RequireSessionIdOrError(string sessionId, InvocationContext ctx)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            SetFailure();
            ctx.ExitCode = ExitCode;
            throw new ArgumentException("--session is required. Use 'azsdk customized-update sessions' to list existing sessions or start a new one with 'azsdk customized-update regenerate <spec-path>'.");
        }
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
            UpdateStage.Regenerated => $"Run diff --session {session.SessionId}",
            UpdateStage.Diffed => session.ApiChangeCount == 0 ? null : $"Run map --session {session.SessionId}",
            UpdateStage.Mapped => $"Run merge --session {session.SessionId}",
            UpdateStage.Merged => $"Run propose --session {session.SessionId}",
            UpdateStage.PatchesProposed => $"Run apply --session {session.SessionId}",
            UpdateStage.AppliedDryRun => $"Review changes then re-run apply --session {session.SessionId}",
            _ => null
        };
    }

    private string? ComputeNextTool(UpdateSessionState session, bool? applyDryRun = null)
    {
        return session.LastStage switch
        {
            UpdateStage.Regenerated => "tsp_update_diff", // unconditional
            UpdateStage.Diffed => session.ApiChangeCount > 0 ? "tsp_update_map" : null,
            UpdateStage.Mapped => "tsp_update_merge",
            UpdateStage.Merged => "tsp_update_propose",
            UpdateStage.PatchesProposed => "tsp_update_apply",
            UpdateStage.AppliedDryRun => "tsp_update_apply", // real apply next
            UpdateStage.Applied => null,
            _ => null
        };
    }

    private IDisposable AcquireFileLock(string lockPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        FileStream fs;
        while (true)
        {
            try
            {
                fs = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                break;
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }
        return fs;
    }

    private UpdateSessionState ValidateAndMaybeMarkStale(UpdateSessionState session)
    {
        bool missing = false;
        if (!string.IsNullOrWhiteSpace(session.NewGeneratedPath) && !Directory.Exists(session.NewGeneratedPath)) { missing = true; }
        if (!string.IsNullOrWhiteSpace(session.OldGeneratedPath) && !Directory.Exists(session.OldGeneratedPath)) { missing = true; }
        if (missing && session.LastStage != UpdateStage.Stale)
        {
            session.Status = "Stale";
            session.LastStage = UpdateStage.Stale;
        }
        return session;
    }

    private string FormatManifestEntry(SessionManifestEntry e, bool redact)
    {
        var pathDisplay = redact && !string.IsNullOrWhiteSpace(e.SpecPath) ? Path.GetFileName(e.SpecPath) : e.SpecPath;
        return $"{e.SessionId}\t{e.Status}\t{pathDisplay}";
    }

    private string? AutoSelectSessionIfConvenient(string provided)
    {
        if (!string.IsNullOrWhiteSpace(provided)) { return provided; }
        var entries = LoadManifest();
        var active = entries.Where(e => InProgressStatuses.Contains(e.Status)).ToList();
        if (active.Count == 1)
        {
            output.Output(new TspClientUpdateResponse { Message = $"Auto-selected session {active[0].SessionId} (status {active[0].Status})", NextStep = null });
            return active[0].SessionId;
        }
        return provided; // unchanged
    }

    private void CopyDirectory(string sourceDir, string destDir)
    {
        if (!Directory.Exists(sourceDir)) { return; }
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, file);
            var target = Path.Combine(destDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private IUpdateLanguageService ResolveLanguageService(string lang)
    {
        var svc = _languageServices.FirstOrDefault(s => string.Equals(s.Language, lang, StringComparison.OrdinalIgnoreCase));
        if (svc == null) { throw new InvalidOperationException($"No update language service registered for '{lang}'"); }
        return svc;
    }
}
