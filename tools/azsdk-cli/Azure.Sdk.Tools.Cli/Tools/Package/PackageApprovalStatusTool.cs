using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Services.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Tools.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Package;

[McpServerToolType]
public class PackageApprovalStatusTool(
    IPackageReleaseStatusService packageReleaseStatusService,
    ILogger<PackageApprovalStatusTool> logger) : MCPTool
{
    private static readonly string[] SupportedLanguages = [.. ApiReviewHubTool.DefaultTargetRepos.Keys.Order(StringComparer.OrdinalIgnoreCase)];
    private const string GetApprovalStatusToolName = "azsdk_package_get_approval_status";
    private const string DefaultEndpoint = "https://api-review-hub.azurewebsites.net";

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
        PackageReleaseStatusResponse response = await GetApprovalStatus(
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

    [McpServerTool(Name = GetApprovalStatusToolName), Description("Check API review release approval status using APIView and API Review Hub.")]
    public async Task<PackageReleaseStatusResponse> GetApprovalStatus(
        [Description("The SDK language.")] string language,
        [Description("The package name.")] string packageName,
        [Description("The package version to check.")] string packageVersion,
        [Description("The API Review Hub API hash to check. When omitted, the release gate cannot be approved but current approval status is returned.")] string apiHash = "",
        [Description("The GitHub repository owner to query in API Review Hub. Optional; when omitted, the service default is used.")] string repoOwner = "",
        CancellationToken ct = default)
    {
        try
        {
            var result = await packageReleaseStatusService.GetApprovalStatusAsync(DefaultEndpoint, language, packageName, packageVersion, apiHash, repoOwner, ct);
            var response = new PackageReleaseStatusResponse
            {
                Result = result,
                Details = BuildDetails(result, packageName, packageVersion, apiHash)
            };

            if (!result.IsApproved)
            {
                response.ResponseError = BuildFailureMessage(result, packageName, packageVersion);
            }

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get API review release approval status for {packageName}", packageName);
            return new PackageReleaseStatusResponse
            {
                ResponseError = $"Failed to get API review release approval status for {packageName}: {ex.Message}"
            };
        }
    }

    private static List<string> BuildDetails(PackageReleaseStatusResult result, string packageName, string packageVersion, string apiHash)
    {
        var details = new List<string>();

        details.Add("== API Review Hub (Primary) ==");
        if (result.ReviewHub.StatusCode is >= 200 and < 300)
        {
            details.Add($"Status Code: {result.ReviewHub.StatusCode}");
            details.Add($"Approved: {result.ReviewHub.IsApproved}");
            details.Add($"Reason: {result.ReviewHub.Reason ?? "none"}");
            if (!string.IsNullOrWhiteSpace(result.ReviewHub.AppliedInheritanceRule))
            {
                details.Add($"Applied Inheritance Rule: {result.ReviewHub.AppliedInheritanceRule}");
            }
            if (result.ReviewHub.Details?.Count > 0)
            {
                details.AddRange(result.ReviewHub.Details);
            }

            AddApprovalDetails(details, result.ReviewHub.Approvals, apiHash);
        }
        else
        {
            details.Add($"WARNING: Primary query failed for {packageName} {packageVersion}.");
            if (result.ReviewHub.StatusCode is not null)
            {
                details.Add($"Status Code: {result.ReviewHub.StatusCode}");
            }
            if (!string.IsNullOrWhiteSpace(result.ReviewHub.Error))
            {
                details.Add(result.ReviewHub.Error);
            }
        }

        if (result.ApiView != null)
        {
            details.Add(string.Empty);
            details.Add("== APIView (Legacy) ==");
            if (result.ApiView.StatusCode is >= 200 and < 300
                || string.Equals(result.ApiView.Reason, "reviewNotFound", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.ApiView.Reason, "languageNotSupported", StringComparison.OrdinalIgnoreCase))
            {
                details.Add("Queried because the primary API Review Hub result was not approved or could not be retrieved.");
                if (result.ApiView.StatusCode is not null)
                {
                    details.Add($"Status Code: {result.ApiView.StatusCode}");
                }
                details.Add($"Approved: {result.ApiView.IsApproved}");
                details.Add($"Package Name Approved: {result.ApiView.PackageNameApproved}");
                details.Add($"Reason: {result.ApiView.Reason}");
                details.AddRange(result.ApiView.Details);
            }
            else
            {
                details.Add($"WARNING: Fallback query failed for {packageName} {packageVersion}.");
                if (result.ApiView.StatusCode is not null)
                {
                    details.Add($"Status Code: {result.ApiView.StatusCode}");
                }
                if (!string.IsNullOrWhiteSpace(result.ApiView.Error))
                {
                    details.Add(result.ApiView.Error);
                }
            }
        }

        details.Add(string.Empty);
        details.Add("== Final Result ==");
        details.Add($"Approved: {result.IsApproved}");
        if (result.IsApproved)
        {
            details.Add($"Source: {GetSourceLabel(result.FinalSource)}");
        }
        else
        {
            details.Add($"Reason: {result.Reason}");
        }

        return details;
    }

    private static string BuildFailureMessage(PackageReleaseStatusResult result, string packageName, string packageVersion)
    {
        return result.Reason switch
        {
            "rejected" => $"API review release gate is rejected for {packageName} {packageVersion}.",
            "staleArtifact" => $"API review release gate cannot be approved for {packageName} {packageVersion} because the release candidate artifact is not the one that was approved.",
            "missingApiHash" => $"API review release gate cannot be approved for {packageName} {packageVersion} because no API hash was provided.",
            "repositoryNotSupported" => $"API review release gate cannot be evaluated by API Review Hub for {packageName} {packageVersion} because this repository is not currently supported.",
            "reviewNotFound" => $"API review release gate is not approved for {packageName} {packageVersion} because no APIView review was found.",
            "apiViewLanguageNotSupported" => $"API review release gate is not approved for {packageName} {packageVersion} because APIView does not support this language for release gating.",
            "queryFailed" => $"API review release gate status could not be determined for {packageName} {packageVersion} because both API Review Hub and APIView status queries failed.",
            _ => $"API review release gate is not approved for {packageName} {packageVersion}."
        };
    }

    private static void AddApprovalDetails(List<string> details, IReadOnlyList<ApiReviewHubApprovalRecord>? approvals, string apiHash)
    {
        if (approvals?.Count is not > 0)
        {
            details.Add("Approval records returned by service: none");
            return;
        }

        if (string.IsNullOrWhiteSpace(apiHash))
        {
            details.Add("No API hash was provided. Approval records returned by the service:");
        }
        else
        {
            details.Add($"Provided API hash: {apiHash}");
            details.Add("Approval records returned by the service:");
        }

        foreach (var approval in approvals.OrderByDescending(approval => ApiHashMatches(approval.ApiHash, apiHash)).ThenByDescending(approval => approval.LastUpdatedOn, StringComparer.Ordinal))
        {
            var matchText = ApiHashMatches(approval.ApiHash, apiHash) ? " [provided hash]" : string.Empty;
            var versionText = string.IsNullOrWhiteSpace(approval.Version) ? string.Empty : $" (version {approval.Version})";
            details.Add($"- {approval.Status}: {approval.ApiHash}{matchText}{versionText}");

            if (!string.IsNullOrWhiteSpace(approval.Id))
            {
                details.Add($"  Approval record ID: {approval.Id}");
            }

            if (!string.IsNullOrWhiteSpace(approval.LastUpdatedBy) || !string.IsNullOrWhiteSpace(approval.LastUpdatedOn))
            {
                details.Add($"  Updated by: {approval.LastUpdatedBy ?? "unknown"}{FormatOnSuffix(approval.LastUpdatedOn)}");
            }

            if (!string.IsNullOrWhiteSpace(approval.PullRequestUrl))
            {
                details.Add($"  Pull request: {approval.PullRequestUrl}");
            }
        }
    }

    private static bool ApiHashMatches(string approvalApiHash, string requestedApiHash)
    {
        return !string.IsNullOrWhiteSpace(requestedApiHash)
            && string.Equals(approvalApiHash, requestedApiHash, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatOnSuffix(string? lastUpdatedOn)
    {
        return string.IsNullOrWhiteSpace(lastUpdatedOn) ? string.Empty : $" on {lastUpdatedOn}";
    }

    private static string GetSourceLabel(string finalSource)
    {
        return finalSource switch
        {
            "ApiReviewHub" => "API Review Hub (Primary)",
            "APIView" => "APIView (Legacy)",
            _ => finalSource
        };
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