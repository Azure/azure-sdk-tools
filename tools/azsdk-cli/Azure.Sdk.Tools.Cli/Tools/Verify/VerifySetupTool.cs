// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Utils;
using ModelContextProtocol.Server;
using System.Text.Json;
using System.Net;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Tools.Verify;

[McpServerToolType, Description("This tool verifies that the environment is set up with the required installations to run MCP release tools.")]
public class VerifySetupTool(
    IProcessHelper processHelper,
    ILogger<VerifySetupTool> logger
) : MCPTool
{
    // for V1 prototype only TODO
    private string PATH_TO_REQS = Path.Combine(AppContext.BaseDirectory, "Configuration", "RequirementsV1.json");
    private static readonly List<string> LANGUAGES = new() { "python", "java", "dotnet", "javascript", "go" };
    private const int COMMAND_TIMEOUT_IN_SECONDS = 30;

    public override CommandGroup[] CommandHierarchy { get; set; } = [
        SharedCommandGroups.YourGroup,
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
        var parsed = ParseLanguages(langs);
        return await VerifySetup(parsed, allLangs, ct);
    }

    [McpServerTool(Name = "azsdk_verify_setup"), Description("Verifies the developer environment for MCP release tool requirements")]
    public async Task<DefaultCommandResponse> VerifySetup(string[]? langs = null, bool allLangs = false, CancellationToken ct = default)
    {
        try
        {
            List<SetupRequirements.Requirement> reqsToCheck = GetRequirements(allLangs ? LANGUAGES : ParseLanguages(langs));
            VerifySetupResponse response = new VerifySetupResponse
            {
                AllRequirementsSatisfied = true,
                Results = new List<RequirementCheckResult>()
            };

            foreach (var req in reqsToCheck)
            {
                logger.LogInformation("Checking requirement: {Requirement}, Version: {Version}, Check: {Check}, Install: {Install}",
                    req.requirement, req.version, req.check, req.install);

                var result = await RunCheck(req.check, ct);

                if (result.ExitCode != 0)
                {
                    logger.LogWarning("Requirement check failed for {Requirement}. Suggested install command: {Install}", req.requirement, req.install);
                    response.AllRequirementsSatisfied = false;
                    response.Results.Add(new RequirementCheckResult
                    {
                        Requirement = req.requirement,
                        Version = req.version,
                        Instructions = req.install
                    });
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error verifying setup: {input}", langs);
            return new DefaultCommandResponse
            {
                ResponseError = $"Error processing request: {ex.Message}"
            };
        }
    }

    private async Task<DefaultCommandResponse> RunCheck(string command, CancellationToken ct)
    {
        var options = new ProcessOptions(command, args)
        {
            Command = command,
            WorkingDirectory = Environment.CurrentDirectory,
            Timeout = TimeSpan.FromSeconds(COMMAND_TIMEOUT_IN_SECONDS),
            LogOutputStream = true,
        };

        var result = await processHelper.Run(options, ct); 
        var trimmed = (result.Output ?? string.Empty).Trim();

        if (result.ExitCode != 0)
        {
            logger.LogError("Command {Command} failed with exit code {ExitCode}. Output: {Output}", command, result.ExitCode, trimmed);
            return new DefaultCommandResponse
            {
                ResponseError = $"Command {command} failed with exit code {result.ExitCode}. Output: {trimmed}"
            };
        }
        
        logger.LogInformation("Command {Command} succeeded. Output: {Output}", command, trimmed);
        
        return new DefaultCommandResponse
        {
            ResponseMessage = $"Command {command} succeeded. Output: {trimmed}"
        };
    }

    private static SetupRequirements.Requirement<string> GetRequirements(List<string> languages)
    {
        // Returns requirements to check
        String requirementsJson = File.ReadAllText(PATH_TO_REQS);
        var setupRequirements = JsonSerializer.Deserialize<List<SetupRequirements>>(requirementsJson);
        List<SetupRequirements.Requirement> reqsToCheck = new List<>();
        foreach (var lang in languages)
        {
            var reqs = setupRequirements?.FirstOrDefault(r => r.language.Equals(lang, StringComparison.OrdinalIgnoreCase));
            if (reqs != null)
            {
                reqsToCheck.AddRange(reqs.requirements.Select(r => r.requirement));
            }
        }

        return reqsToCheck;
    }

    private static List<string> ParseLanguages(string? langs)
    {
        if (string.IsNullOrWhiteSpace(langs))
        {
            // TODO determine language from current repo if no arg given
            return new List<string>().Add("python");
        }
        // TODO validate and sanitize languages
        return langs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public class VerifySetupResponse : Response
    {
        [JsonPropertyName("allRequirementsSatisfied")]
        public bool? AllRequirementsSatisfied { get; set; }

        [JsonPropertyName("results")]
        public List<RequirementCheckResult>? Results { get; set; } // all checks with details

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"AllRequirementsSatisfied: {AllRequirementsSatisfied}");
            sb.AppendLine("Results:");

            if (Results != null)
            {
                foreach (var result in Results)
                {
                    sb.AppendLine($"  - Requirement: {result.Requirement}");
                    sb.AppendLine($"    Version: {result.Version}");
                    sb.AppendLine($"    Instructions: {result.Instructions}");
                }
            }
            else
            {
                sb.AppendLine("  None");
            }
            return sb.ToString();
        }
    }
    
    public class RequirementCheckResult
    {
        public string Requirement { get; set; }
        public string Version { get; set; }
        public string Instructions { get; set; }
    }

    // for V1 prototype only
    private class SetupRequirements
    {
        private string language { get; set; }
        private List<Requirement> requirements { get; set; }

        public class Requirement
        {
            public string requirement { get; set; }
            public string version { get; set; }
            public string check { get; set; }
            public string instructions { get; set; }
        }
    }
}
