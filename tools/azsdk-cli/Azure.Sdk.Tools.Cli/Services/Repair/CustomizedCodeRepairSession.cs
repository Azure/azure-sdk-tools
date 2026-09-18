// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services.Languages;

namespace Azure.Sdk.Tools.Cli.Services.Repair;

internal sealed class CustomizedCodeRepairSession(
    CustomizedCodeRepairRequest request,
    IGitHelper gitHelper,
    ITypeSpecHelper typeSpecHelper,
    ITspClientHelper tspClientHelper,
    IFeedbackClassifierService classifier,
    IRepairSourceState sourceState,
    IRepairArtifacts artifacts,
    TimeProvider timeProvider,
    ILogger logger)
{
    private readonly CustomizedCodeRepairResult _repair = new()
    {
        MaxAttempts = request.MaxAttempts,
        TimeoutMinutes = request.TimeoutMinutes
    };
    private readonly ConcurrentQueue<AppliedPatch> _patches = new();
    private readonly HashSet<string> _failedTrees = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failedCustomStates = new(StringComparer.Ordinal);
    private readonly List<string> _patchDiffs = [];
    private readonly CustomizedCodeUpdateResponse _response = new();
    private string _repositoryRoot = "";
    private string _packagePath = "";
    private string? _localSpecPath;
    private string? _localSpecRoot;
    private string _artifactPath = "";
    private LanguageService _language = null!;
    private CustomizationFilePolicy _policy = null!;
    private RepairSourceSnapshot? _state;
    private CustomizedCodeRepairStage? _activeStage;
    private string? _buildDiagnostics;
    private string _classificationContext = "";
    private CancellationToken _callerToken;
    private long _stageStartedAt;

    public async Task<CustomizedCodeUpdateResponse> RunAsync(
        Func<string, CancellationToken, Task<LanguageService>> resolveLanguage,
        CancellationToken ct)
    {
        _response.Repair = _repair;
        _callerToken = ct;
        if (request.EditScope != EditScope.CustomCode || request.MaxAttempts is < 1 or > 10 ||
            request.TimeoutMinutes is < 1 or > 120)
        {
            Fail("invalid_input", "Build repair requires CustomCode scope, maxAttempts in 1..10, and timeoutMinutes in 1..120.");
            return _response;
        }

        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(request.TimeoutMinutes), timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        var token = linked.Token;
        try
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(request.CustomizationRequest) ||
                string.IsNullOrWhiteSpace(request.PackagePath) || !Directory.Exists(request.PackagePath))
            {
                throw new RepairStoppedException("invalid_input", "A customization request and an existing SDK package directory are required.");
            }
            _packagePath = Path.GetFullPath(request.PackagePath);
            _repositoryRoot = await gitHelper.DiscoverRepoRootAsync(_packagePath, token);
            if (!string.IsNullOrWhiteSpace(request.TypeSpecProjectPath))
            {
                if (!typeSpecHelper.IsValidTypeSpecProjectPath(request.TypeSpecProjectPath))
                {
                    throw new RepairStoppedException("invalid_input", "The local TypeSpec project must exist and contain tspconfig.yaml.");
                }
                _localSpecPath = Path.GetFullPath(request.TypeSpecProjectPath);
                _localSpecRoot = await gitHelper.DiscoverRepoRootAsync(_localSpecPath, token);
                if (RealPath.GetRealPath(_localSpecRoot).Equals(RealPath.GetRealPath(_repositoryRoot)))
                {
                    throw new RepairStoppedException("invalid_input", "Build repair requires a separate, unchanged local spec repository.");
                }
            }

            _artifactPath = artifacts.CreateDirectory(request.ArtifactsPath, _repair.SessionId, _repositoryRoot, _localSpecRoot);
            _repair.ArtifactsPath = _artifactPath;
            await CheckpointAsync(token);
            _language = await resolveLanguage(_packagePath, token);
            if (_language == null || !_language.IsCustomizedCodeUpdateSupported)
            {
                throw new RepairStoppedException("invalid_input", "The package language does not support customized-code repair.");
            }
            _response.Language = _language.Language;
            _policy = new CustomizationFilePolicy(_language.Language, _packagePath);
            _state = await sourceState.CaptureAsync(_repositoryRoot, _artifactPath, token);
            var pinPath = Path.Combine(_packagePath, "tsp-location.yaml");
            if (RequiresGeneration && !File.Exists(pinPath))
            {
                throw new RepairStoppedException("invalid_input", "tsp-location.yaml is required for .NET/Java build repair.");
            }
            _repair.Input = new CustomizedCodeRepairInput
            {
                RepositoryRoot = _repositoryRoot,
                PackagePath = _packagePath,
                RequestSha256 = Hash(Encoding.UTF8.GetBytes(request.CustomizationRequest)),
                BaseHead = _state.Head,
                InitialTree = _state.Tree,
                TspLocationPath = Path.GetRelativePath(_repositoryRoot, pinPath).Replace('\\', '/'),
                TspLocationSha256 = await ReadPinHashAsync(token)
            };
            if (_localSpecRoot != null)
            {
                var spec = await sourceState.CaptureAsync(_localSpecRoot, _artifactPath, token);
                _repair.Input.LocalSpec = new CustomizedCodeRepairLocalSpec
                {
                    ProjectPath = _localSpecPath!,
                    RepositoryRoot = _localSpecRoot,
                    InitialHead = spec.Head,
                    InitialTree = spec.Tree
                };
            }
            await CheckpointAsync(token);

            var initiallyValid = await ValidateAsync(0, token);
            var customizationsRequested = await ClassifyAsync(token);
            if (initiallyValid && !customizationsRequested)
            {
                Complete();
            }
            else
            {
                if (!initiallyValid) { await RememberFailureAsync(token); }
                var customizationRoot = _language.HasCustomizations(_packagePath, token);
                if (customizationRoot == null || _policy.GetCustomizationFiles().Count == 0)
                {
                    throw new RepairStoppedException("no_customizations", "The build failed, but no custom-code files are available to repair.");
                }
                var context = BuildContext();
                try
                {
                    await _language.RunRepairSessionAsync(customizationRoot, _packagePath, context, request.MaxAttempts,
                        StartAttemptAsync, CompleteAttemptAsync, _patches.Enqueue, token);
                }
                catch (OperationCanceledException) { throw; }
                catch (TimeoutException) { throw; }
                catch (RepairStoppedException) { throw; }
                catch (CopilotCliUnavailableException) { throw; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "The customization repair conversation failed");
                    throw new RepairStoppedException("agent_failed", $"The repair conversation failed: {ex.Message}");
                }
                token.ThrowIfCancellationRequested();
                if (_repair.TerminalReason == null)
                {
                    throw new RepairStoppedException("agent_failed", "The repair conversation ended without a code-validated terminal outcome.");
                }
            }

            // The conversation has been disposed. No model can mutate the source after this check.
            await CaptureAndCheckAsync(_state!, "final", _ => false, token);
            if (_repair.Validation.Succeeded && _repair.Validation.ValidatedTree != _state!.Tree)
            {
                throw new RepairStoppedException("source_changed", "The final source tree differs from the validated build.");
            }
        }
        catch (OperationCanceledException ex)
        {
            var reason = ct.IsCancellationRequested ? "cancelled" : "timed_out";
            MarkInterruptedStage(reason, ex.Message);
            Fail(reason, ct.IsCancellationRequested ? "Build repair was cancelled." : "Build repair exceeded a time limit.");
        }
        catch (TimeoutException ex)
        {
            MarkInterruptedStage("timed_out", ex.Message);
            Fail("timed_out", ex.Message);
        }
        catch (RepairStoppedException ex)
        {
            Fail(ex.Reason, ex.Message);
        }
        catch (CopilotCliUnavailableException ex)
        {
            Fail("infrastructure_failure", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Build repair failed");
            Fail("unexpected_error", ex.Message);
        }

        _response.AppliedPatches = [.. _patches];
        if (_artifactPath.Length > 0)
        {
            // Cleanup records do not run generation/build and cannot authorize success.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                if (!_response.Success && _repair.Input != null)
                {
                    await CapturePartialStateAsync(cleanup.Token);
                }
                await CheckpointAsync(cleanup.Token);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException or InvalidOperationException)
            {
                logger.LogError(ex, "Could not persist final repair evidence");
                Fail("infrastructure_failure", $"Could not persist final repair evidence: {ex.Message}");
            }
        }
        return _response;
    }

    private bool RequiresGeneration => _language.Language is SdkLanguage.DotNet or SdkLanguage.Java;

    private async Task<bool> ValidateAsync(int attempt, CancellationToken ct)
    {
        _repair.Validation = new();
        var required = new List<string>();
        if (RequiresGeneration)
        {
            var prepared = await RunStageAsync(attempt, "prepare", async () =>
            {
                var result = await _language.PrepareForGenerationAsync(_repositoryRoot, ct);
                return (result.Success, result.Diagnostics, !result.Required);
            }, _ => false, ct);
            if (!prepared.Success)
            {
                throw new RepairStoppedException("preparation_failed", prepared.Diagnostic ?? "Required generation preparation failed.");
            }
            if (!prepared.NotRequired) { required.Add(StageId(attempt, "prepare")); }

            var generated = await RunStageAsync(attempt, "generate", async () =>
            {
                var result = await tspClientHelper.UpdateGenerationAsync(_packagePath, localSpecRepoPath: _localSpecPath, isCli: false, ct: ct);
                return (result.IsSuccessful, result.ResponseError, false);
            }, _policy.IsGeneratedFile, ct);
            required.Add(StageId(attempt, "generate"));
            if (!generated.Success)
            {
                throw new RepairStoppedException("generation_failed", generated.Diagnostic ?? "Required SDK regeneration failed.");
            }
        }
        else
        {
            await RunStageAsync(attempt, "prepare", () => Task.FromResult((true, (string?)null, true)), _ => false, ct);
            await RunStageAsync(attempt, "generate", () => Task.FromResult((true, (string?)null, true)), _ => false, ct);
        }
        var built = await RunStageAsync(attempt, "build", async () =>
        {
            var result = await _language.BuildAsync(_packagePath, Math.Min(request.TimeoutMinutes, 30), ct);
            return (result.Success, result.ErrorMessage, false);
        }, _ => false, ct);
        required.Add(StageId(attempt, "build"));
        _buildDiagnostics = built.Diagnostic;
        _response.BuildResult = built.Success ? null : built.Diagnostic;
        _repair.Validation = new CustomizedCodeRepairValidation
        {
            Succeeded = built.Success,
            ValidatedTree = built.Success ? _state!.Tree : null,
            BuildStageId = StageId(attempt, "build"),
            RequiredStageIds = required
        };
        await CheckpointAsync(ct);
        return built.Success;
    }

    private async Task<bool> ClassifyAsync(CancellationToken ct)
    {
        List<FeedbackItem> items = [];
        FeedbackClassificationResponse? result = null;
        await RunStageAsync(0, "classify", async () =>
        {
            result = await classifier.ClassifyItemsAsync(items,
                globalContext: BuildContext(), tspProjectPath: _localSpecPath,
                apiViewUrl: request.ApiViewUrl,
                plainTextFeedback: request.ApiViewUrl == null ? request.CustomizationRequest : null,
                language: _language.Language.ToString(),
                editScope: EditScope.CustomCode, ct: ct);
            return (true, JsonSerializer.Serialize(result), false);
        }, _ => false, ct);
        if (result?.Classifications is not { Count: > 0 } classifications)
        {
            throw new RepairStoppedException("agent_failed", "Build diagnostics could not be classified.");
        }
        if (classifications.Any(c => c.Classification is not ("TSP_APPLICABLE" or "CODE_CUSTOMIZATION" or "REQUIRES_MANUAL_INTERVENTION" or "SUCCESS")))
        {
            throw new RepairStoppedException("agent_failed", "The classifier returned an unsupported classification.");
        }
        _classificationContext = JsonSerializer.Serialize(classifications);
        var spec = classifications.Where(c => c.Classification == "TSP_APPLICABLE").Select(c => $"{c.Text}: {c.Reason}").ToList();
        var manual = classifications.Where(c => c.Classification == "REQUIRES_MANUAL_INTERVENTION").Select(c => $"{c.Text}: {c.Reason}").ToList();
        if (spec.Count > 0)
        {
            _response.SpecChangeRequired = spec;
            throw new RepairStoppedException("spec_change_required", "The build repair requires a spec change outside CustomCode scope.");
        }
        if (manual.Count > 0)
        {
            _response.NextSteps = manual;
            throw new RepairStoppedException("manual_intervention_required", "The build repair requires manual intervention.");
        }
        return classifications.Any(c => c.Classification == "CODE_CUSTOMIZATION");
    }

    private async Task StartAttemptAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_repair.AttemptsUsed >= request.MaxAttempts)
        {
            throw new RepairStoppedException("attempt_limit", "The repair attempt limit was reached.");
        }
        await CaptureAndCheckAsync(_state!, $"before-attempt-{_repair.AttemptsUsed + 1}", _ => false, ct);
        var attempt = new CustomizedCodeRepairAttempt
        {
            Number = _repair.AttemptsUsed + 1,
            SourceTreeBefore = _state!.Tree
        };
        _repair.Attempts.Add(attempt);
        _activeStage = NewStage(attempt.Number, "patch", _state.Tree);
        await CheckpointAsync(ct);
    }

    private async Task<CopilotAgentTurnResult<string>> CompleteAttemptAsync(string? summary, CancellationToken ct)
    {
        var attempt = _repair.Attempts[^1];
        attempt.Hypothesis = summary;
        var beforePatch = _state!;
        try
        {
            await CaptureAndCheckAsync(beforePatch, $"attempt-{attempt.Number}-patch", _policy.IsCustomFile, ct);
            attempt.SourceTreeAfterPatch = _state!.Tree;
            _activeStage!.SourceTreeAfter = _state.Tree;
            _activeStage.Status = "succeeded";
            _activeStage.DiagnosticSummary = summary;
            _activeStage.ElapsedMilliseconds = (long)timeProvider.GetElapsedTime(_stageStartedAt).TotalMilliseconds;
            _activeStage = null;
            attempt.PatchDiffPath = $"attempt-{attempt.Number}/patch.diff";
            var diff = await sourceState.GetDiffAsync(_repositoryRoot, beforePatch.Tree, _state.Tree, ct);
            await artifacts.WriteTextAsync(_artifactPath, attempt.PatchDiffPath, diff, ct);
            attempt.ReversedPatchCount = RepairProgress.CountReversedHunks(diff, _patchDiffs);
            _patchDiffs.Add(diff);
            if (beforePatch.Tree == _state.Tree)
            {
                throw new RepairStoppedException("no_progress", "The patch turn made no effective source changes.");
            }
            if (_failedTrees.Contains(_state.Tree))
            {
                throw new RepairStoppedException("repeated_state", "The patch returned to a previously failed source state.");
            }
            var beforeValidation = _state;
            var succeeded = await ValidateAsync(attempt.Number, ct);
            attempt.SourceTreeAfterValidation = _state.Tree;
            attempt.ValidationDiffPath = $"attempt-{attempt.Number}/validation.diff";
            await artifacts.WriteTextAsync(_artifactPath, attempt.ValidationDiffPath,
                await sourceState.GetDiffAsync(_repositoryRoot, beforeValidation.Tree, _state.Tree, ct), ct);
            attempt.DiagnosticSummary = _buildDiagnostics;
            if (succeeded)
            {
                Complete();
                return new(false, null, "");
            }
            var customState = await GetCustomFailureIdentityAsync(ct);
            if (_failedTrees.Contains(_state.Tree) || _failedCustomStates.Contains(customState))
            {
                throw new RepairStoppedException("repeated_state", "Validation returned to a previously failed source/diagnostic state.");
            }
            await RememberFailureAsync(ct);
            if (_repair.AttemptsUsed >= request.MaxAttempts)
            {
                throw new RepairStoppedException("attempt_limit", "The build still fails after the permitted repair attempts.");
            }
            await CheckpointAsync(ct);
            return new(true, BuildContext(), null);
        }
        catch (RepairStoppedException ex)
        {
            attempt.DiagnosticSummary = ex.Message;
            Fail(ex.Reason, ex.Message);
            await CheckpointAsync(ct);
            return new(false, null, "");
        }
    }

    private async Task<(bool Success, string? Diagnostic, bool NotRequired)> RunStageAsync(
        int attempt,
        string name,
        Func<Task<(bool Success, string? Diagnostic, bool NotRequired)>> action,
        Func<string, bool> allowed,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var before = _state!;
        await CaptureAndCheckAsync(before, $"before-{StageId(attempt, name)}", _ => false, ct);
        var stage = NewStage(attempt, name, before.Tree);
        _activeStage = stage;
        var started = timeProvider.GetTimestamp();
        await CheckpointAsync(ct);
        try
        {
            var result = await action();
            ct.ThrowIfCancellationRequested();
            stage.Status = !result.Success ? "failed" : result.NotRequired ? "not_required" : "succeeded";
            stage.DiagnosticSummary = result.Diagnostic;
            stage.DiagnosticsPath = $"attempt-{attempt}/{name}.log";
            await artifacts.WriteTextAsync(_artifactPath, stage.DiagnosticsPath, result.Diagnostic ?? "Stage completed without diagnostics.", ct);
            await CaptureAndCheckAsync(before, stage.Id, allowed, ct);
            stage.SourceTreeAfter = _state!.Tree;
            return result;
        }
        catch (OperationCanceledException ex)
        {
            stage.Status = _callerToken.IsCancellationRequested ? "cancelled" : "timed_out";
            stage.DiagnosticSummary = ex.Message;
            throw;
        }
        catch (TimeoutException ex)
        {
            stage.Status = "timed_out";
            stage.DiagnosticSummary = ex.Message;
            throw;
        }
        catch (RepairStoppedException) { stage.Status = "failed"; throw; }
        catch (Exception ex)
        {
            stage.Status = "failed";
            stage.DiagnosticSummary = ex.Message;
            await CaptureAndCheckAsync(before, stage.Id, allowed, ct);
            throw new RepairStoppedException(name switch
            {
                "prepare" => "preparation_failed",
                "generate" => "generation_failed",
                "build" => "build_failed",
                _ => "agent_failed"
            }, $"{name} failed: {ex.Message}");
        }
        finally
        {
            stage.ElapsedMilliseconds = (long)timeProvider.GetElapsedTime(started).TotalMilliseconds;
            if (!ct.IsCancellationRequested) { await CheckpointAsync(ct); }
            if (!ct.IsCancellationRequested) { _activeStage = null; }
        }
    }

    private CustomizedCodeRepairStage NewStage(int attempt, string name, string tree)
    {
        _stageStartedAt = timeProvider.GetTimestamp();
        var stage = new CustomizedCodeRepairStage
        {
            Id = StageId(attempt, name),
            Attempt = attempt,
            Name = name,
            SourceTreeBefore = tree
        };
        _repair.Stages.Add(stage);
        if (attempt > 0) { _repair.Attempts[attempt - 1].StageIds.Add(stage.Id); }
        return stage;
    }

    private async Task CaptureAndCheckAsync(RepairSourceSnapshot before, string label, Func<string, bool> allowed, CancellationToken ct)
    {
        var after = await sourceState.CaptureAsync(_repositoryRoot, _artifactPath, ct);
        _state = after;
        if (after.Head != _repair.Input!.BaseHead)
        {
            throw new RepairStoppedException("source_changed", "The SDK HEAD changed during repair.");
        }
        var changes = await sourceState.GetChangesAsync(_repositoryRoot, before.Tree, after.Tree, ct);
        if (changes.Count > 0)
        {
            await artifacts.WriteTextAsync(_artifactPath, $"stages/{label}.diff",
                await sourceState.GetDiffAsync(_repositoryRoot, before.Tree, after.Tree, ct), ct);
        }
        var forbidden = changes.Where(change =>
            !allowed(Path.Combine(_repositoryRoot, change.Path)) ||
            !IsRegularFileChange(change)).ToList();
        if (forbidden.Count > 0)
        {
            throw new RepairStoppedException("scope_violation",
                $"Source changes outside the permitted {label} scope: {string.Join(", ", forbidden.Select(c => c.Path))}");
        }
        var pinHash = await ReadPinHashAsync(ct);
        if (pinHash != _repair.Input.TspLocationSha256)
        {
            throw new RepairStoppedException("scope_violation", "tsp-location.yaml changed during build repair.");
        }
        if (_repair.Input.LocalSpec is { } localSpec)
        {
            var currentSpec = await sourceState.CaptureAsync(localSpec.RepositoryRoot, _artifactPath, ct);
            localSpec.FinalHead = currentSpec.Head;
            localSpec.FinalTree = currentSpec.Tree;
            if (currentSpec.Head != localSpec.InitialHead || currentSpec.Tree != localSpec.InitialTree)
            {
                await artifacts.WriteTextAsync(_artifactPath, $"stages/{label}-spec.diff",
                    await sourceState.GetDiffAsync(localSpec.RepositoryRoot, localSpec.InitialTree, currentSpec.Tree, ct), ct);
                throw new RepairStoppedException("scope_violation", "The explicit local TypeSpec source changed during repair.");
            }
        }
        await UpdateFinalStateAsync(after, pinHash, ct);
    }

    private async Task UpdateFinalStateAsync(RepairSourceSnapshot snapshot, string? pinHash, CancellationToken ct)
    {
        var changes = await sourceState.GetChangesAsync(_repositoryRoot, _repair.Input!.InitialTree, snapshot.Tree, ct);
        _repair.FinalState = new CustomizedCodeRepairState
        {
            Head = snapshot.Head,
            Tree = snapshot.Tree,
            TspLocationSha256 = pinHash,
            ChangedFiles = changes.Select(c => new CustomizedCodeRepairChange(c.Path, c.Status)).ToList()
        };
    }

    private async Task CapturePartialStateAsync(CancellationToken ct)
    {
        var final = await sourceState.CaptureAsync(_repositoryRoot, _artifactPath, ct);
        await UpdateFinalStateAsync(final, await ReadPinHashAsync(ct), ct);
        await artifacts.WriteTextAsync(_artifactPath, "partial.diff",
            await sourceState.GetDiffAsync(_repositoryRoot, _repair.Input!.InitialTree, final.Tree, ct), ct);
        if (_repair.Attempts.LastOrDefault() is { } attempt && attempt.SourceTreeBefore != null)
        {
            var partialPath = $"attempt-{attempt.Number}/partial.diff";
            attempt.PatchDiffPath ??= partialPath;
            await artifacts.WriteTextAsync(_artifactPath, partialPath,
                await sourceState.GetDiffAsync(_repositoryRoot, attempt.SourceTreeBefore, final.Tree, ct), ct);
        }
    }

    private async Task RememberFailureAsync(CancellationToken ct)
    {
        _failedTrees.Add(_state!.Tree);
        _failedCustomStates.Add(await GetCustomFailureIdentityAsync(ct));
    }

    private async Task<string> GetCustomFailureIdentityAsync(CancellationToken ct)
    {
        var identity = new StringBuilder();
        foreach (var file in _policy.GetCustomizationFiles().Order(StringComparer.Ordinal))
        {
            identity.Append(Path.GetRelativePath(_packagePath, file)).Append('\0');
            identity.Append(Hash(await File.ReadAllBytesAsync(file, ct))).Append('\0');
        }
        identity.Append(_buildDiagnostics);
        return Hash(Encoding.UTF8.GetBytes(identity.ToString()));
    }

    private async Task<string?> ReadPinHashAsync(CancellationToken ct)
    {
        var path = Path.Combine(_packagePath, "tsp-location.yaml");
        return File.Exists(path) ? Hash(await File.ReadAllBytesAsync(path, ct)) : null;
    }

    private string BuildContext() =>
        $"""
        Repair the current failing build, not the diagnostic count. The host owns all generation and build commands.
        Original request:
        {request.CustomizationRequest}
        Classifier analysis:
        {_classificationContext}
        Current build diagnostics:
        {_buildDiagnostics}
        The last deterministic build validation succeeded: {_repair.Validation.Succeeded}.
        Previous attempts (preserve hypotheses and do not return to failed source states):
        {JsonSerializer.Serialize(_repair.Attempts)}
        Actual prior patch diffs:
        {string.Join("\n", _patchDiffs)}
        """;

    private void Complete()
    {
        _response.Success = true;
        _response.BuildValidated = true;
        _response.ResponseError = null;
        _response.ErrorCode = null;
        var unchanged = _repair.Input!.InitialTree == _state!.Tree;
        _repair.TerminalReason = unchanged && _repair.AttemptsUsed == 0 ? "already_green" : "repaired";
        _repair.RepairKind = _repair.AttemptsUsed > 0 ? "custom_code" : unchanged ? "none" : "generation_only";
        _response.Message = _repair.TerminalReason == "already_green"
            ? "The unchanged package passed deterministic build validation."
            : "The repaired source passed deterministic build validation.";
    }

    private void Fail(string reason, string message)
    {
        if (_activeStage != null)
        {
            _activeStage.DiagnosticSummary ??= message;
            _activeStage.ElapsedMilliseconds = (long)timeProvider.GetElapsedTime(_stageStartedAt).TotalMilliseconds;
        }
        _repair.TerminalReason = reason;
        _repair.Validation.Succeeded = false;
        _repair.Validation.ValidatedTree = null;
        _response.Success = false;
        _response.BuildValidated = false;
        _response.ResponseError = message;
        _response.Message = message;
        _response.ErrorCode = reason switch
        {
            "invalid_input" => CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
            "spec_change_required" => CustomizedCodeUpdateResponse.KnownErrorCodes.SpecChangeRequired,
            "manual_intervention_required" => CustomizedCodeUpdateResponse.KnownErrorCodes.ManualInterventionRequired,
            "generation_failed" => _repair.AttemptsUsed == 0
                ? CustomizedCodeUpdateResponse.KnownErrorCodes.RegenerateFailed
                : CustomizedCodeUpdateResponse.KnownErrorCodes.RegenerateAfterPatchesFailed,
            "no_customizations" => CustomizedCodeUpdateResponse.KnownErrorCodes.BuildNoCustomizationsFailed,
            "no_progress" => CustomizedCodeUpdateResponse.KnownErrorCodes.PatchesFailed,
            _ => string.Concat(reason.Split('_').Select(word => char.ToUpperInvariant(word[0]) + word[1..]))
        };
    }

    private void MarkInterruptedStage(string reason, string message)
    {
        if (_activeStage != null)
        {
            _activeStage.Status = reason;
            _activeStage.DiagnosticSummary = message;
            _activeStage.ElapsedMilliseconds = (long)timeProvider.GetElapsedTime(_stageStartedAt).TotalMilliseconds;
        }
        if (_repair.Attempts.LastOrDefault() is { } attempt) { attempt.DiagnosticSummary = message; }
    }

    private Task CheckpointAsync(CancellationToken ct) =>
        artifacts.WriteJsonAsync(_artifactPath, "result.json", _response, ct);

    private static string StageId(int attempt, string name) => $"attempt-{attempt}-{name}";
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static bool IsRegularFileChange(RepairSourceChange change) =>
        change.OldMode is "000000" or "100644" or "100755" &&
        change.NewMode is "000000" or "100644" or "100755" &&
        (change.OldMode == "000000" || change.NewMode == "000000" || change.OldMode == change.NewMode);

    private sealed class RepairStoppedException(string reason, string message) : Exception(message)
    {
        public string Reason { get; } = reason;
    }
}
