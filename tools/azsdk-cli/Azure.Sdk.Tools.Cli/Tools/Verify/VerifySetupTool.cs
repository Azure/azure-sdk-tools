// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.VerifySetup;
using Azure.Sdk.Tools.Cli.Helpers;
using ModelContextProtocol.Server;
using System.Text.Json;
using System.Net;
using System.Text.Json.Serialization;
using System.IO.Pipelines;

namespace Azure.Sdk.Tools.Cli.Tools.Verify;

/// <summary>
/// This tool verifies that the environment is set up with the required installations to run MCP release tools
/// </summary>
[McpServerToolType, Description("This tool verifies that the environment is set up with the required installations to run MCP release tools.")]
public class VerifySetupTool : MCPTool
{
    private readonly IProcessHelper processHelper;
    private readonly ILogger<VerifySetupTool> logger;

    private readonly ILanguageSpecificResolver<IEnvRequirementsCheck> envRequirementsCheck;

    private readonly string PATH_TO_REQS = Path.Combine(AppContext.BaseDirectory, "Configuration", "RequirementsV1.json");

    public VerifySetupTool(IProcessHelper processHelper, ILogger<VerifySetupTool> logger, ILanguageSpecificResolver<IEnvRequirementsCheck> envRequirementsCheck)
    {
        this.processHelper = processHelper;
        this.logger = logger;
        this.envRequirementsCheck = envRequirementsCheck;
    }

    private static readonly List<string> LANGUAGES = new() { "python", "java", "dotnet", "javascript", "go" };
    private const int COMMAND_TIMEOUT_IN_SECONDS = 30;

    public override CommandGroup[] CommandHierarchy { get; set; } = [
        SharedCommandGroups.Verify,
    ];

    private readonly Option<string> languagesParam = new("--langs", "-l")
    {
        Description = "Comma-separated list of programming languages to check requirements for (java, python, dotnet, javascript, go). Defaults to current repo's language.",
        Required = false
    };


    private readonly Option<bool> allLangOption = new("--all")
    {
        Description = "Check requirements for all supported languages.",
        DefaultValueFactory = _ => false
    };

    protected override Command GetCommand() =>
        new("setup", "Verify environment setup for MCP release tools")
        {
            languagesParam,
            allLangOption,
            SharedOptions.PackagePath
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var langs = parseResult.GetValue(languagesParam);
        var allLangs = parseResult.GetValue(allLangOption);
        var parsed = allLangs ? LANGUAGES : ParseLanguages(langs);
        var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
        return await VerifySetup(parsed, packagePath, ct);
    }

    [McpServerTool(Name = "azsdk_verify_setup"), Description("Verifies the developer environment for MCP release tool requirements")]
    public async Task<VerifySetupResponse> VerifySetup(List<string> langs = null, string packagePath = Environment.CurrentDirectory, CancellationToken ct = default)
    {
        try
        {
            List<SetupRequirements.Requirement> reqsToCheck = await GetRequirements(langs, packagePath, ct);

            VerifySetupResponse response = new VerifySetupResponse
            {
                AllRequirementsSatisfied = true,
                Results = new List<RequirementCheckResult>()
            };

            foreach (var req in reqsToCheck)
            {
                logger.LogInformation("Checking requirement: {Requirement}, Check: {Check}, Instructions: {Instructions}",
                    req.requirement, req.check, req.instructions);

                var result = await RunCheck(req, packagePath, ct);

                if (result.ExitCode != 0)
                {
                    logger.LogWarning("Requirement check failed for {Requirement}. Suggested install command: {Instruction}", req.requirement, req.instructions);
                    response.AllRequirementsSatisfied = false;
                    response.Results.Add(new RequirementCheckResult
                    {
                        Requirement = req.requirement,
                        Instructions = req.instructions,
                        Output = result.ResponseError,
                    });
                }
            }
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError("Error verifying setup for {input}: {ex}", langs, ex);
            return new ()
            {
                ResponseError = $"Error processing request: {ex.Message}"
            };
        }
    }

    private async Task<DefaultCommandResponse> RunCheck(SetupRequirements.Requirement req, string packagePath, CancellationToken ct)
    {
        var command = req.check;
        var options = new ProcessOptions(
            command[0],
            args: command.Skip(1).ToArray(),
            timeout: TimeSpan.FromSeconds(COMMAND_TIMEOUT_IN_SECONDS),
            logOutputStream: true,
            workingDirectory: packagePath
        );

        var trimmed = string.Empty;
        try
        {
            logger.LogInformation("Running command: {Command} in {packagePath}", string.Join(' ', command), packagePath);
            var result = await processHelper.Run(options, ct);
            trimmed = (result.Output ?? string.Empty).Trim();

            if (result.ExitCode != 0)
            {
                logger.LogError("Command {Command} failed with exit code {ExitCode}. Output: {Output}", string.Join(' ', command), result.ExitCode, trimmed);
                return new DefaultCommandResponse
                {
                    ResponseError = $"Command {string.Join(' ', command)} failed with exit code {result.ExitCode}. Output: {trimmed}."
                };
            }

            var versionCheckResult = CheckVersion(trimmed, req);

            if (!versionCheckResult.Equals(string.Empty))
            {
                logger.LogError("Command {Command} failed version check.", string.Join(' ', command));
                return new DefaultCommandResponse
                {
                    ResponseError = $"Command {string.Join(' ', command)} failed, requires upgrade to version {versionCheckResult}"
                };
            }
        }
        catch
        {
            logger.LogError("Command {Command} failed to execute.", string.Join(' ', command));
            return new DefaultCommandResponse
            {
                ResponseError = $"Command {string.Join(' ', command)} failed to execute."
            };
        }

        logger.LogInformation("Command {Command} succeeded. Output: {Output}", string.Join(' ', command), trimmed);

        return new DefaultCommandResponse
        {
            Message = $"Command {string.Join(' ', command)} succeeded. Output: {trimmed}"
        };
    }

