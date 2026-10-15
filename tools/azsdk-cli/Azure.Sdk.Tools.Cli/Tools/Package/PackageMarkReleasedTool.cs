using System.CommandLine;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Services.APIView;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.Package;

public class PackageMarkReleasedTool(
    IApiReviewHubService apiReviewHubService,
    IAPIViewService apiViewService,
    ILogger<PackageMarkReleasedTool> logger) : MCPTool
{
    private const string CommandName = "mark-released";
    private static readonly JsonSerializerOptions responseSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

    private readonly Option<string> languageOption = RequiredOption("--language", "The SDK language.");
    private readonly Option<string> packageNameOption = RequiredOption("--package-name", "The package name.");
    private readonly Option<string> packageVersionOption = RequiredOption("--package-version", "The released package version.");
    private readonly Option<string> apiHashOption = new("--api-hash")
    {
        Description = "The API Review Hub hash for the released API artifact."
    };
    private readonly Option<string> repoOwnerOption = new("--repo-owner")
    {
        Description = "The GitHub repository owner to query in API Review Hub. Optional; when omitted, the service default is used."
    };
    private readonly Option<bool> dryRunOption = new("--dry-run")
    {
        Description = "Preview the release operation without marking the package as released."
    };

    protected override Command GetCommand() => new(
        CommandName,
        "Mark a package as released in API Review Hub and APIView")
    {
        languageOption,
        packageNameOption,
        packageVersionOption,
        apiHashOption,
        repoOwnerOption,
        dryRunOption
    };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct) =>
        await MarkReleasedAsync(
            parseResult.GetValue(languageOption)!,
            parseResult.GetValue(packageNameOption)!,
            parseResult.GetValue(packageVersionOption)!,
            parseResult.GetValue(apiHashOption) ?? string.Empty,
            parseResult.GetValue(repoOwnerOption) ?? string.Empty,
            parseResult.GetValue(dryRunOption),
            ct);

    public async Task<PackageMarkReleasedResponse> MarkReleasedAsync(
        string language,
        string packageName,
        string packageVersion,
        string apiHash,
        string repoOwner,
        bool dryRun = false,
        CancellationToken ct = default)
    {
        try
        {
            JsonElement? reviewHubResponse = null;
            bool reviewHubSucceeded = false;
            bool reviewHubSkipped = false;
            string reviewHubMessage;
            if (string.IsNullOrWhiteSpace(apiHash))
            {
                reviewHubSkipped = true;
                reviewHubMessage = "Skipped because apiHash is required.";
                reviewHubResponse = JsonSerializer.SerializeToElement(new
                {
                    Skipped = true,
                    Message = reviewHubMessage
                }, responseSerializerOptions);
            }
            else
            {
                try
                {
                    var result = await apiReviewHubService.MarkPackageReleasedAsync(language, packageName, packageVersion, apiHash, repoOwner, ct, dryRun);
                    reviewHubResponse = JsonSerializer.SerializeToElement(result, responseSerializerOptions);
                    reviewHubSucceeded = true;
                    string reviewHubAction = dryRun ? "Dry run resolved" : "Release request resolved";
                    reviewHubMessage = $"{reviewHubAction} package version {result.PackageVersionId}; approval is {result.ApprovalStatus}, release state is {(result.IsReleased ? "released" : "not released")}.";
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to mark {packageName} {packageVersion} released in API Review Hub", packageName, packageVersion);
                    reviewHubResponse = GetRawErrorResponse(ex);
                    reviewHubMessage = ex.Message;
                }
            }

            JsonElement? apiViewResponse = null;
            bool apiViewSucceeded = false;
            bool apiViewFailureIsFatal = false;
            string apiViewMessage;
            try
            {
                var result = await apiViewService.MarkPackageReleasedAsync(packageName, language, packageVersion, ct, dryRun);
                apiViewResponse = JsonSerializer.SerializeToElement(result, responseSerializerOptions);
                apiViewSucceeded = true;
                string releaseState = result.IsReleased
                    ? $"already released{(result.ReleasedOn.HasValue ? $" on {result.ReleasedOn.Value:O}" : string.Empty)}"
                    : "not released";
                string apiViewAction = dryRun ? "Dry run resolved" : "Release request resolved";
                apiViewMessage = $"{apiViewAction} revision {result.RevisionId} (review {result.ReviewId}); revision is {releaseState}.";
            }
            catch (Exception ex)
            {
                apiViewFailureIsFatal = ex is not HttpRequestException { StatusCode: { } statusCode }
                    || (int)statusCode >= 500;
                if (!apiViewFailureIsFatal)
                {
                    logger.LogWarning("APIView returned a non-server error while marking {packageName} {packageVersion} released", packageName, packageVersion);
                }
                else
                {
                    logger.LogError(ex, "Failed to mark {packageName} {packageVersion} released in APIView", packageName, packageVersion);
                }
                apiViewMessage = ex.Message;
            }

            List<string> errors = [];
            if (!reviewHubSucceeded && !apiViewSucceeded)
            {
                if (!reviewHubSkipped)
                {
                    errors.Add($"API Review Hub: {reviewHubMessage}");
                }
                errors.Add($"APIView: {apiViewMessage}");
            }

            return new PackageMarkReleasedResponse
            {
                PackageName = packageName,
                Version = packageVersion,
                ApiReviewHub = reviewHubResponse,
                ApiReviewHubSucceeded = reviewHubSucceeded,
                ApiReviewHubSkipped = reviewHubSkipped,
                ApiReviewHubMessage = reviewHubMessage,
                ApiView = apiViewResponse,
                ApiViewSucceeded = apiViewSucceeded,
                ApiViewMessage = apiViewMessage,
                ResponseErrors = errors
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while marking {packageName} {packageVersion} released", packageName, packageVersion);
            return new PackageMarkReleasedResponse
            {
                PackageName = packageName,
                Version = packageVersion,
                ApiReviewHubMessage = "Not completed.",
                ApiViewMessage = "Not completed.",
                ResponseErrors = [$"Unexpected error: {ex.Message}"]
            };
        }
    }

    private static JsonElement? GetRawErrorResponse(Exception exception)
    {
        if (exception is not ApiReviewHubRequestException { Content: { } content }
            || string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(content);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Option<string> RequiredOption(string name, string description) => new(name)
    {
        Description = description,
        Required = true
    };
}