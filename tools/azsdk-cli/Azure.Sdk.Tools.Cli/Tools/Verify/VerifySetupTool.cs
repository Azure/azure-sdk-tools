// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Helpers;
using ModelContextProtocol.Server;
using System.Text.Json;
using System.Net;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Tools.Verify;

[McpServerToolType, Description("This tool verifies that the environment is set up with the required installations to run MCP release tools.")]
public class VerifySetupTool : MCPTool
{
    private readonly IProcessHelper processHelper;
    private readonly ILogger<VerifySetupTool> logger;

    public VerifySetupTool(IProcessHelper processHelper, ILogger<VerifySetupTool> logger)
    {
        this.processHelper = processHelper;
        this.logger = logger;
    }
    // for V1 prototype only TODO
    private string PATH_TO_REQS = Path.Combine(AppContext.BaseDirectory, "Configuration", "RequirementsV1.json");
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
            List<SetupRequirements.Requirement> reqsToCheck = GetRequirements(langs);

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
    private List<SetupRequirements.Requirement> GetRequirements(List<string> languages)
    {
        String requirementsJson = File.ReadAllText(PATH_TO_REQS);
        var setupRequirements = JsonSerializer.Deserialize<SetupRequirements>(requirementsJson);

        if (setupRequirements == null)
        {
            throw new Exception("Failed to parse requirements JSON.");
        }

        List<SetupRequirements.Requirement> reqsToCheck = new List<SetupRequirements.Requirement>();
        foreach (var category in setupRequirements.categories.Keys)
        {
            if (category.Equals("core") || languages.Contains(category))
            {
                setupRequirements.categories.TryGetValue(category, out var requirements);
                if (requirements != null) {
                    reqsToCheck.AddRange(requirements);
                }
            }
        }

        return reqsToCheck;
    }

    private List<string> ParseLanguages(string? langs)
    {
        if (string.IsNullOrWhiteSpace(langs))
        {
            // TODO determine language from current repo if no arg given
            return new List<string> { "python" };
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

    // for V1 prototype only
    private class SetupRequirements
    {
        [JsonPropertyName("categories")]
        public Dictionary<string, List<Requirement>> categories { get; set; }

        public class Requirement
        {
            [JsonPropertyName("requirement")]
            public string requirement { get; set; }

            [JsonPropertyName("check")]
            public string[] check { get; set; }
            [JsonPropertyName("instructions")]
            public List<string> instructions { get; set; }
        }
    }
}
