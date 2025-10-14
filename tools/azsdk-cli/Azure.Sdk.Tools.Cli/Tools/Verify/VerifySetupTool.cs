// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
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

namespace Azure.Sdk.Tools.Cli.Tools.Verify;

/// <summary>
/// This tool verifies that the environment is set up with the required installations to run MCP release tools
/// </summary>
[McpServerToolType, Description("This tool verifies that the environment is set up with the required installations to run MCP release tools.")]
public class VerifySetupTool : MCPTool
{
    private readonly IProcessHelper processHelper;
    private readonly ILogger<VerifySetupTool> logger;

    private readonly ILanguageSpecificResolverVerifySetup<IEnvRequirementsCheck> envRequirementsCheck;

    public VerifySetupTool(IProcessHelper processHelper, ILogger<VerifySetupTool> logger, ILanguageSpecificResolverVerifySetup<IEnvRequirementsCheck> envRequirementsCheck)
    {
        this.processHelper = processHelper;
        this.logger = logger;
        this.envRequirementsCheck = envRequirementsCheck;
    }
    // for V1 prototype only TODO
    // private string PATH_TO_REQS = Path.Combine(AppContext.BaseDirectory, "Configuration", "RequirementsV1.json");
    private static readonly List<string> LANGUAGES = new() { "python", "java", "dotnet", "javascript", "go" };
    private const int COMMAND_TIMEOUT_IN_SECONDS = 30;

    public override CommandGroup[] CommandHierarchy { get; set; } = [
        SharedCommandGroups.Verify,
    ];

    private readonly Option<string> languagesParam = new(["--langs", "-l"], "Comma-separated list of programming languages to check requirements for (java, python, dotnet, javascript, go). Defaults to current repo's language.") { IsRequired = false };
    private readonly Option<bool> allLangOption = new(["--all"], () => false, "Check requirements for all supported languages.");

    protected override Command GetCommand() =>
        new("setup", "Verify environment setup for MCP release tools")
        {
            languagesParam,
            allLangOption
        };

    public override async Task<CommandResponse> HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        var langs = ctx.ParseResult.GetValueForOption(languagesParam);
        var allLangs = ctx.ParseResult.GetValueForOption(allLangOption);
        var parsed = allLangs ? LANGUAGES : ParseLanguages(langs);
        return await VerifySetup(parsed, ct);
    }

    [McpServerTool(Name = "azsdk_verify_setup"), Description("Verifies the developer environment for MCP release tool requirements")]
    public async Task<VerifySetupResponse> VerifySetup(List<string> langs, CancellationToken ct = default)
    {
        try
        {
            List<SetupRequirements.Requirement> reqsToCheck = await GetRequirements(langs, ct);

            VerifySetupResponse response = new VerifySetupResponse
            {
                AllRequirementsSatisfied = true,
                Results = new List<RequirementCheckResult>()
            };

            foreach (var req in reqsToCheck)
            {
                logger.LogInformation("Checking requirement: {Requirement}, Check: {Check}, Instructions: {Instructions}",
                    req.requirement, req.check, req.instructions);

                var result = await RunCheck(req.check, ct);

                if (result.ExitCode != 0)
                {
                    logger.LogWarning("Requirement check failed for {Requirement}. Suggested install command: {Instruction}", req.requirement, req.instructions);
                    response.AllRequirementsSatisfied = false;
                    response.Results.Add(new RequirementCheckResult
                    {
                        Requirement = req.requirement,
                        Instructions = req.instructions
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

    private async Task<DefaultCommandResponse> RunCheck(string[] command, CancellationToken ct)
    {
        var options = new ProcessOptions(
            command[0],
            args: command.Skip(1).ToArray(),
            timeout: TimeSpan.FromSeconds(COMMAND_TIMEOUT_IN_SECONDS),
            logOutputStream: true
        );

        var trimmed = string.Empty;
        try
        {
            logger.LogInformation("Running command: {Command}", string.Join(' ', command));
            var result = await processHelper.Run(options, ct);
            trimmed = (result.Output ?? string.Empty).Trim();

            if (result.ExitCode != 0)
            {
                logger.LogError("Command {Command} failed with exit code {ExitCode}. Output: {Output}", command, result.ExitCode, trimmed);
                return new DefaultCommandResponse
                {
                    ResponseError = $"Command {command} failed with exit code {result.ExitCode}. Output: {trimmed}"
                };
            }
        }
        catch
        {
            logger.LogError("Command {Command} failed to execute.", command);
            return new DefaultCommandResponse
            {
                ResponseError = $"Command {command} failed to execute."
            };
        }

        logger.LogInformation("Command {Command} succeeded. Output: {Output}", command, trimmed);

        return new DefaultCommandResponse
        {
            Message = $"Command {command} succeeded. Output: {trimmed}"
        };
    }

    // for V1 prototype only
    private async Task<List<SetupRequirements.Requirement>> GetRequirements(List<string> languages, CancellationToken ct)
    {
        var reqGetter = null as IEnvRequirementsCheck;
        if (languages.Count == 0)
        {
            // detect language if none given
            reqGetter = await envRequirementsCheck.Resolve("."); // TODO add package dir as option?

            if (reqGetter == null)
            {
                throw new Exception("Could not resolve requirements checker for the specified languages.");
            }

            return await reqGetter.GetRequirements(ct);
        }

        List<SetupRequirements.Requirement> reqsToCheck = new List<SetupRequirements.Requirement>();

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
            var langReqs = await getter.GetRequirements(ct);
            if (langReqs != null)
            {
                reqsToCheck.AddRange(langReqs);
            }
        }

        return reqsToCheck ?? new List<SetupRequirements.Requirement>();
    }

    private List<string> ParseLanguages(string? langs)
    {
        if (string.IsNullOrWhiteSpace(langs))
        {
            return new List<string> ();
        }

        // validate and sanitize languages
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
