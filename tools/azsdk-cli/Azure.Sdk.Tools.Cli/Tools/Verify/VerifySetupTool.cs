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
    private readonly IPackageInfoHelper packageInfoHelper;

    public VerifySetupTool(
        IProcessHelper processHelper,
        ILogger<VerifySetupTool> logger,
        IGitHelper gitHelper,
        IPackageInfoHelper packageInfoHelper,
        IEnumerable<LanguageService> languageServices) : base(languageServices, gitHelper, logger)
    {
        this.processHelper = processHelper;
        this.packageInfoHelper = packageInfoHelper;
    }

    private const int COMMAND_TIMEOUT_IN_SECONDS = 30;
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

    protected override Command GetCommand() =>
        new McpCommand("setup", "Verify environment setup for MCP release tools", VerifySetupToolName)
        {
            languagesParam,
            SharedOptions.PackagePath
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var parsed = GetLanguagesFromOption(parseResult);
        var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
        return await VerifySetup(parsed, packagePath, ct);
    }

    [McpServerTool(Name = VerifySetupToolName), Description("Verifies the developer environment for MCP release tool requirements. Accepts a list of supported languages to check requirements for, and the packagePath of the repo to check.")]
    public async Task<VerifySetupResponse> VerifySetup(HashSet<SdkLanguage> langs = null, string packagePath = null, CancellationToken ct = default)
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

            foreach (var (req, result) in results)
            {
                var instructions = req.GetInstructions(ctx).ToList();

                var displayName = req.Name;

                // Display version constraints if applicable
                if (req.MinVersion != null)
                {
                    displayName = $"{req.Name} (>= {req.MinVersion})";
                }

                if (result.Message != null)
                {
                    response.Results.Add(new RequirementCheckResult
                    {
                        Requirement = displayName,
                        Instructions = instructions,
                        RequirementStatusDetails = result.Message,
                        Reason = req.Reason
                    });
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

    private async Task<DefaultCommandResponse> RunCheck(Requirement req, RequirementContext ctx, CancellationToken ct)
    {
        // Create a delegate that captures execution details
        async Task<ProcessResult> runCommand(string[] command)
        {
            var options = new ProcessOptions(
                command[0],
                args: command.Skip(1).ToArray(),
                timeout: TimeSpan.FromSeconds(COMMAND_TIMEOUT_IN_SECONDS),
                logOutputStream: false,
                workingDirectory: ctx.PackagePath
            );
            return await processHelper.Run(options, ct);
        }

        try
        {
            var checkResult = await req.RunCheckAsync(runCommand, ctx, ct);

            if (!checkResult.Success)
            {
                var instructions = req.GetInstructions(ctx);
                logger.LogError("Requirement {Requirement} check failed.\n\nInstructions: {Instructions}",
                    req.Name, string.Join(", ", instructions));
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

    private async Task<RequirementContext> CreateRequirementContext(string packagePath, HashSet<SdkLanguage>? languages = null, CancellationToken ct = default)
    {
        var (repoRoot, _, _) = await packageInfoHelper.ParsePackagePathAsync(packagePath, ct);
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
