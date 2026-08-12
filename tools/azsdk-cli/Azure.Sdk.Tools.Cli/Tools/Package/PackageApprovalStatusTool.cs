using System.CommandLine;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Services.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Tools.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.Package;

public class PackageApprovalStatusTool : MCPTool
{
    private static readonly string[] SupportedLanguages = ["python", "java", "csharp", "js", "go", "cpp", "swift", "rust"];
    private readonly ApiReviewHubTool apiReviewHubTool;

    public PackageApprovalStatusTool(
        IApiReviewHubService apiReviewHubService,
        IApiReviewReleaseStatusService apiReviewReleaseStatusService,
        ILogger<ApiReviewHubTool> logger)
    {
        apiReviewHubTool = new ApiReviewHubTool(apiReviewHubService, apiReviewReleaseStatusService, logger);
    }

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

    private readonly Option<string> languageOption = CreateLanguageOption();
    private readonly Option<string> packageNameOption = RequiredOption("--package-name", "The package name.");
    private readonly Option<string> packageVersionOption = RequiredOption("--package-version", "The package version to check.");
    private readonly Option<string> apiHashOption = new("--api-hash")
    {
        Description = "The API Review Hub API hash to check. When omitted, the release gate cannot be approved but current approval status is returned."
    };
    private readonly Option<string> repoOwnerOption = new("--repo-owner")
    {
        Description = "The GitHub repository owner to query in API Review Hub. Optional; when omitted, the service default is used."
    };

    protected override Command GetCommand() => new(
        "get-approval-status",
        "Check API review release approval status using APIView and API Review Hub")
    {
        languageOption,
        packageNameOption,
        packageVersionOption,
        apiHashOption,
        repoOwnerOption
    };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        ApiReviewReleaseStatusResponse response = await apiReviewHubTool.GetApprovalStatus(
            parseResult.GetValue(languageOption) ?? string.Empty,
            parseResult.GetValue(packageNameOption) ?? string.Empty,
            parseResult.GetValue(packageVersionOption) ?? string.Empty,
            parseResult.GetValue(apiHashOption) ?? string.Empty,
            parseResult.GetValue(repoOwnerOption) ?? string.Empty,
            ct);

        if (string.Equals(parseResult.GetValue(SharedOptions.Format), "json", StringComparison.OrdinalIgnoreCase))
        {
            response.Details = null;
        }

        return response;
    }

    private static Option<string> CreateLanguageOption()
    {
        var option = RequiredOption("--language", $"The SDK language. Supported values: {string.Join(", ", SupportedLanguages)}.");
        option.Validators.Add(result =>
        {
            string? value = result.GetValueOrDefault<string>();
            if (!string.IsNullOrWhiteSpace(value) && !SupportedLanguages.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                result.AddError($"Invalid language '{value}'. Supported values: {string.Join(", ", SupportedLanguages)}.");
            }
        });
        return option;
    }

    private static Option<string> RequiredOption(string name, string description) => new(name)
    {
        Description = description,
        Required = true
    };
}