    private async Task<List<SetupRequirements.Requirement>> GetRequirements(List<string> languages, string packagePath, CancellationToken ct)
    {
        // Check core requirements before language-specific requirements
        var reqsToCheck = await GetCoreRequirements(ct);

        // Per-language requirements
        var reqGetter = null as IEnvRequirementsCheck;
        if (languages == null || languages.Count == 0)
        {
            // Detect language if none given
            reqGetter = await envRequirementsCheck.Resolve(packagePath);

            if (reqGetter == null)
            {
                logger.LogWarning("Could not resolve requirements checker for the specified languages. Checking only core requirements. Please provide languages using --langs option to check language requirements.");
                return reqsToCheck;
            }

            reqsToCheck.AddRange(await reqGetter.GetRequirements(packagePath, ct));
            return reqsToCheck;
        }

        var reqGetters = envRequirementsCheck.Resolve(languages);

        if (reqGetters == null)
        {
            throw new Exception("Could not resolve requirements checker for the specified languages.");
        }

        foreach (var getter in reqGetters)
        {
            if (getter == null)
            {
                logger.LogError("Could not resolve requirements checker for one of the specified languages.");
                continue;
            }
            var langReqs = await getter.GetRequirements(packagePath, ct);
            if (langReqs != null)
            {
                reqsToCheck.AddRange(langReqs);
            }
        }

        return reqsToCheck ?? new List<SetupRequirements.Requirement>();
    }

    private async Task<List<SetupRequirements.Requirement>> GetCoreRequirements(CancellationToken ct)
    {
        var requirementsJson = await File.ReadAllTextAsync(PATH_TO_REQS, ct);
        var setupRequirements = JsonSerializer.Deserialize<SetupRequirements>(requirementsJson);

        if (setupRequirements == null)
        {
            throw new Exception("Failed to parse requirements JSON.");
        }

        var reqs = new List<SetupRequirements.Requirement>();
        foreach (var kv in setupRequirements.categories)
        {
            var category = kv.Key;
            var requirements = kv.Value;
            if (string.Equals(category, "core", StringComparison.OrdinalIgnoreCase))
            {
                if (requirements != null)
                {
                    reqs.AddRange(requirements);
                }
            }
        }

        return reqs;
    }

    private string CheckVersion(string output, SetupRequirements.Requirement req)
    {
        // Return empty string if version requirement is satisfied, else return version to upgrade to
        string versionPattern = @"(>=|<=|>|<|=)\s*([\d\.]+)";
        
        var match = System.Text.RegularExpressions.Regex.Match(req.requirement, versionPattern);
        if (!match.Success)
        {
            // No version specified in the requirement
            return String.Empty;
        }

        string operatorSymbol = match.Groups[1].Value;
        string requiredVersion = match.Groups[2].Value;

        logger.LogInformation($"Requires version: {requiredVersion}");

        // Parse the output version
        var outputVersionMatch = System.Text.RegularExpressions.Regex.Match(output, @"[\d\.]+");
        if (!outputVersionMatch.Success)
        {
            // Unable to parse the version from the output
            return requiredVersion;
        }

        string installedVersion = outputVersionMatch.Value;

        logger.LogInformation($"Installed version: {installedVersion}");

        int comparison = CompareVersions(installedVersion, requiredVersion);

        return operatorSymbol switch
        {
            ">=" => comparison >= 0 ? string.Empty : requiredVersion,
            "<=" => comparison <= 0 ? string.Empty : requiredVersion,
            ">" => comparison > 0 ? string.Empty : requiredVersion,
            "<" => comparison < 0 ? string.Empty : requiredVersion,
            "=" => comparison == 0 ? string.Empty : requiredVersion,
            _ => requiredVersion,
        };
    }

    private int CompareVersions(string version1, string version2)
    {
        var v1Parts = version1.Split('.');
        var v2Parts = version2.Split('.');

        int length = Math.Max(v1Parts.Length, v2Parts.Length);
        for (int i = 0; i < length; i++)
        {
            int v1Part = i < v1Parts.Length ? int.Parse(v1Parts[i]) : 0;
            int v2Part = i < v2Parts.Length ? int.Parse(v2Parts[i]) : 0;

            int comparison = v1Part.CompareTo(v2Part);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    private List<string> ParseLanguages(string? langs)
    {
        if (string.IsNullOrWhiteSpace(langs))
        {
            return new List<string> ();
        }

        // Validate languages
        List<string> parsed = new List<string>(langs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        List<string> parsedResult = new List<string>();
        foreach (var lang in parsed)
        {
            if (!LANGUAGES.Contains(lang.ToLower().Trim()))
            {
                logger.LogError("Unsupported language: {lang}. Supported languages are: {supportedLanguages}.", lang, string.Join(", ", LANGUAGES));
                continue;
            }
            parsedResult.Add(lang);
        }

        return parsedResult;
    }
}
