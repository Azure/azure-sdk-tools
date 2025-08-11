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

    // Non-static session store to avoid global mutable state
    private readonly Dictionary<string, UpdateSessionState> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public TspClientUpdateTool(ILogger<TspClientUpdateTool> logger, IOutputService output)
    {
        this.logger = logger;
        this.output = output;
    }

    public override Command GetCommand()
    {
        var root = new Command("customized-update", "Workflow for updating customized TypeSpec-generated client code");

        var regenerate = new Command("regenerate", "Generate new client code artifacts for comparison");
        regenerate.AddArgument(_specPathArg);
        regenerate.AddOption(_generatedNewOpt);
        regenerate.AddOption(_sessionIdOpt);
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

        root.AddCommand(regenerate);
        root.AddCommand(diff);
        root.AddCommand(map);
        root.AddCommand(merge);
        root.AddCommand(propose);
        root.AddCommand(apply);
        root.AddCommand(status);

        CommandHierarchy = [ SharedCommandGroups.EngSys ];
        return root;
    }

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct) => await Task.CompletedTask;

    // --------------- MCP Methods ---------------
    [McpServerTool(Name = "tsp_update_regenerate"), Description("Generate new code artifacts and start (or continue) an update session")] 
    public async Task<TspClientUpdateResponse> Regenerate(string specPath, string? sessionId = null, string? newGeneratedPath = null, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
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
            session.SpecPath = specPath;
            session.NewGeneratedPath = string.IsNullOrWhiteSpace(newGeneratedPath) ? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tsp-gen-" + session.SessionId) : newGeneratedPath;
            session.Status = "Regenerated";
            return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = "Regenerate stage complete (skeleton)" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Regenerate failed: {specPath}", specPath);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    private Task<TspClientUpdateResponse> DoDiff(string sessionId, string? oldGeneratedPath, string? newGeneratedPath, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var session = RequireSession(sessionId);
            if (!string.IsNullOrWhiteSpace(oldGeneratedPath)) { session.OldGeneratedPath = oldGeneratedPath; }
            if (!string.IsNullOrWhiteSpace(newGeneratedPath)) { session.NewGeneratedPath = newGeneratedPath; }
            session.ApiChanges = [
                new ApiChange { Kind = "SignatureChanged", Symbol = "Client.GetFoo", Detail = "param bar: int -> string" },
                new ApiChange { Kind = "MethodRemoved", Symbol = "Client.DeleteFoo", Detail = "Removed in new spec" }
            ];
            session.Status = "Diffed";
            return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = "Diff stage complete (dummy changes)" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Diff failed: {sessionId}", sessionId);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    private Task<TspClientUpdateResponse> DoMap(string sessionId, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var session = RequireSession(sessionId);
            if (session.ApiChanges.Count == 0)
            {
                return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = "No API changes to map" });
            }
            session.ImpactedCustomizations = [
                new CustomizationImpact { File = "src/custom/ClientExtensions.cs", Reasons = ["Uses Client.GetFoo"] },
                new CustomizationImpact { File = "src/custom/DeleteHelpers.cs", Reasons = ["Calls Client.DeleteFoo"] }
            ];
            session.DirectMergeFiles = ["src/custom/UnchangedHelper.cs"];
            session.Status = "Mapped";
            return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = "Impact mapping complete (dummy)" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Map failed: {sessionId}", sessionId);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    private Task<TspClientUpdateResponse> DoMerge(string sessionId, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var session = RequireSession(sessionId);
            if (session.DirectMergeFiles.Count == 0)
            {
                return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = "No direct merge files" });
            }
            session.Status = "Merged";
            return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = $"Merged {session.DirectMergeFiles.Count} files (dummy)" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Merge failed: {sessionId}", sessionId);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    private Task<TspClientUpdateResponse> DoPropose(string sessionId, string? filesCsv, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var session = RequireSession(sessionId);
            var targetFiles = ParseFiles(filesCsv, session.ImpactedCustomizations.Select(i => i.File));
            foreach (var f in targetFiles)
            {
                if (session.ProposedPatches.Any(p => p.File.Equals(f, StringComparison.OrdinalIgnoreCase))) { continue; }
                session.ProposedPatches.Add(new PatchProposal
                {
                    File = f,
                    Diff = $"--- a/{f}\\n+++ b/{f}\\n@@ line @@\\n- old line\\n+ updated line (dummy)\\n",
                    Rationale = "Adjust for API change (dummy)"
                });
            }
            session.Status = "PatchesProposed";
            return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = $"Proposed {targetFiles.Count} patches (dummy)" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Propose failed: {sessionId}", sessionId);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    private Task<TspClientUpdateResponse> DoApply(string sessionId, bool dryRun, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var session = RequireSession(sessionId);
            if (session.ProposedPatches.Count == 0)
            {
                return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = "No patches to apply" });
            }
            session.Status = dryRun ? "AppliedDryRun" : "Applied";
            return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = dryRun ? "Dry-run apply complete" : "Patches applied (dummy)" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Apply failed: {sessionId}", sessionId);
            SetFailure();
            return Task.FromResult(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    private Task<TspClientUpdateResponse> DoStatus(string sessionId, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var session = RequireSession(sessionId);
            return Task.FromResult(new TspClientUpdateResponse { Session = session, Message = "OK" });
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
            var sid = ctx.ParseResult.GetValueForOption(_sessionIdOpt);
            var newGen = ctx.ParseResult.GetValueForOption(_generatedNewOpt);
            var resp = await DoRegenerate(spec, string.IsNullOrWhiteSpace(sid) ? null : sid, string.IsNullOrWhiteSpace(newGen) ? null : newGen, ct);
            ctx.ExitCode = ExitCode; output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI regenerate failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.Output(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    private async Task HandleDiff(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            var sid = ctx.ParseResult.GetValueForOption(_sessionIdOpt);
            var oldGen = ctx.ParseResult.GetValueForOption(_generatedOldOpt);
            var newGen = ctx.ParseResult.GetValueForOption(_generatedNewOpt);
            var resp = await DoDiff(RequireSessionIdOrError(sid, ctx), oldGen, newGen, ct);
            ctx.ExitCode = ExitCode; output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI diff failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.Output(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    private async Task HandleMap(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            var sid = ctx.ParseResult.GetValueForOption(_sessionIdOpt);
            var resp = await DoMap(RequireSessionIdOrError(sid, ctx), ct);
            ctx.ExitCode = ExitCode; output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI map failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.Output(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    private async Task HandleMerge(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            var sid = ctx.ParseResult.GetValueForOption(_sessionIdOpt);
            var resp = await DoMerge(RequireSessionIdOrError(sid, ctx), ct);
            ctx.ExitCode = ExitCode; output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI merge failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.Output(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    private async Task HandlePropose(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            var sid = ctx.ParseResult.GetValueForOption(_sessionIdOpt);
            var files = ctx.ParseResult.GetValueForOption(_filesOpt);
            var resp = await DoPropose(RequireSessionIdOrError(sid, ctx), string.IsNullOrWhiteSpace(files) ? null : files, ct);
            ctx.ExitCode = ExitCode; output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI propose failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.Output(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    private async Task HandleApply(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            var sid = ctx.ParseResult.GetValueForOption(_sessionIdOpt);
            var dry = ctx.ParseResult.GetValueForOption(_dryRunOpt);
            var resp = await DoApply(RequireSessionIdOrError(sid, ctx), dry, ct);
            ctx.ExitCode = ExitCode; output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI apply failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.Output(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    private async Task HandleStatus(InvocationContext ctx, CancellationToken ct)
    {
        try
        {
            var sid = ctx.ParseResult.GetValueForOption(_sessionIdOpt);
            var resp = await DoStatus(RequireSessionIdOrError(sid, ctx), ct);
            ctx.ExitCode = ExitCode; output.Output(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CLI status failed");
            SetFailure();
            ctx.ExitCode = ExitCode; output.Output(new TspClientUpdateResponse { ResponseError = ex.Message });
        }
    }

    // --------------- Helpers ---------------
    private UpdateSessionState GetOrCreateSession(string? sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId) && _sessions.TryGetValue(sessionId, out var existing)) { return existing; }
        var session = new UpdateSessionState();
        _sessions[session.SessionId] = session;
        return session;
    }

    private UpdateSessionState RequireSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) { throw new ArgumentException("sessionId required"); }
        if (!_sessions.TryGetValue(sessionId, out var session)) { throw new InvalidOperationException($"Unknown session: {sessionId}"); }
        return session;
    }

    private string RequireSessionIdOrError(string sessionId, InvocationContext ctx)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            SetFailure();
            ctx.ExitCode = ExitCode;
            throw new ArgumentException("--session is required");
        }
        return sessionId;
    }

    private List<string> ParseFiles(string? csv, IEnumerable<string> fallback)
    {
        if (string.IsNullOrWhiteSpace(csv)) { return fallback.ToList(); }
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}
