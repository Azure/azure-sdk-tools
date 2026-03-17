// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.SetupRequirements;
using Azure.Sdk.Tools.Cli.Helpers;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Tools.Core;
using Azure.Sdk.Tools.Cli.Services.Languages;

namespace Azure.Sdk.Tools.Cli.Tools.Verify;

/// <summary>
/// This tool verifies that the environment is set up with the required installations to run MCP release tools.
/// </summary>
[McpServerToolType, Description("This tool verifies that the environment is set up with the required installations to run MCP release tools.")]
public class VerifySetupTool : LanguageMcpTool
{
    private readonly IVerifySetupService verifySetupService;
    private readonly IPackageInfoHelper packageInfoHelper;

    public VerifySetupTool(
        IProcessHelper processHelper,
        ILogger<VerifySetupTool> logger,
        IGitHelper gitHelper,
        IPackageInfoHelper packageInfoHelper,
        IVerifySetupService verifySetupService,
        IEnumerable<LanguageService> languageServices) : base(languageServices, gitHelper, logger)
    {
        this.verifySetupService = verifySetupService;
        this.packageInfoHelper = packageInfoHelper;
    }

    private const string VerifySetupToolName = "azsdk_verify_setup";

    public override CommandGroup[] CommandHierarchy { get; set; } = [
        SharedCommandGroups.Verify,
        SharedCommandGroups.Setup,
    ];

    private static readonly HashSet<string> SupportedSdkLanguages = [ "All", .. Enum.GetNames<SdkLanguage>()
            .Where(n => n != nameof(SdkLanguage.Unknown))];

    internal static readonly Option<List<string>> LanguagesOption = new("--languages", "-l")
    {
        Description = $"List of space-separated SDK languages ({string.Join(" ", SupportedSdkLanguages.OrderBy(n => n))}) to check requirements for. Defaults to current repo's language.",
        Validators =
        {
            result =>
            {
                var badLanguages = (result.GetValueOrDefault<List<string>>() ?? [])
                    .Except(SupportedSdkLanguages, StringComparer.OrdinalIgnoreCase);

                foreach (var value in badLanguages)
                {
                    result.AddError($"Invalid language '{value}'");
                }
            }
        },
        Required = false,
        AllowMultipleArgumentsPerToken = true
    };

    protected override Command GetCommand()
    {
        var checkCommand = new McpCommand("check", "Verify environment setup for MCP release tools", VerifySetupToolName);
        checkCommand.Options.Add(LanguagesOption);
        checkCommand.Options.Add(SharedOptions.PackagePath);
        return checkCommand;
    }

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var languages = VerifySetupService.ParseLanguages(parseResult.GetValue(LanguagesOption) ?? []);
        var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
        return await VerifySetup(languages, packagePath, requirementsToInstall: null, ct);
    }

    [McpServerTool(Name = VerifySetupToolName), Description("Verifies the developer environment for MCP release tool requirements. Accepts a list of supported languages to check requirements for, the packagePath of the repo to check, and an optional list of requirement names to try installing. To auto-install, call with `requirementsToInstall` containing the exact requirement names the user wants to install.")]
    public async Task<VerifySetupResponse> VerifySetup(HashSet<SdkLanguage>? langs = null, string? packagePath = null, List<string>? requirementsToInstall = null, CancellationToken ct = default)
    {
        try
        {
            return await verifySetupService.VerifySetup(langs, packagePath, requirementsToInstall, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error verifying setup");
            return new VerifySetupResponse
            {
                ResponseError = $"Error processing request: {ex.Message}"
            };
        }
    }
}
