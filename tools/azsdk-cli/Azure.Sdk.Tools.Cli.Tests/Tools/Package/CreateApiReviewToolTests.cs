// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.ApiReview;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.ApiReview;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Moq;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package;

public class CreateApiReviewToolTests
{
    [Test]
    public void ParseTargetSupportsTagRemoteBranchAndForkBranch()
    {
        var tag = ApiReviewTarget.Parse("azure-ai-projects_2.2.0");
        var remoteBranch = ApiReviewTarget.Parse("upstream/release/api-review");
        var forkBranch = ApiReviewTarget.Parse("someone:feature/api-review");

        Assert.Multiple(() =>
        {
            Assert.That(tag.Kind, Is.EqualTo(ApiReviewTargetKind.Tag));
            Assert.That(tag.GitRef, Is.EqualTo("azure-ai-projects_2.2.0"));

            Assert.That(remoteBranch.Kind, Is.EqualTo(ApiReviewTargetKind.RemoteBranch));
            Assert.That(remoteBranch.Remote, Is.EqualTo("upstream"));
            Assert.That(remoteBranch.Branch, Is.EqualTo("release/api-review"));
            Assert.That(remoteBranch.GitRef, Is.EqualTo("upstream/release/api-review"));

            Assert.That(forkBranch.Kind, Is.EqualTo(ApiReviewTargetKind.ForkBranch));
            Assert.That(forkBranch.Owner, Is.EqualTo("someone"));
            Assert.That(forkBranch.Branch, Is.EqualTo("feature/api-review"));
            Assert.That(forkBranch.GitRef, Is.EqualTo("someone/feature/api-review"));
        });
    }

    [Test]
    public void CommandParsesCreateApiReviewOptions()
    {
        var tool = CreateTool(out _, out _, out _, out _, out _);
        var command = tool.GetCommandInstances().Single(command => command.Name == "create-api-review");
        var parseConfig = new CommandLineConfiguration(command)
        {
            ResponseFileTokenReplacer = null
        };

        var parseResult = command.Parse("--package-name azure-ai-projects --base azure-ai-projects_2.1.0 --target azure-ai-projects_2.2.0 --dry-run", parseConfig);

        Assert.That(parseResult.Errors, Is.Empty);
    }

