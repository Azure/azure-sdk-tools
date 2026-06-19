// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.ApiReview;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.ApiReview;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Package;

[McpServerToolType, Description("Create API review pull requests from package API artifacts.")]
public class CreateApiReviewTool(
    IGitHelper gitHelper,
    IGitHubService gitHubService,
    IApiReviewGitService apiReviewGitService,
    IApiReviewPackageResolver apiReviewPackageResolver,
    ILogger<CreateApiReviewTool> toolLogger,
    IEnumerable<LanguageService> languageServices) : LanguageMcpTool(languageServices, gitHelper, toolLogger)
{
    private const string CommandName = "create-api-review";
    private const string ToolName = "azsdk_package_create_api_review";
    private const bool CreateDraftPullRequest = true;

    private readonly Option<string> packageNameOption = new("--package-name")
    {
        Description = "Package name to create an API review for.",
        Required = true
    };

    private readonly Option<string> baseOption = new("--base")
    {
        Description = "Baseline package release tag or ref.",
        Required = true
    };

    private readonly Option<string> targetOption = new("--target")
    {
        Description = "Target package release tag, remote branch, or owner:branch fork reference.",
        Required = true
    };

    private readonly Option<bool> dryRunOption = new("--dry-run")
    {
        Description = "Generate and validate API review artifacts without pushing branches or opening a pull request.",
        DefaultValueFactory = _ => false
    };

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

    protected override Command GetCommand() =>
        new McpCommand(CommandName, "Create an API review pull request for a package API diff", ToolName)
        {
            packageNameOption,
            baseOption,
            targetOption,
            dryRunOption
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        return await CreateApiReviewAsync(
            parseResult.GetValue(packageNameOption)!,
            parseResult.GetValue(baseOption)!,
            parseResult.GetValue(targetOption)!,
            parseResult.GetValue(dryRunOption),
            ct);
    }

    [McpServerTool(Name = ToolName), Description("Create an API review pull request for a package API diff. The target can be a tag, remote branch, or owner:branch fork reference.")]
    public async Task<CreateApiReviewResponse> CreateApiReviewAsync(
        [Description("Package name to create an API review for.")]
        string packageName,
        [Description("Baseline package release tag or ref.")]
        string baseRef,
        [Description("Target package release tag, remote branch, or owner:branch fork reference.")]
        string target,
        bool dryRun = false,
        CancellationToken ct = default)
    {
        try
        {
            var response = new CreateApiReviewResponse
            {
                PackageName = packageName,
                Base = baseRef,
                Target = ApiReviewTarget.Parse(target),
                Messages = []
            };

            if (string.IsNullOrWhiteSpace(packageName))
            {
                response.ResponseError = "Package name is required.";
                return response;
            }

            var repoRoot = await gitHelper.DiscoverRepoRootAsync(Environment.CurrentDirectory, ct);
            var repoName = await gitHelper.GetRepoNameAsync(repoRoot, ct);
            response.SdkRepoName = repoName;

            var language = await SdkLanguageHelpers.GetLanguageForRepoPathAsync(gitHelper, repoRoot, ct);
            var languageService = GetLanguageService(language);
            if (languageService == null)
            {
                response.ResponseError = $"API review creation is not supported for repository '{repoName}'.";
                return response;
            }

            response.Language = language;
            var baseTarget = ApiReviewTarget.Parse(baseRef);
            var targetRef = response.Target;
            var tempRoot = Path.Combine(Path.GetTempPath(), "azsdk-api-review", Guid.NewGuid().ToString("N"));
            var baseWorktree = Path.Combine(tempRoot, "base");
            var targetWorktree = Path.Combine(tempRoot, "target");

            try
            {
                await apiReviewGitService.FetchTargetAsync(repoRoot, repoName, baseTarget, ct);
                await apiReviewGitService.FetchTargetAsync(repoRoot, repoName, targetRef, ct);
                await apiReviewGitService.AddWorktreeAsync(repoRoot, baseWorktree, baseTarget.GitRef, ct);
                await apiReviewGitService.AddWorktreeAsync(repoRoot, targetWorktree, targetRef.GitRef, ct);

                var basePackage = apiReviewPackageResolver.ResolvePackage(packageName, baseWorktree);
                if (!basePackage.Success || basePackage.Package == null)
                {
                    response.ResponseError = basePackage.ErrorMessage;
                    return response;
                }

                var targetPackage = apiReviewPackageResolver.ResolvePackage(packageName, targetWorktree);
                if (!targetPackage.Success || targetPackage.Package == null)
                {
                    response.ResponseError = targetPackage.ErrorMessage;
                    return response;
                }

                var baseArtifacts = await GenerateArtifactsAsync(languageService, packageName, basePackage.Package, repoRoot, baseWorktree, baseRef, Path.Combine(tempRoot, "base-artifacts"), ct);
                if (!baseArtifacts.Success)
                {
                    response.ResponseError = baseArtifacts.ErrorMessage;
                    return response;
                }

                var targetArtifacts = await GenerateArtifactsAsync(languageService, packageName, targetPackage.Package, repoRoot, targetWorktree, target, Path.Combine(tempRoot, "target-artifacts"), ct);
                if (!targetArtifacts.Success)
                {
                    response.ResponseError = targetArtifacts.ErrorMessage;
                    return response;
                }

                response.Artifacts = targetArtifacts.Artifacts;
                response.Messages.Add($"Generated {baseArtifacts.Artifacts.Count} baseline artifact(s) and {targetArtifacts.Artifacts.Count} target artifact(s).");

                response.BaseBranch = CreateBranchName(packageName, "base", baseRef);
                response.HeadBranch = CreateBranchName(packageName, "target", $"{baseRef}-{target}");

                if (dryRun)
                {
                    response.Messages.Add("Dry run completed. No branches were pushed and no pull request was created.");
                    return response;
                }

                await apiReviewGitService.MaterializeReviewBranchAsync(repoRoot, response.BaseBranch, baseTarget.GitRef, baseArtifacts.Artifacts, $"Generate baseline API review artifacts for {packageName}", ct);
                await apiReviewGitService.MaterializeReviewBranchAsync(repoRoot, response.HeadBranch, response.BaseBranch, targetArtifacts.Artifacts, $"Generate target API review artifacts for {packageName}", ct);
                await apiReviewGitService.PushBranchAsync(repoRoot, response.BaseBranch, ct);
                await apiReviewGitService.PushBranchAsync(repoRoot, response.HeadBranch, ct);

                var headRepoOwner = await gitHelper.GetRepoOwnerNameAsync(repoRoot, findUpstreamParent: false, ct);
                var targetRepoOwner = await gitHelper.GetRepoOwnerNameAsync(repoRoot, findUpstreamParent: true, ct);
                var headBranch = $"{headRepoOwner}:{response.HeadBranch}";
                var title = $"API review for {packageName}: {baseRef} -> {target}";
                var body = BuildPullRequestBody(packageName, baseRef, targetRef, baseArtifacts.Artifacts);
                var prResult = await gitHubService.CreatePullRequestAsync(repoName, targetRepoOwner, response.BaseBranch, headBranch, title, body, CreateDraftPullRequest, ct);

                response.PullRequestUrl = prResult.Url;
                response.Messages.AddRange(prResult.Messages);
                if (string.IsNullOrWhiteSpace(prResult.Url))
                {
                    response.ResponseError = "Failed to create API review pull request. See messages for details.";
                }
            }
            finally
            {
                await apiReviewGitService.RemoveWorktreeAsync(repoRoot, baseWorktree, ct);
                await apiReviewGitService.RemoveWorktreeAsync(repoRoot, targetWorktree, ct);
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            toolLogger.LogError(ex, "Failed to create API review for package {PackageName}", packageName);
            return new CreateApiReviewResponse
            {
                PackageName = packageName,
                Base = baseRef,
                ResponseError = $"Failed to create API review: {ex.Message}"
            };
        }
    }

    private static async Task<ApiReviewArtifactResult> GenerateArtifactsAsync(LanguageService languageService, string packageName, ApiReviewPackageInfo packageInfo, string repoRoot, string worktreeRoot, string refName, string outputDirectory, CancellationToken ct)
    {
        return await languageService.GenerateApiReviewArtifactsAsync(new ApiReviewArtifactRequest
        {
            PackageName = packageName,
            PackagePath = packageInfo.PackagePath,
            PackageRelativePath = packageInfo.RelativePath,
            RepoRoot = repoRoot,
            WorktreeRoot = worktreeRoot,
            Ref = refName,
            OutputDirectory = outputDirectory
        }, ct);
    }

    private static string CreateBranchName(string packageName, string kind, string refName)
    {
        return $"api-review/{SanitizeGitRefSegment(packageName)}/{kind}/{SanitizeGitRefSegment(refName)}";
    }

    private static string SanitizeGitRefSegment(string value)
    {
        var sanitized = Regex.Replace(value.Trim(), @"[^A-Za-z0-9._/-]+", "-");
        sanitized = sanitized.Replace("..", ".", StringComparison.Ordinal).Trim('/', '.', '-');
        return string.IsNullOrWhiteSpace(sanitized) ? "ref" : sanitized;
    }

    private static string BuildPullRequestBody(string packageName, string baseRef, ApiReviewTarget target, IReadOnlyList<ApiReviewArtifact> artifacts)
    {
        var targetDescription = target.Kind switch
        {
            ApiReviewTargetKind.ForkBranch => $"fork branch `{target.Owner}:{target.Branch}`",
            ApiReviewTargetKind.RemoteBranch => $"remote branch `{target.Remote}/{target.Branch}`",
            _ => $"target tag `{target.Raw}`"
        };

        return $"""
        This draft PR contains generated API review artifacts for `{packageName}`.

        Baseline: `{baseRef}`
        Target: {targetDescription}

        Artifacts:
        {string.Join(Environment.NewLine, artifacts.Select(artifact => $"- `{artifact.ReviewPath}`"))}
        """;
    }
}
