using System.CommandLine;
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

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

    private readonly Option<string> languageOption = RequiredOption("--language", "The SDK language.");
    private readonly Option<string> packageNameOption = RequiredOption("--package-name", "The package name.");
    private readonly Option<string> packageVersionOption = RequiredOption("--package-version", "The released package version.");
    private readonly Option<string> apiHashOption = new("--api-hash")
    {
        Description = "The API Review Hub hash for the released API artifact."
    };

    protected override Command GetCommand() => new(
        CommandName,
        "Mark a package as released in API Review Hub and APIView")
    {
        languageOption,
        packageNameOption,
        packageVersionOption,
        apiHashOption
    };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct) =>
        await MarkReleasedAsync(
            parseResult.GetValue(languageOption)!,
            parseResult.GetValue(packageNameOption)!,
            parseResult.GetValue(packageVersionOption)!,
            parseResult.GetValue(apiHashOption) ?? string.Empty,
            ct);

    public async Task<PackageMarkReleasedResponse> MarkReleasedAsync(
        string language,
        string packageName,
        string packageVersion,
        string apiHash,
        CancellationToken ct = default)
    {
        try
        {
            ReleaseBackendResult reviewHubResult;
            try
            {
                await apiReviewHubService.MarkPackageReleasedAsync(language, packageName, packageVersion, apiHash, ct);
                reviewHubResult = new ReleaseBackendResult { Succeeded = true, Message = "Package marked released." };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to mark {packageName} {packageVersion} released in API Review Hub", packageName, packageVersion);
                reviewHubResult = new ReleaseBackendResult { Succeeded = false, Message = ex.Message };
            }

            ReleaseBackendResult apiViewResult;
            try
            {
                var result = await apiViewService.MarkPackageReleasedAsync(packageName, language, packageVersion, ct);
                string releaseState = result.IsReleased
                    ? $"already released{(result.ReleasedOn.HasValue ? $" on {result.ReleasedOn.Value:O}" : string.Empty)}"
                    : "not released";
                apiViewResult = new ReleaseBackendResult
                {
                    Succeeded = true,
                    Message = $"Dry run resolved revision {result.RevisionId} (review {result.ReviewId}); revision is {releaseState}."
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to mark {packageName} {packageVersion} shipped in APIView", packageName, packageVersion);
                apiViewResult = new ReleaseBackendResult { Succeeded = false, Message = ex.Message };
            }

            List<string> errors = [];
            if (!reviewHubResult.Succeeded)
            {
                errors.Add($"API Review Hub: {reviewHubResult.Message}");
            }
            if (!apiViewResult.Succeeded)
            {
                errors.Add($"APIView: {apiViewResult.Message}");
            }

            return new PackageMarkReleasedResponse
            {
                PackageName = packageName,
                Version = packageVersion,
                ApiHash = apiHash,
                ApiReviewHub = reviewHubResult,
                ApiView = apiViewResult,
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
                ApiHash = apiHash,
                ApiReviewHub = new ReleaseBackendResult { Succeeded = false, Message = "Not completed." },
                ApiView = new ReleaseBackendResult { Succeeded = false, Message = "Not completed." },
                ResponseErrors = [$"Unexpected error: {ex.Message}"]
            };
        }
    }

    private static Option<string> RequiredOption(string name, string description) => new(name)
    {
        Description = description,
        Required = true
    };
}