    [Test]
    public async Task CreateApiReviewDryRunGeneratesArtifactsWithoutPushingOrCreatingPr()
    {
        var tool = CreateTool(out var gitHelper, out var gitHubService, out var apiReviewGitService, out var packageResolver, out var languageService);

        gitHelper.Setup(helper => helper.DiscoverRepoRootAsync(Environment.CurrentDirectory, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repo");
        gitHelper.Setup(helper => helper.GetRepoNameAsync("/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync("azure-sdk-for-python");
        packageResolver.Setup(resolver => resolver.ResolvePackage("azure-ai-projects", It.IsAny<string>()))
            .Returns((string _, string worktreeRoot) => ApiReviewPackageResult.CreateSuccess(new ApiReviewPackageInfo
            {
                PackagePath = Path.Combine(worktreeRoot, "sdk", "ai", "azure-ai-projects"),
                RelativePath = "sdk/ai/azure-ai-projects",
                CodeownersPathExpression = "/sdk/ai/azure-ai-projects/"
            }));

        languageService.Setup(service => service.GenerateApiReviewArtifactsAsync(It.IsAny<ApiReviewArtifactRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiReviewArtifactRequest request, CancellationToken _) => ApiReviewArtifactResult.CreateSuccess(
                request.PackagePath,
                [
                    new ApiReviewArtifact
                    {
                        SourcePath = Path.Combine(request.OutputDirectory, "api.md"),
                        ReviewPath = "sdk/ai/azure-ai-projects/api-review/api.md"
                    },
                    new ApiReviewArtifact
                    {
                        SourcePath = Path.Combine(request.OutputDirectory, "api.metadata.yml"),
                        ReviewPath = "sdk/ai/azure-ai-projects/api-review/api.metadata.yml"
                    }
                ]));

        var result = await tool.CreateApiReviewAsync("azure-ai-projects", "azure-ai-projects_2.1.0", "azure-ai-projects_2.2.0", dryRun: true, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Language, Is.EqualTo(SdkLanguage.Python));
            Assert.That(result.Artifacts, Has.Count.EqualTo(2));
            Assert.That(result.Artifacts!.Select(artifact => artifact.ReviewPath), Does.Contain("sdk/ai/azure-ai-projects/api-review/api.md"));
            Assert.That(result.Artifacts!.Select(artifact => artifact.ReviewPath), Does.Contain("sdk/ai/azure-ai-projects/api-review/api.metadata.yml"));
            Assert.That(result.PullRequestUrl, Is.Null);
            Assert.That(result.Messages, Does.Contain("Dry run completed. No branches were pushed and no pull request was created."));
        });

        packageResolver.Verify(resolver => resolver.ResolvePackage("azure-ai-projects", It.IsAny<string>()), Times.Exactly(2));
        languageService.Verify(service => service.GenerateApiReviewArtifactsAsync(It.Is<ApiReviewArtifactRequest>(request =>
            request.PackagePath.Replace('\\', '/').EndsWith("sdk/ai/azure-ai-projects") && request.PackageRelativePath == "sdk/ai/azure-ai-projects"), It.IsAny<CancellationToken>()), Times.Exactly(2));
        apiReviewGitService.Verify(service => service.FetchTargetAsync("/repo", "azure-sdk-for-python", It.IsAny<ApiReviewTarget>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        apiReviewGitService.Verify(service => service.AddWorktreeAsync("/repo", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        apiReviewGitService.Verify(service => service.MaterializeReviewBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<ApiReviewArtifact>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        apiReviewGitService.Verify(service => service.PushBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        gitHubService.Verify(service => service.CreatePullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void PackageResolverFindsPackagePathFromCodeownersParser()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "azsdk-cli-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(tempRoot, ".github"));
            Directory.CreateDirectory(Path.Combine(tempRoot, "sdk", "ai", "azure-ai-projects"));
            File.WriteAllText(Path.Combine(tempRoot, ".github", "CODEOWNERS"), """
            # PRLabel: %Azure.AI.Projects
            /sdk/ai/azure-ai-projects/ @azure-sdk-write
            """);

            var resolver = new ApiReviewPackageResolver();
            var result = resolver.ResolvePackage("azure-ai-projects", tempRoot);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Package!.RelativePath, Is.EqualTo("sdk/ai/azure-ai-projects"));
                Assert.That(result.Package.PackagePath, Is.EqualTo(Path.Combine(tempRoot, "sdk", "ai", "azure-ai-projects")));
                Assert.That(result.Package.CodeownersPathExpression, Is.EqualTo("/sdk/ai/azure-ai-projects/"));
            });
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static CreateApiReviewTool CreateTool(
        out Mock<IGitHelper> gitHelper,
        out Mock<IGitHubService> gitHubService,
        out Mock<IApiReviewGitService> apiReviewGitService,
        out Mock<IApiReviewPackageResolver> packageResolver,
        out Mock<LanguageService> languageService)
    {
        gitHelper = new Mock<IGitHelper>();
        gitHubService = new Mock<IGitHubService>();
        apiReviewGitService = new Mock<IApiReviewGitService>();
        packageResolver = new Mock<IApiReviewPackageResolver>();
        languageService = new Mock<LanguageService>();
        languageService.SetupGet(service => service.Language).Returns(SdkLanguage.Python);

        apiReviewGitService.Setup(service => service.FetchTargetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ApiReviewTarget>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        apiReviewGitService.Setup(service => service.AddWorktreeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        apiReviewGitService.Setup(service => service.RemoveWorktreeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new CreateApiReviewTool(
            gitHelper.Object,
            gitHubService.Object,
            apiReviewGitService.Object,
                packageResolver.Object,
            new TestLogger<CreateApiReviewTool>(),
            [languageService.Object]);
    }
}