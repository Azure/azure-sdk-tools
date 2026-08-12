using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.APIView;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Services.APIView;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Package;

[McpServerToolType, Description("Mark released SDK packages in API Review Hub and APIView")]
public class PackageMarkReleasedTool(
    IApiReviewHubService apiReviewHubService,
    IAPIViewService apiViewService,
    ILogger<PackageMarkReleasedTool> logger) : MCPTool
{
    private const string CommandName = "mark-released";
    private const string ToolName = "azsdk_package_mark_released";

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

    private readonly Option<string> languageOption = RequiredOption("--language", "The SDK language.");
    private readonly Option<string> packageNameOption = RequiredOption("--package-name", "The package name.");
    private readonly Option<string> packageVersionOption = RequiredOption("--package-version", "The released package version.");
    private readonly Option<string> apiHashOption = RequiredOption("--api-hash", "The API Review Hub hash for the released API artifact.");
    private readonly Option<string> sourceFilePathOption = RequiredOption("--source-file-path", "The source artifact path used by APIView.");
    private readonly Option<string> packageTypeOption = RequiredOption("--package-type", "The APIView package type.");
    private readonly Option<string?> reviewTokenFileNameOption = new("--review-token-file-name")
    {
        Description = "The APIView review token file name. When omitted, the source artifact is uploaded."
    };
    private readonly Option<string?> buildIdOption = new("--build-id") { Description = "The Azure Pipelines build ID containing the review token." };
    private readonly Option<string?> repoNameOption = new("--repo-name") { Description = "The owner/repository containing the pipeline build." };
    private readonly Option<string> artifactNameOption = new("--artifact-name")
    {
        Description = "The Azure Pipelines artifact name.",
        DefaultValueFactory = _ => "packages"
    };
    private readonly Option<string> projectOption = new("--project")
    {
        Description = "The Azure DevOps project containing the build.",
        DefaultValueFactory = _ => "internal"
    };
    private readonly Option<string?> sourceBranchOption = new("--source-branch") { Description = "The source branch associated with the APIView revision." };

    protected override Command GetCommand() => new McpCommand(
        CommandName,
        "Mark a package as released in API Review Hub and APIView",
        ToolName)
    {
        languageOption,
        packageNameOption,
        packageVersionOption,
        apiHashOption,
        sourceFilePathOption,
        packageTypeOption,
        reviewTokenFileNameOption,
        buildIdOption,
        repoNameOption,
        artifactNameOption,
        projectOption,
        sourceBranchOption
    };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct) =>
        await MarkReleasedAsync(
            parseResult.GetValue(languageOption)!,
            parseResult.GetValue(packageNameOption)!,
            parseResult.GetValue(packageVersionOption)!,
            parseResult.GetValue(apiHashOption)!,
            parseResult.GetValue(sourceFilePathOption)!,
            parseResult.GetValue(packageTypeOption)!,
            parseResult.GetValue(reviewTokenFileNameOption),
            parseResult.GetValue(buildIdOption),
            parseResult.GetValue(repoNameOption),
            parseResult.GetValue(artifactNameOption)!,
            parseResult.GetValue(projectOption)!,
            parseResult.GetValue(sourceBranchOption),
            ct);

    [McpServerTool(Name = ToolName), Description("Mark a released SDK package in API Review Hub and APIView.")]
    public async Task<PackageMarkReleasedResponse> MarkReleasedAsync(
        [Description("SDK language.")] string language,
        [Description("Package name.")] string packageName,
        [Description("Released package version.")] string packageVersion,
        [Description("API Review Hub API hash.")] string apiHash,
        [Description("Source artifact path used by APIView.")] string sourceFilePath,
        [Description("APIView package type.")] string packageType,
        [Description("Optional APIView review token file name.")] string? reviewTokenFileName = null,
        [Description("Build ID containing the review token.")] string? buildId = null,
        [Description("Repository containing the pipeline build.")] string? repoName = null,
        [Description("Pipeline artifact name.")] string artifactName = "packages",
        [Description("Azure DevOps project.")] string project = "internal",
        [Description("Source branch associated with the revision.")] string? sourceBranch = null,
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
                var request = new APIViewReleaseRequest
                {
                    SourceFilePath = sourceFilePath,
                    ReviewTokenFileName = reviewTokenFileName,
                    BuildId = buildId,
                    ArtifactName = artifactName,
                    RepoName = repoName,
                    PackageName = packageName,
                    Project = project,
                    PackageVersion = packageVersion,
                    PackageType = packageType,
                    SourceBranch = sourceBranch
                };
                await apiViewService.MarkPackageReleasedAsync(request, ct);
                apiViewResult = new ReleaseBackendResult { Succeeded = true, Message = "Revision marked shipped." };
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