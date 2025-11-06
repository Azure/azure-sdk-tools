// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Helpers;
using ModelContextProtocol.Server;
using System.Text.Json;
using System.Reflection;
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
    private const string REQ_VERSION_PATTERN = @"(>=|<=|>|<|=)\s*([\d\.]+)";
    private const string OUTPUT_VERSION_PATTERN = @"[\d\.]+";

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

    private readonly Option<string> venvOption = new("--venv-path", "-venv")
    {
        Description = "Path to Python virtual environment to use for Python requirements checks.",
        Required = false
    };

    protected override Command GetCommand() =>
        new("setup", "Verify environment setup for MCP release tools")
        {
            languagesParam,
            allLangOption,
            SharedOptions.PackagePath,
            venvOption
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var langs = parseResult.GetValue(languagesParam);
        var allLangs = parseResult.GetValue(allLangOption);
        var parsed = allLangs ? Enum.GetValues<SdkLanguage>().ToHashSet() : langs.ToHashSet();
        var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
        var venvPath = parseResult.GetValue(venvOption);
        return await VerifySetup(parsed, packagePath, venvPath, ct);
    }

    [McpServerTool(Name = "azsdk_verify_setup"), Description("Verifies the developer environment for MCP release tool requirements. Accepts a list of supported languages to check requirements for, and the packagePath of the repo to check. Accepts a specific Python virtual environment path to use for Python requirements checks.")]
    public async Task<VerifySetupResponse> VerifySetup(HashSet<SdkLanguage> langs = null, string packagePath = null, string venvPath = null, CancellationToken ct = default)
    {
        try
        {
            List<SetupRequirements.Requirement> reqsToCheck = GetRequirements(langs, packagePath ?? Environment.CurrentDirectory, venvPath, ct);

            VerifySetupResponse response = new VerifySetupResponse
            {
                Results = new List<RequirementCheckResult>()
            };

            // Start all checks concurrently
            var checkTasks = new List<Task<(SetupRequirements.Requirement req, DefaultCommandResponse result)>>();
            
            foreach (var req in reqsToCheck)
            {
                logger.LogInformation("Checking requirement: {Requirement}, Check: {Check}, Instructions: {Instructions}",
                    req.requirement, req.check, req.instructions);

                var task = RunCheck(req, packagePath, venvPath, ct).ContinueWith(t => (req, t.Result), TaskScheduler.Default);
                checkTasks.Add(task);
            }

            var results = await Task.WhenAll(checkTasks);

            foreach (var (req, result) in results)
            {
                if (result.ExitCode != 0)
                {
                    logger.LogWarning("Requirement check failed for {Requirement}. Suggested install command: {Instruction}", req.requirement, req.instructions);

                    response.ResponseErrors ??= new List<string>() ;
                    response.ResponseErrors.Add($"Requirement check failed for {req.requirement}. Error: {result.ResponseError}");

                    response.Results.Add(new RequirementCheckResult
                    {
                        Requirement = req.requirement,
                        Instructions = req.instructions,
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

    private async Task<DefaultCommandResponse> RunCheck(SetupRequirements.Requirement req, string packagePath, string venvPath, CancellationToken ct)
    {
        var command = req.check;
        var options = new ProcessOptions(
            command[0],
            args: command.Skip(1).ToArray(),
            timeout: TimeSpan.FromSeconds(COMMAND_TIMEOUT_IN_SECONDS),
            logOutputStream: true,
            workingDirectory: venvPath ?? packagePath
        );

        var trimmed = string.Empty;
        try
        {
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
        catch (Exception ex)
        {
            logger.LogError(ex, "Command {Command} failed to execute.", string.Join(' ', command));
            return new DefaultCommandResponse
            {
                ResponseError = $"Command {string.Join(' ', command)} failed to execute. Exception: {ex.Message}"
            };
        }

        logger.LogInformation("Command {Command} succeeded. Output: {Output}", string.Join(' ', command), trimmed);

        return new DefaultCommandResponse
        {
            Message = $"Command {string.Join(' ', command)} succeeded. Output: {trimmed}"
        };
    }

    private List<SetupRequirements.Requirement> GetRequirements(HashSet<SdkLanguage> languages, string packagePath, string venvPath, CancellationToken ct)
    {
        // Check core requirements before language-specific requirements
        var parsedReqs = ParseRequirements(ct);
        var reqsToCheck = GetCoreRequirements(parsedReqs, ct);

        // Per-language requirements
        List<LanguageService> languageSvs = [];
        if (languages == null || languages.Count == 0)
        {
            // Detect language if none given
            if (string.IsNullOrEmpty(packagePath))
            {
                logger.LogWarning("Could not resolve requirements checker for the specified languages from path {PackagePath}. Checking only core requirements. Please provide languages explicitly to check language requirements.", packagePath);
                return reqsToCheck;
            }

            languageSvs.Add(GetLanguageService(packagePath));
        } 
        else
        {
            foreach (var lang in languages)
            {
                languageSvs.Add(GetLanguageService(lang));
            }
        }

        if (languageSvs.Count == 0)
        {
            throw new Exception("Could not resolve requirements checker for the specified languages.");
        }

        foreach (var getter in languageSvs)
        {
            if (getter == null)
            {
                logger.LogError("Could not resolve requirements checker for one of the specified languages.");
                continue;
            }

            if (getter is PythonLanguageService pythonReqCheck && !string.IsNullOrEmpty(venvPath))
            {
                // If checking Python and venv path provided, use it
                reqsToCheck.AddRange(pythonReqCheck.GetRequirements(packagePath, parsedReqs, venvPath, ct));
                continue;
            }

            reqsToCheck.AddRange(getter.GetRequirements(packagePath, parsedReqs, ct));
        }

        return reqsToCheck ?? new List<SetupRequirements.Requirement>();
    }

    private List<SetupRequirements.Requirement> GetCoreRequirements(Dictionary<string, List<SetupRequirements.Requirement>> categories, CancellationToken ct)
    {
        if (categories.TryGetValue("core", out var reqs))
        {
            return reqs;
        }
        logger.LogWarning("No core requirements found in the requirements JSON.");
        return new List<SetupRequirements.Requirement>();
    }

    private Dictionary<string, List<SetupRequirements.Requirement>> ParseRequirements(CancellationToken ct = default)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var names = assembly.GetManifestResourceNames();
        using var stream = assembly.GetManifestResourceStream("Azure.Sdk.Tools.Cli.Configuration.RequirementsV1.json");
        var setupRequirements = JsonSerializer.Deserialize<SetupRequirements>(stream);

        if (setupRequirements == null)
        {
            throw new Exception("Failed to parse requirements JSON.");
        }

        return setupRequirements.categories;
    }

    private string CheckVersion(string output, SetupRequirements.Requirement req)
    {
        // Return empty string if version requirement is satisfied, else return version to upgrade to        
        var match = System.Text.RegularExpressions.Regex.Match(req.requirement, REQ_VERSION_PATTERN);
        
        if (!match.Success)
        {
            // No version specified in the requirement
            return String.Empty;
        }

        string operatorSymbol = match.Groups[1].Value;
        string requiredVersion = match.Groups[2].Value.Trim();

        logger.LogInformation("Requires version: {requiredVersion}", requiredVersion);

        // Parse the output version
        var outputVersionMatch = System.Text.RegularExpressions.Regex.Match(output, OUTPUT_VERSION_PATTERN);
        if (!outputVersionMatch.Success)
        {
            // Unable to parse the version from the output
            return requiredVersion;
        }

        string installedVersion = outputVersionMatch.Value.Trim();

        logger.LogInformation("Installed version: {installedVersion}", installedVersion);


        if (Version.TryParse(requiredVersion, out var requiredVer) && Version.TryParse(installedVersion, out var installedVer))
        {
            return operatorSymbol switch
            {
                ">=" => installedVer >= requiredVer ? string.Empty : requiredVersion,
                "<=" => installedVer <= requiredVer ? string.Empty : requiredVersion,
                ">" => installedVer > requiredVer ? string.Empty : requiredVersion,
                "<" => installedVer < requiredVer ? string.Empty : requiredVersion,
                "=" => installedVer == requiredVer ? string.Empty : requiredVersion,
                _ => requiredVersion,
            };
        }

        logger.LogWarning("Failed to parse requirement versions as System.Version.");
        return requiredVersion;
    }
}
