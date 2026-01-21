// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
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

    private const int COMMAND_TIMEOUT_IN_SECONDS = 30;
    private const string OUTPUT_VERSION_PATTERN = @"[\d\.]+";
    private const string VerifySetupToolName = "azsdk_verify_setup";

    public override CommandGroup[] CommandHierarchy { get; set; } = [
        SharedCommandGroups.Verify,
    ];

    private readonly Option<List<SdkLanguage>> languagesParam = new("--languages", "-l")
    {
        Description = $"List of space-separated SDK languages to check requirements for ({string.Join(" ", Enum.GetNames(typeof(SdkLanguage)))}). Defaults to current repo's language.",
        Required = false,
        AllowMultipleArgumentsPerToken = true
    };


    private readonly Option<bool> allLangOption = new("--all")
    {
        Description = "Check requirements for all supported languages.",
        DefaultValueFactory = _ => false
    };

    protected override Command GetCommand() =>
        new McpCommand("setup", "Verify environment setup for MCP release tools", VerifySetupToolName)
        {
            languagesParam,
            allLangOption,
            SharedOptions.PackagePath
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var langs = parseResult.GetValue(languagesParam);
        var allLangs = parseResult.GetValue(allLangOption);
        var parsed = allLangs ? Enum.GetValues<SdkLanguage>().ToHashSet() : langs.ToHashSet();
        var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
        return await VerifySetup(parsed, packagePath, ct);
    }

    [McpServerTool(Name = VerifySetupToolName), Description("Verifies the developer environment for MCP release tool requirements. Accepts a list of supported languages to check requirements for, and the packagePath of the repo to check.")]
    public async Task<VerifySetupResponse> VerifySetup(HashSet<SdkLanguage> langs = null, string packagePath = null, CancellationToken ct = default)
    {
        try
        {
            // Create context for filtering requirements
            var ctx = CreateRequirementContext(packagePath ?? Environment.CurrentDirectory, langs);
            
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
                
                if (result.ExitCode != 0)
                {
                    response.ResponseErrors ??= new List<string>();
                    response.ResponseErrors.Add($"Requirement failed: {req.Name}. Error: {result.ResponseError}");

                    response.Results.Add(new RequirementCheckResult
                    {
                        Requirement = req.Name,
                        Instructions = instructions,
                    });
                } 
                else if (result.Message != null)
                {
                    response.Results.Add(new RequirementCheckResult
                    {
                        Requirement = req.Name,
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
            return new ()
            {
                ResponseError = $"Error processing request: {ex.Message}"
            };
        }
    }

    private async Task<DefaultCommandResponse> RunCheck(Requirement req, RequirementContext ctx, CancellationToken ct)
    {
        var command = req.CheckCommand;
        var options = new ProcessOptions(
            command[0],
            args: command.Skip(1).ToArray(),
            timeout: TimeSpan.FromSeconds(COMMAND_TIMEOUT_IN_SECONDS),
            logOutputStream: false,
            workingDirectory: ctx.PackagePath
        );

        var trimmed = string.Empty;
        try
        {
            var result = await processHelper.Run(options, ct);
            trimmed = (result.Output ?? string.Empty).Trim();

            if (result.ExitCode != 0)
            {
                var instructions = req.GetInstructions(ctx);
                logger.LogError("Command {Command} failed with exit code {ExitCode}.\n\nInstructions: {Instructions}", string.Join(' ', command), result.ExitCode, string.Join(", ", instructions));
                return new DefaultCommandResponse
                {
                    Message = $"Command {string.Join(' ', command)} failed with exit code {result.ExitCode}: {trimmed}."
                };
            }

            var versionCheckResult = CheckVersion(trimmed, req);

            if (!versionCheckResult.Equals(string.Empty))
            {
                logger.LogError("Command {Command} failed, requires upgrade to version {Version}.", string.Join(' ', command), versionCheckResult);
                return new DefaultCommandResponse
                {
                    Message = $"Command {string.Join(' ', command)} failed, requires upgrade to version {versionCheckResult}"
                };
            }
        }
        catch (Exception ex)
        {
            var instructions = req.GetInstructions(ctx);
            logger.LogError(ex, "Command {Command} failed to execute.\n\nInstructions: {Instructions}", string.Join(' ', command), string.Join(", ", instructions));
            return new DefaultCommandResponse
            {
                Message = $"Command {string.Join(' ', command)} failed to execute.\n\tException: {ex.Message}"
            };
        }

        return new DefaultCommandResponse();
    }

    private RequirementContext CreateRequirementContext(string packagePath, HashSet<SdkLanguage>? languages = null)
    {
        var (repoRoot, _, _) = PackagePathParser.Parse(gitHelper, packagePath);
        
        // If no languages specified, try to detect from the repo
        if (languages == null || languages.Count == 0)
        {
            var languageService = GetLanguageService(packagePath);
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
            packagePath: packagePath,
            languages: languages
        );
    }

    private string CheckVersion(string output, Requirement req)
    {
        // Return empty string if version requirement is satisfied, else return version to upgrade to
        if (req.MinVersion == null && req.MaxVersion == null)
        {
            // No version constraints
            return string.Empty;
        }

        // Parse the output version
        var outputVersionMatch = System.Text.RegularExpressions.Regex.Match(output, OUTPUT_VERSION_PATTERN);
        if (!outputVersionMatch.Success)
        {
            // Unable to parse the version from the output
            return req.MinVersion ?? req.MaxVersion ?? string.Empty;
        }

        string installedVersion = outputVersionMatch.Value.Trim();
        logger.LogDebug("Installed version: {installedVersion}", installedVersion);

        if (!Version.TryParse(installedVersion, out var installedVer))
        {
            logger.LogWarning("Failed to parse installed version as System.Version.");
            return req.MinVersion ?? req.MaxVersion ?? string.Empty;
        }

        // Check minimum version
        if (req.MinVersion != null)
        {
            if (Version.TryParse(req.MinVersion, out var minVer))
            {
                logger.LogDebug("Requires minimum version: {minVersion}", req.MinVersion);
                if (installedVer < minVer)
                {
                    return req.MinVersion;
                }
            }
        }

        // Check maximum version
        if (req.MaxVersion != null)
        {
            if (Version.TryParse(req.MaxVersion, out var maxVer))
            {
                logger.LogDebug("Requires maximum version: {maxVersion}", req.MaxVersion);
                if (installedVer > maxVer)
                {
                    return req.MaxVersion;
                }
            }
        }

        return string.Empty;
    }
}
