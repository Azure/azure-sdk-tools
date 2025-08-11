// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Tools;

[McpServerToolType, Description("Orchestrates tsp-client customization update workflow (Phase 1-2 skeleton)")]
public class TspClientUpdateTool(ILogger<TspClientUpdateTool> logger, IOutputService output) : MCPTool
{
    // --- CLI options/args ---
    private readonly Argument<string> _specPathArg = new(name: "spec-path", description: "Path to .tsp spec (Stage1 regenerate)") { Arity = ArgumentArity.ExactlyOne };
    private readonly Option<string> _sessionIdOpt = new(["--session", "-s"], () => string.Empty, "Existing session id");
    private readonly Option<string> _generatedOldOpt = new(["--old-gen"], () => string.Empty, "Path to previous generated code (optional)");
    private readonly Option<string> _generatedNewOpt = new(["--new-gen"], () => string.Empty, "Path to place new generated code (optional)");
    private readonly Option<bool> _dryRunOpt = new(["--dry-run"], () => false, "Do not write changes");
    private readonly Option<string> _filesOpt = new(["--files"], () => string.Empty, "Comma-separated file list");

    private static readonly Dictionary<string, UpdateSessionState> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public override Command GetCommand()
    {
        var root = new Command("customized-update", "tsp-client customized code update workflow");

        // regenerate
        var regenerate = new Command("regenerate", "Stage 1: regenerate new code into staging path (skeleton)");
        regenerate.AddArgument(_specPathArg);
        regenerate.AddOption(_generatedNewOpt);
        regenerate.AddOption(_sessionIdOpt);
        regenerate.SetHandler(async ctx => { await HandleRegenerate(ctx, ctx.GetCancellationToken()); });

        // diff
        var diff = new Command("diff", "Stage 2: diff old vs new generated code (skeleton)");
        diff.AddOption(_sessionIdOpt);
        diff.AddOption(_generatedOldOpt);
        diff.AddOption(_generatedNewOpt);
        diff.SetHandler(async ctx => { await HandleDiff(ctx, ctx.GetCancellationToken()); });

        // map
        var map = new Command("map", "Stage 2: map customizations to API changes (skeleton)");
        map.AddOption(_sessionIdOpt);
        map.SetHandler(async ctx => { await HandleMap(ctx, ctx.GetCancellationToken()); });

        // merge
        var merge = new Command("merge", "Stage 3: apply direct merges (skeleton)");
        merge.AddOption(_sessionIdOpt);
        merge.SetHandler(async ctx => { await HandleMerge(ctx, ctx.GetCancellationToken()); });

        // propose patches
        var propose = new Command("propose", "Stage 4: propose patches for impacted customizations (dummy Phase2)");
        propose.AddOption(_sessionIdOpt);
        propose.AddOption(_filesOpt);
        propose.SetHandler(async ctx => { await HandlePropose(ctx, ctx.GetCancellationToken()); });

        // apply patches
        var apply = new Command("apply", "Stage 4: apply previously proposed patches (dummy Phase2)");
        apply.AddOption(_sessionIdOpt);
        apply.AddOption(_dryRunOpt);
        apply.SetHandler(async ctx => { await HandleApply(ctx, ctx.GetCancellationToken()); });

        // status
        var status = new Command("status", "Show session state");
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

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        // The sub-command handlers already perform logic.
        await Task.CompletedTask;
    }

    // --------------- Response Models ---------------
    public class UpdateSessionState
    {
        [JsonPropertyName("sessionId")] public string SessionId { get; set; } = Guid.NewGuid().ToString("n");
        [JsonPropertyName("specPath")] public string SpecPath { get; set; } = string.Empty;
        [JsonPropertyName("oldGeneratedPath")] public string OldGeneratedPath { get; set; } = string.Empty;
        [JsonPropertyName("newGeneratedPath")] public string NewGeneratedPath { get; set; } = string.Empty;
        [JsonPropertyName("status")] public string Status { get; set; } = "Initialized";
        [JsonPropertyName("apiChanges")] public List<ApiChange> ApiChanges { get; set; } = new();
        [JsonPropertyName("impactedCustomizations")] public List<CustomizationImpact> ImpactedCustomizations { get; set; } = new();
        [JsonPropertyName("directMergeFiles")] public List<string> DirectMergeFiles { get; set; } = new();
        [JsonPropertyName("proposedPatches")] public List<PatchProposal> ProposedPatches { get; set; } = new();
    }

