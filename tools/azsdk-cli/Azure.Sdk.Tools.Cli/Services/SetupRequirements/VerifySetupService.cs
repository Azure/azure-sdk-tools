// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.SetupRequirements;

/// <summary>
/// Core service for verifying environment setup and optionally installing missing requirements.
/// Shared by VerifySetupTool (check) and VerifySetupInstallTool (install).
/// </summary>
public class VerifySetupService : IVerifySetupService
{
    private const string OUTPUT_VERSION_PATTERN = @"[\d\.]+";

    private readonly IProcessHelper processHelper;
    private readonly ILogger<VerifySetupService> logger;
    private readonly IGitHelper gitHelper;

    public VerifySetupService(
        IProcessHelper processHelper,
        ILogger<VerifySetupService> logger,
        IGitHelper gitHelper)
    {
        this.processHelper = processHelper;
        this.logger = logger;
        this.gitHelper = gitHelper;
    }

    /// <summary>
    /// Verifies the developer environment for MCP release tool requirements.
    /// </summary>
    public async Task<VerifySetupResponse> VerifySetup(HashSet<SdkLanguage>? langs = null, string? packagePath = null, List<string>? requirementsToInstall = null, CancellationToken ct = default)
    {
        try
        {
            // Create context for filtering requirements
            var ctx = await CreateRequirementContext(packagePath ?? Environment.CurrentDirectory, langs, ct);

            // Get all requirements that should be checked in this context
            var reqsToCheck = AllRequirements.All
                .Where(r => r.ShouldCheck(ctx))
                .ToList();

            VerifySetupResponse response = new VerifySetupResponse
            {
                Results = new List<RequirementCheckResult>()
            };

            // Split requirements into two passes:
            // 1. Requirements with no dependencies (can run concurrently)
            // 2. Requirements with dependencies (run after pass 1 so dependency status is known)
            var noDeps = reqsToCheck.Where(r => r.DependsOn.Count == 0).ToList();
            var hasDeps = reqsToCheck.Where(r => r.DependsOn.Count > 0).ToList();

            // Pass 1: Check requirements with no dependencies concurrently
            var checkTasks = new List<Task<(Requirement req, DefaultCommandResponse result)>>();
            foreach (var req in noDeps)
            {
                logger.LogInformation("Checking requirement: {Requirement}", req.Name);
                var task = RunCheck(req, ctx, ct).ContinueWith(t => (req, t.Result), TaskScheduler.Default);
                checkTasks.Add(task);
            }
            var pass1Results = await Task.WhenAll(checkTasks);

            // Populate FailedRequirements from pass 1 before running pass 2,
            // so that dependency checks in pass 2 see all pass 1 failures.
            foreach (var (req, result) in pass1Results)
            {
                if (result.Message != null)
                {
                    ctx.FailedRequirements.Add(req.Name);
                }
            }

            // Pass 2: Check requirements with dependencies sequentially
            // (sequential so transitive dependency failures are visible to later checks)
            var pass2Results = new List<(Requirement req, DefaultCommandResponse result)>();
            foreach (var req in hasDeps)
            {
                logger.LogInformation("Checking requirement: {Requirement}", req.Name);
                var result = await RunCheck(req, ctx, ct);
                if (result.Message != null)
                {
                    ctx.FailedRequirements.Add(req.Name);
                }
                pass2Results.Add((req, result));
            }

            // Collect failed requirements from both passes
            var allResults = pass1Results.Concat(pass2Results);
            var failedReqs = new List<(Requirement req, DefaultCommandResponse result)>();

            foreach (var (req, result) in allResults)
            {
                if (result.Message != null)
                {
                    failedReqs.Add((req, result));
                }
            }

            // Determine which requirements to install based on requirementsToInstall
            var requestedSet = (requirementsToInstall ?? [])
                .Select(n => NormalizeRequirementName(n))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            bool installRequested = requestedSet.Count > 0;

            if (installRequested && failedReqs.Count > 0)
            {
                logger.LogInformation("Install requested for: {Requirements}", string.Join(", ", requestedSet));

                // Skip install for requirements whose dependencies failed
                var hasFailedDeps = failedReqs.Where(f => f.req.DependsOn.Any(d => ctx.FailedRequirements.Contains(d))).ToList();
                var canAttempt = failedReqs.Except(hasFailedDeps).ToList();

                // Report dependency-blocked failures
                foreach (var (req, result) in hasFailedDeps)
                {
                    AddFailedResult(response, req, result, ctx);
                }

                // Filter to only requested + installable requirements
                var toInstall = canAttempt.Where(f => f.req.IsAutoInstallable && requestedSet.Contains(f.req.Name)).ToList();
                var notRequested = canAttempt.Where(f => !requestedSet.Contains(f.req.Name)).ToList();
                var requestedButNotInstallable = canAttempt.Where(f => requestedSet.Contains(f.req.Name) && !f.req.IsAutoInstallable).ToList();

                // Log warnings for invalid/non-installable requested names
                var validNames = failedReqs.Select(f => f.req.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var name in requestedSet.Where(n => !validNames.Contains(n)))
                {
                    logger.LogWarning("Ignoring unknown or passing requirement name: {Name}", name);
                }

                // Report non-requested failures
                foreach (var (req, result) in notRequested)
                {
                    AddFailedResult(response, req, result, ctx);
                }

                // Report requested-but-not-installable failures
                foreach (var (req, result) in requestedButNotInstallable)
                {
                    AddFailedResult(response, req, result, ctx);
                }

                // Run installs sequentially
                foreach (var (req, result) in toInstall)
                {
                    logger.LogInformation("Auto-installing requirement: {Requirement}", req.Name);

                    var installResult = await RunInstall(req, ctx, ct);
                    var displayName = GetDisplayName(req);
                    var instructions = req.GetInstructions(ctx).ToList();

                    if (installResult.Success)
                    {
                        // Re-run the check to verify the install succeeded
                        var recheck = await RunCheck(req, ctx, ct);
                        if (recheck.Message == null)
                        {
                            logger.LogInformation("Requirement {Requirement} auto-installed and verified successfully.", req.Name);
                            response.Results.Add(new RequirementCheckResult
                            {
                                Requirement = displayName,
                                Instructions = [],
                                RequirementStatusDetails = $"{req.Name} was auto-installed successfully.",
                                Reason = req.Reason,
                                AutoInstallAttempted = true,
                                IsAutoInstallable = true
                            });
                        }
                        else
                        {
                            logger.LogWarning("Requirement {Requirement} install completed but verification still fails: {Message}", req.Name, recheck.Message);
                            ctx.FailedRequirements.Add(req.Name);
                            response.Results.Add(new RequirementCheckResult
                            {
                                Requirement = displayName,
                                Instructions = instructions,
                                RequirementStatusDetails = $"{req.Name} install completed but verification still fails: {recheck.Message}",
                                Reason = req.Reason,
                                AutoInstallAttempted = true,
                                AutoInstallError = recheck.Message,
                                IsAutoInstallable = true
                            });
                        }
                    }
                    else
                    {
                        logger.LogError("Auto-install failed for {Requirement}: {Error}", req.Name, installResult.Error);
                        ctx.FailedRequirements.Add(req.Name);
                        response.Results.Add(new RequirementCheckResult
                        {
                            Requirement = displayName,
                            Instructions = instructions,
                            RequirementStatusDetails = result.Message ?? $"Requirement {req.Name} check failed.",
                            Reason = req.Reason,
                            AutoInstallAttempted = true,
                            AutoInstallError = installResult.Error ?? installResult.Output,
                            IsAutoInstallable = true
                        });
                    }
                }
            }
            else
            {
                // No install requested: report all failures with installability info
                foreach (var (req, result) in failedReqs)
                {
                    AddFailedResult(response, req, result, ctx);
                }
            }

            // Populate NextSteps for auto-installable requirements (for JSON mode and MCP responses)
            PopulateNextSteps(response);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error verifying setup for {input}", langs);
            return new VerifySetupResponse
            {
                ResponseError = $"Error processing request: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Returns the list of requirement names that can be auto-installed from a check result.
    /// </summary>
    public static List<string> GetInstallableNames(VerifySetupResponse response)
    {
        return response.Results?
            .Where(r => r.IsAutoInstallable && !r.AutoInstallAttempted)
            .Select(r => NormalizeRequirementName(r.Requirement))
            .ToList() ?? [];
    }

    /// <summary>
    /// Strips version suffix from display names (e.g., "Node.js (&gt;= 22.16.0)" → "Node.js")
    /// so that requirement names from the response can match Requirement.Name.
    /// </summary>
    public static string NormalizeRequirementName(string name)
    {
        var parenIndex = name.IndexOf(" (>=");
        return parenIndex >= 0 ? name[..parenIndex].Trim() : name.Trim();
    }

    /// <summary>
    /// Parses a --languages option value into a set of SdkLanguage values.
    /// </summary>
    public static HashSet<SdkLanguage> ParseLanguages(List<string> langs)
    {
        if (langs.Contains("All", StringComparer.OrdinalIgnoreCase))
        {
            return [.. Enum.GetValues<SdkLanguage>().Where(l => l != SdkLanguage.Unknown)];
        }

        return [.. langs.Select(lang => Enum.Parse<SdkLanguage>(lang, ignoreCase: true))];
    }

    private void AddFailedResult(VerifySetupResponse response, Requirement req, DefaultCommandResponse result, RequirementContext ctx)
    {
        ctx.FailedRequirements.Add(req.Name);

        var instructions = req.GetInstructions(ctx).ToList();
        var displayName = GetDisplayName(req);

        response.Results ??= new List<RequirementCheckResult>();
        response.Results.Add(new RequirementCheckResult
        {
            Requirement = displayName,
            Instructions = instructions,
            RequirementStatusDetails = result.Message ?? $"Requirement {req.Name} check failed.",
            Reason = req.Reason,
            IsAutoInstallable = req.IsAutoInstallable,
            NotAutoInstallableReason = req.NotAutoInstallableReason
        });
    }

    private static void PopulateNextSteps(VerifySetupResponse response)
    {
        var autoInstallableNames = response.Results?
            .Where(r => r.IsAutoInstallable && !r.AutoInstallAttempted)
            .Select(r => r.Requirement)
            .ToList() ?? [];

        if (autoInstallableNames.Count > 0)
        {
            response.NextSteps ??= [];
            response.NextSteps.Add($"Run 'azsdk verify setup install' to auto-install: {string.Join(", ", autoInstallableNames)}");
        }
    }

    private static string GetDisplayName(Requirement req)
    {
        return req.MinVersion != null ? $"{req.Name} (>= {req.MinVersion})" : req.Name;
    }

    private async Task<DefaultCommandResponse> RunCheck(Requirement req, RequirementContext ctx, CancellationToken ct)
    {
        // Check if any dependencies have failed
        var failedDeps = req.DependsOn
            .Where(d => ctx.FailedRequirements.Contains(d))
            .ToList();

        if (failedDeps.Count > 0)
        {
            var depList = string.Join(", ", failedDeps);
            logger.LogWarning("Skipping {Requirement}: depends on {Dependencies} which failed or is not installed.",
                req.Name, depList);
            return new DefaultCommandResponse
            {
                Message = $"Skipped {req.Name}: depends on {depList} which failed or is not installed."
            };
        }

        try
        {
            var checkResult = await req.RunCheck(processHelper, ctx, ct);

            if (!checkResult.Success)
            {
                logger.LogError("Requirement {Requirement} check failed",
                    req.Name);
                return new DefaultCommandResponse
                {
                    Message = $"Requirement {req.Name} check failed: {checkResult.Error ?? checkResult.Output}."
                };
            }

            var versionCheckResult = CheckVersion(checkResult.Output ?? string.Empty, req);

            if (!versionCheckResult.Equals(string.Empty))
            {
                logger.LogError("Requirement {Requirement} failed, requires upgrade to version {Version}.",
                    req.Name, versionCheckResult);
                return new DefaultCommandResponse
                {
                    Message = $"Requirement {req.Name} failed, requires upgrade to version {versionCheckResult}"
                };
            }
        }
        catch (Exception ex)
        {
            var instructions = req.GetInstructions(ctx);
            logger.LogError(ex, "Requirement {Requirement} check failed to execute.\n\nInstructions: {Instructions}",
                req.Name, string.Join(", ", instructions));
            return new DefaultCommandResponse
            {
                Message = $"Requirement {req.Name} check failed to execute.\n\tException: {ex.Message}"
            };
        }

        return new DefaultCommandResponse();
    }

    private async Task<RequirementCheckOutput> RunInstall(Requirement req, RequirementContext ctx, CancellationToken ct)
    {
        try
        {
            return await req.RunInstall(processHelper, ctx, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Install for requirement {Requirement} threw an exception.", req.Name);
            return new RequirementCheckOutput
            {
                Success = false,
                Error = $"Install threw an exception: {ex.Message}"
            };
        }
    }

    private async Task<RequirementContext> CreateRequirementContext(string packagePath, HashSet<SdkLanguage>? languages = null, CancellationToken ct = default)
    {
        var repoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, ct);
        var repoName = await gitHelper.GetRepoNameAsync(repoRoot, ct);

        // If no languages specified, try to detect from the repo
        if (languages == null || languages.Count == 0)
        {
            var language = await SdkLanguageHelpers.GetLanguageForRepoPathAsync(gitHelper, packagePath, ct);
            if (language != SdkLanguage.Unknown)
            {
                languages = [language];
            }
            else
            {
                languages = [];
            }
        }

        return RequirementContext.Create(
            repoRoot: repoRoot,
            repoName: repoName,
            packagePath: packagePath,
            languages: languages
        );
    }

    private string CheckVersion(string output, Requirement req)
    {
        // Return empty string if version requirement is satisfied, else return version to upgrade to
        if (req.MinVersion == null)
        {
            // No version constraints
            return string.Empty;
        }

        // Parse the output version
        var outputVersionMatch = System.Text.RegularExpressions.Regex.Match(output, OUTPUT_VERSION_PATTERN);
        if (!outputVersionMatch.Success)
        {
            // Unable to parse the version from the output
            return req.MinVersion;
        }

        string installedVersion = outputVersionMatch.Value.Trim();
        logger.LogDebug("Installed version: {installedVersion}", installedVersion);

        if (!Version.TryParse(installedVersion, out var installedVer))
        {
            logger.LogWarning("Failed to parse installed version as System.Version.");
            return req.MinVersion;
        }

        if (Version.TryParse(req.MinVersion, out var minVer))
        {
            logger.LogDebug("Requires minimum version: {minVersion}", req.MinVersion);
            if (installedVer < minVer)
            {
                return req.MinVersion;
            }
        }

        return string.Empty;
    }
}
