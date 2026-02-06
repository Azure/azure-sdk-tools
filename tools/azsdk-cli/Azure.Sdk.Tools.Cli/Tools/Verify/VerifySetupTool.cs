// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.SetupRequirements;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Helpers;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Tools.Core;
using Azure.Sdk.Tools.Cli.Services.Languages;

namespace Azure.Sdk.Tools.Cli.Tools.Verify;

/// <summary>
/// This tool verifies that the environment is set up with the required installations to run MCP release tools
/// </summary>
[McpServerToolType, Description("This tool verifies that the environment is set up with the required installations to run MCP release tools.")]
public class VerifySetupTool : LanguageMcpTool
{
    private readonly IProcessHelper processHelper;

    public VerifySetupTool(IProcessHelper processHelper, ILogger<VerifySetupTool> logger, IGitHelper gitHelper, IEnumerable<LanguageService> languageServices) : base(languageServices, gitHelper, logger)
    {
        this.processHelper = processHelper;
    }

    private const string OUTPUT_VERSION_PATTERN = @"[\d\.]+";
    private const string VerifySetupToolName = "azsdk_verify_setup";

    public override CommandGroup[] CommandHierarchy { get; set; } = [
        SharedCommandGroups.Verify,
    ];

    private static readonly HashSet<string> supportedSdkLanguages = [ "All", .. Enum.GetNames<SdkLanguage>()
            .Where(n => n != nameof(SdkLanguage.Unknown))];

    private readonly Option<List<string>> languagesParam = new("--languages", "-l")
    {
        Description = $"List of space-separated SDK languages ({string.Join(" ", supportedSdkLanguages.OrderBy(n => n))}) to check requirements for. Defaults to current repo's language.",
        Validators =
        {
            result =>
            {
                var badLanguages = (result.GetValueOrDefault<List<string>>() ?? [])
                    .Except(supportedSdkLanguages, StringComparer.OrdinalIgnoreCase);

                foreach (var value in badLanguages)
                {
                    result.AddError($"Invalid language '{value}'");
                }
            }
        },
        Required = false,
        AllowMultipleArgumentsPerToken = true
    };

    private readonly Option<bool> autoInstallParam = new("--auto-install", "-i")
    {
        Description = "Automatically install requirements that support auto-installation.",
        Required = false,
    };

    protected override Command GetCommand() =>
        new McpCommand("setup", "Verify environment setup for MCP release tools", VerifySetupToolName)
        {
            languagesParam,
            autoInstallParam,
            SharedOptions.PackagePath
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var parsed = GetLanguagesFromOption(parseResult);
        var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
        var autoInstall = parseResult.GetValue(autoInstallParam);
        return await VerifySetup(parsed, packagePath, autoInstall, ct);
    }

    [McpServerTool(Name = VerifySetupToolName), Description("Verifies the developer environment for MCP release tool requirements. Accepts a list of supported languages to check requirements for, and the packagePath of the repo to check. Set autoInstall to true to automatically install requirements that support auto-installation.")]
    public async Task<VerifySetupResponse> VerifySetup(HashSet<SdkLanguage> langs = null, string packagePath = null, bool autoInstall = false, CancellationToken ct = default)
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

            // Start all checks concurrently
            var checkTasks = new List<Task<(Requirement req, DefaultCommandResponse result)>>();
            
            foreach (var req in reqsToCheck)
            {
                logger.LogInformation("Checking requirement: {Requirement}", req.Name);

                var task = RunCheck(req, ctx, ct).ContinueWith(t => (req, t.Result), TaskScheduler.Default);
                checkTasks.Add(task);
            }

            var results = await Task.WhenAll(checkTasks);

            // Collect failed requirements
            var failedReqs = new List<(Requirement req, DefaultCommandResponse result)>();

            foreach (var (req, result) in results)
            {
                if (result.Message != null)
                {
                    failedReqs.Add((req, result));
                }
            }

            // If auto-install is enabled, attempt to install failed requirements that support it
            if (autoInstall && failedReqs.Count > 0)
            {
                logger.LogInformation("Auto-install is enabled. Attempting to auto-install requirements that support it.");
                var installable = failedReqs.Where(f => f.req.IsAutoInstallable).ToList();
                var notInstallable = failedReqs.Where(f => !f.req.IsAutoInstallable).ToList();

                // Report non-installable failures
                foreach (var (req, result) in notInstallable)
                {
                    AddFailedResult(response, req, result, ctx);
                }

                // Run installs sequentially (order may matter for dependencies)
                foreach (var (req, result) in installable)
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
                                Instructions = instructions,
                                RequirementStatusDetails = $"{req.Name} was auto-installed successfully.",
                                Reason = req.Reason,
                                AutoInstallAttempted = true,
                                IsAutoInstallable = true
                            });
                        }
                        else
                        {
                            logger.LogWarning("Requirement {Requirement} install completed but verification still fails: {Message}", req.Name, recheck.Message);
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
                // No auto-install: report all failures with installability info
                foreach (var (req, result) in failedReqs)
                {
                    AddFailedResult(response, req, result, ctx);
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error verifying setup for {input}", langs);
            return new()
            {
                ResponseError = $"Error processing request: {ex.Message}"
            };
        }
    }

    private void AddFailedResult(VerifySetupResponse response, Requirement req, DefaultCommandResponse result, RequirementContext ctx)
    {
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

    private static string GetDisplayName(Requirement req)
    {
        return req.MinVersion != null ? $"{req.Name} (>= {req.MinVersion})" : req.Name;
    }

    private async Task<DefaultCommandResponse> RunCheck(Requirement req, RequirementContext ctx, CancellationToken ct)
    {
        try
        {
            var checkResult = await req.RunCheckAsync(processHelper, ctx, ct);

            if (!checkResult.Success)
            {
                var instructions = req.GetInstructions(ctx);
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
            return await req.RunInstallAsync(processHelper, ctx, ct);
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
        var (repoRoot, _, _) = await PackagePathParser.ParseAsync(gitHelper, packagePath, ct);
        var repoName = await gitHelper.GetRepoNameAsync(repoRoot, ct);

        // If no languages specified, try to detect from the repo
        if (languages == null || languages.Count == 0)
        {
            var languageService = await GetLanguageServiceAsync(packagePath, ct);
            if (languageService != null)
            {
                languages = [languageService.Language];
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

    private HashSet<SdkLanguage> GetLanguagesFromOption(ParseResult parseResult)
    {
        var langs = parseResult.GetValue(languagesParam) ?? [];

        if (langs.Contains("All", StringComparer.OrdinalIgnoreCase))
        {
            return [.. Enum.GetValues<SdkLanguage>().Where(l => l != SdkLanguage.Unknown)];
        }

        return [.. langs.Select(lang => Enum.Parse<SdkLanguage>(lang, ignoreCase: true))];
    }
}