    public class ApiChange
    {
        [JsonPropertyName("kind")] public string Kind { get; set; } = string.Empty;
        [JsonPropertyName("symbol")] public string Symbol { get; set; } = string.Empty;
        [JsonPropertyName("detail")] public string Detail { get; set; } = string.Empty;
    }

    public class CustomizationImpact
    {
        [JsonPropertyName("file")] public string File { get; set; } = string.Empty;
        [JsonPropertyName("reasons")] public List<string> Reasons { get; set; } = new();
    }

    public class PatchProposal
    {
        [JsonPropertyName("file")] public string File { get; set; } = string.Empty;
        [JsonPropertyName("diff")] public string Diff { get; set; } = string.Empty; // unified diff placeholder
        [JsonPropertyName("rationale")] public string Rationale { get; set; } = string.Empty;
    }

    // Generic response wrapper (could reuse DefaultCommandResponse but keeping custom for clarity)
    public class UpdateResponse : Response
    {
        [JsonPropertyName("session")] public UpdateSessionState? Session { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(Message)) sb.AppendLine(Message);
            if (Session != null) sb.AppendLine($"Session: {Session.SessionId} Status: {Session.Status} API changes: {Session.ApiChanges.Count}");
            return ToString(sb);
        }
    }

    // --------------- MCP Methods ---------------

    [McpServerTool(Name = "tsp_update_regenerate"), Description("Stage1 regenerate placeholder")]
    public async Task<UpdateResponse> Regenerate(string specPath, string? sessionId = null, string? newGeneratedPath = null, CancellationToken ct = default)
    {
        try
        {
            var session = GetOrCreateSession(sessionId);
            session.SpecPath = specPath;
            session.NewGeneratedPath = string.IsNullOrWhiteSpace(newGeneratedPath) ? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tsp-gen-" + session.SessionId) : newGeneratedPath;
            session.Status = "Regenerated"; // Placeholder (real implementation would invoke tsp-client)
            return new UpdateResponse { Session = session, Message = "Regenerate stage complete (skeleton)" };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Regenerate failed: {specPath}", specPath);
            SetFailure();
            return new UpdateResponse { ResponseError = ex.Message };
        }
    }

    [McpServerTool(Name = "tsp_update_diff"), Description("Stage2 diff placeholder")]
    public async Task<UpdateResponse> Diff(string sessionId, string? oldGeneratedPath = null, string? newGeneratedPath = null, CancellationToken ct = default)
    {
        try
        {
            var session = RequireSession(sessionId);
            if (!string.IsNullOrWhiteSpace(oldGeneratedPath)) session.OldGeneratedPath = oldGeneratedPath;
            if (!string.IsNullOrWhiteSpace(newGeneratedPath)) session.NewGeneratedPath = newGeneratedPath;

            // Dummy API changes
            session.ApiChanges = [
                new ApiChange { Kind = "SignatureChanged", Symbol = "Client.GetFoo", Detail = "param bar: int -> string" },
                new ApiChange { Kind = "MethodRemoved", Symbol = "Client.DeleteFoo", Detail = "Removed in new spec" }
            ];
            session.Status = "Diffed";
            return new UpdateResponse { Session = session, Message = "Diff stage complete (dummy changes)" };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Diff failed: {sessionId}", sessionId);
            SetFailure();
            return new UpdateResponse { ResponseError = ex.Message };
        }
    }

    [McpServerTool(Name = "tsp_update_map"), Description("Stage2 impact mapping placeholder")]
    public async Task<UpdateResponse> Map(string sessionId, CancellationToken ct = default)
    {
        try
        {
            var session = RequireSession(sessionId);
            if (session.ApiChanges.Count == 0)
            {
                return new UpdateResponse { Session = session, Message = "No API changes to map" };
            }
            // Dummy impacted customizations
            session.ImpactedCustomizations = [
                new CustomizationImpact { File = "src/custom/ClientExtensions.cs", Reasons = ["Uses Client.GetFoo"] },
                new CustomizationImpact { File = "src/custom/DeleteHelpers.cs", Reasons = ["Calls Client.DeleteFoo"] }
            ];
            session.DirectMergeFiles = ["src/custom/UnchangedHelper.cs"]; // placeholder
            session.Status = "Mapped";
            return new UpdateResponse { Session = session, Message = "Impact mapping complete (dummy)" };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Map failed: {sessionId}", sessionId);
            SetFailure();
            return new UpdateResponse { ResponseError = ex.Message };
        }
    }

    [McpServerTool(Name = "tsp_update_merge"), Description("Stage3 direct merge placeholder")]
    public async Task<UpdateResponse> Merge(string sessionId, CancellationToken ct = default)
    {
        try
        {
            var session = RequireSession(sessionId);
            if (session.DirectMergeFiles.Count == 0)
            {
                return new UpdateResponse { Session = session, Message = "No direct merge files" };
            }
            // Dummy: mark merged
            session.Status = "Merged";
            return new UpdateResponse { Session = session, Message = $"Merged {session.DirectMergeFiles.Count} files (dummy)" };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Merge failed: {sessionId}", sessionId);
            SetFailure();
            return new UpdateResponse { ResponseError = ex.Message };
        }
    }

    [McpServerTool(Name = "tsp_update_propose"), Description("Stage4 propose patches placeholder")]
    public async Task<UpdateResponse> Propose(string sessionId, string? filesCsv = null, CancellationToken ct = default)
    {
        try
        {
            var session = RequireSession(sessionId);
            var targetFiles = ParseFiles(filesCsv, session.ImpactedCustomizations.Select(i => i.File));
            foreach (var f in targetFiles)
            {
                if (session.ProposedPatches.Any(p => p.File.Equals(f, StringComparison.OrdinalIgnoreCase))) continue;
                session.ProposedPatches.Add(new PatchProposal
                {
                    File = f,
                    Diff = $"--- a/{f}\n+++ b/{f}\n@@ line @@\n- old line\n+ updated line (dummy)\n",
                    Rationale = "Adjust for API change (dummy)"
                });
            }
            session.Status = "PatchesProposed";
            return new UpdateResponse { Session = session, Message = $"Proposed {targetFiles.Count} patches (dummy)" };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Propose failed: {sessionId}", sessionId);
            SetFailure();
            return new UpdateResponse { ResponseError = ex.Message };
        }
    }

    [McpServerTool(Name = "tsp_update_apply"), Description("Stage4 apply patches placeholder")]
    public async Task<UpdateResponse> Apply(string sessionId, bool dryRun = false, CancellationToken ct = default)
    {
        try
        {
            var session = RequireSession(sessionId);
            if (session.ProposedPatches.Count == 0)
            {
                return new UpdateResponse { Session = session, Message = "No patches to apply" };
            }
            // Dummy apply (no file writes yet)
            session.Status = dryRun ? "AppliedDryRun" : "Applied";
            return new UpdateResponse { Session = session, Message = dryRun ? "Dry-run apply complete" : "Patches applied (dummy)" };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Apply failed: {sessionId}", sessionId);
            SetFailure();
            return new UpdateResponse { ResponseError = ex.Message };
        }
    }

    [McpServerTool(Name = "tsp_update_status"), Description("Get current session state")]
    public async Task<UpdateResponse> Status(string sessionId, CancellationToken ct = default)
    {
        try
        {
            var session = RequireSession(sessionId);
            return new UpdateResponse { Session = session, Message = "OK" };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Status failed: {sessionId}", sessionId);
            SetFailure();
            return new UpdateResponse { ResponseError = ex.Message };
        }
    }

    // --------------- CLI Handlers (invoke MCP methods) ---------------
    private async Task HandleRegenerate(InvocationContext ctx, CancellationToken ct)
    {
        var spec = ctx.ParseResult.GetValueForArgument(_specPathArg);
        var sid = ctx.ParseResult.GetValueForOption(_sessionIdOpt);
        var newGen = ctx.ParseResult.GetValueForOption(_generatedNewOpt);
        var resp = await Regenerate(spec, string.IsNullOrWhiteSpace(sid) ? null : sid, string.IsNullOrWhiteSpace(newGen) ? null : newGen, ct);
        ctx.ExitCode = ExitCode; output.Output(resp);
    }

    private async Task HandleDiff(InvocationContext ctx, CancellationToken ct)
    {
        var sid = ctx.ParseResult.GetValueForOption(_sessionIdOpt);
        var oldGen = ctx.ParseResult.GetValueForOption(_generatedOldOpt);
        var newGen = ctx.ParseResult.GetValueForOption(_generatedNewOpt);
        var resp = await Diff(RequireSessionIdOrError(sid, ctx), oldGen, newGen, ct);
        ctx.ExitCode = ExitCode; output.Output(resp);
    }

    private async Task HandleMap(InvocationContext ctx, CancellationToken ct)
    {
        var sid = ctx.ParseResult.GetValueForOption(_sessionIdOpt);
        var resp = await Map(RequireSessionIdOrError(sid, ctx), ct);
        ctx.ExitCode = ExitCode; output.Output(resp);
    }

    private async Task HandleMerge(InvocationContext ctx, CancellationToken ct)
    {
        var sid = ctx.ParseResult.GetValueForOption(_sessionIdOpt);
        var resp = await Merge(RequireSessionIdOrError(sid, ctx), ct);
        ctx.ExitCode = ExitCode; output.Output(resp);
    }

    private async Task HandlePropose(InvocationContext ctx, CancellationToken ct)
    {
        var sid = ctx.ParseResult.GetValueForOption(_sessionIdOpt);
        var files = ctx.ParseResult.GetValueForOption(_filesOpt);
        var resp = await Propose(RequireSessionIdOrError(sid, ctx), string.IsNullOrWhiteSpace(files) ? null : files, ct);
        ctx.ExitCode = ExitCode; output.Output(resp);
    }

    private async Task HandleApply(InvocationContext ctx, CancellationToken ct)
    {
        var sid = ctx.ParseResult.GetValueForOption(_sessionIdOpt);
        var dry = ctx.ParseResult.GetValueForOption(_dryRunOpt);
        var resp = await Apply(RequireSessionIdOrError(sid, ctx), dry, ct);
        ctx.ExitCode = ExitCode; output.Output(resp);
    }

    private async Task HandleStatus(InvocationContext ctx, CancellationToken ct)
    {
        var sid = ctx.ParseResult.GetValueForOption(_sessionIdOpt);
        var resp = await Status(RequireSessionIdOrError(sid, ctx), ct);
        ctx.ExitCode = ExitCode; output.Output(resp);
    }

    // --------------- Helpers ---------------
    private UpdateSessionState GetOrCreateSession(string? sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId) && _sessions.TryGetValue(sessionId, out var existing)) return existing;
        var session = new UpdateSessionState();
        _sessions[session.SessionId] = session;
        return session;
    }

    private UpdateSessionState RequireSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("sessionId required");
        if (!_sessions.TryGetValue(sessionId, out var s)) throw new InvalidOperationException($"Unknown session: {sessionId}");
        return s;
    }

    private string RequireSessionIdOrError(string sessionId, InvocationContext ctx)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            SetFailure();
            ctx.ExitCode = ExitCode;
            throw new CommandException("--session is required");
        }
        return sessionId;
    }

    private List<string> ParseFiles(string? csv, IEnumerable<string> fallback)
    {
        if (string.IsNullOrWhiteSpace(csv)) return fallback.ToList();
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}
