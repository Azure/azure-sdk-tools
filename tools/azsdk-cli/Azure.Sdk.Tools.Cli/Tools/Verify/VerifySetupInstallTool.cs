// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.SetupRequirements;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.Verify;

/// <summary>
/// CLI-only tool for installing missing environment requirements.
/// CLI: azsdk verify setup install [--tools ...] [--yes]
/// </summary>
public class VerifySetupInstallTool : MCPTool
{
    private readonly IVerifySetupService verifySetupService;

    public VerifySetupInstallTool(IVerifySetupService verifySetupService)
    {
        this.verifySetupService = verifySetupService;
    }

    public override CommandGroup[] CommandHierarchy { get; set; } = [
        SharedCommandGroups.Verify,
        SharedCommandGroups.Setup,
    ];

    private readonly Option<List<string>> toolsParam = new("--tools", "-t")
    {
        Description = "Specific tools to install by name.",
        AllowMultipleArgumentsPerToken = true
    };

    private readonly Option<bool> yesParam = new("--yes", "-y")
    {
        Description = "Install all missing installable tools without prompting.",
    };

    protected override Command GetCommand()
    {
        var installCommand = new Command("install", "Install missing environment requirements");
        installCommand.Options.Add(VerifySetupTool.LanguagesOption);
        installCommand.Options.Add(SharedOptions.PackagePath);
        installCommand.Options.Add(toolsParam);
        installCommand.Options.Add(yesParam);
        return installCommand;
    }

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var languages = VerifySetupService.ParseLanguages(parseResult.GetValue(VerifySetupTool.LanguagesOption) ?? []);
        var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
        var tools = parseResult.GetValue(toolsParam) ?? [];
        var yes = parseResult.GetValue(yesParam);

        return await HandleInstallCommand(languages, packagePath, tools, yes, ct);
    }

    /// <summary>
    /// CLI-only orchestration for the 'install' sub-command: discovers missing requirements,
    /// handles --tools/--yes flags and interactive prompts, then delegates to VerifySetupService.
    /// </summary>
    private async Task<CommandResponse> HandleInstallCommand(
        HashSet<SdkLanguage> languages, string? packagePath,
        List<string> tools, bool yes, CancellationToken ct)
    {
        // --tools foo bar → install exactly these
        if (tools.Count > 0)
        {
            return await verifySetupService.VerifySetup(languages, packagePath, tools, ct);
        }

        // No --tools → discover what's missing first
        var checkResult = await verifySetupService.VerifySetup(languages, packagePath, requirementsToInstall: null, ct);
        var installable = VerifySetupService.GetInstallableNames(checkResult);

        if (installable.Count == 0)
        {
            return checkResult;
        }

        // --yes → install all installable without prompting
        if (yes)
        {
            return await verifySetupService.VerifySetup(languages, packagePath, installable, ct);
        }

        // Interactive: prompt y/N for each installable requirement
        var approved = PromptForApproval(installable);

        if (approved.Count == 0)
        {
            return checkResult;
        }

        return await verifySetupService.VerifySetup(languages, packagePath, approved, ct);
    }

    private static List<string> PromptForApproval(List<string> installable)
    {
        var approved = new List<string>();
        Console.WriteLine("\nThe following requirements can be auto-installed:");
        foreach (var reqName in installable)
        {
            Console.Write($"  Install {reqName}? [y/N]: ");
            var response = Console.ReadLine()?.Trim();
            if (string.Equals(response, "y", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(response, "yes", StringComparison.OrdinalIgnoreCase))
            {
                approved.Add(reqName);
            }
        }
        return approved;
    }
}
