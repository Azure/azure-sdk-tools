// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers.EngSys;
using Azure.Sdk.Tools.Cli.Models.Pipeline;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.EngSys;

/// <summary>
/// Seam: <see cref="PipelineFixSurvivalJudge.EvaluateAsync"/> turns a set of Copilot commit SHAs and the
/// human commits that landed on top of them into a survival verification and the model's verdict, given
/// <see cref="IGitHubService"/> and <see cref="ICopilotAgentRunner"/>.
/// </summary>
[TestFixture]
public class PipelineFixSurvivalJudgeTests
{
    private const string Owner = "ReilleyMilne";
    private const string Repo = "azure-sdk-for-python";
    private const int PrNumber = 35;
    private const string CopilotSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string HumanSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private Mock<IGitHubService> gitHubService;
    private Mock<ICopilotAgentRunner> agentRunner;
    private PipelineFixSurvivalJudge judge;

    [SetUp]
    public void SetUp()
    {
        gitHubService = new Mock<IGitHubService>(MockBehavior.Loose);
        agentRunner = new Mock<ICopilotAgentRunner>(MockBehavior.Loose);
        judge = new PipelineFixSurvivalJudge(
            gitHubService.Object,
            agentRunner.Object,
            new TestLogger<PipelineFixSurvivalJudge>());

        // The judge fetches file lists for both sides and for the merged PR before it builds the prompt; a
        // loose mock would otherwise return null where the contract promises an empty list.
        gitHubService
            .Setup(g => g.GetCommitFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GitHubCommitFile>());
        gitHubService
            .Setup(g => g.GetPullRequestFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PullRequestFile>());
    }

    #region EvaluateAsync

    [Test]
    public async Task EvaluateAsync_NoHumanCommitsAfter_ReturnsVerifiedFixWithoutJudging()
    {
        var (verification, verdict) = await judge.EvaluateAsync(
            Owner, Repo, PrNumber, [CopilotSha], humanCommitsAfter: [], model: null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(verification, Is.EqualTo(CopilotFixVerification.CopilotVerifiedFix));
            Assert.That(verdict, Is.Null);
        });
        agentRunner.Verify(
            r => r.RunAsync(It.IsAny<CopilotAgent<PipelineFixEvaluationJudgeVerdict>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        gitHubService.Verify(
            g => g.GetCommitFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task EvaluateAsync_JudgeSaysContributionSurvived_ReturnsJudgeVerifiedFix()
    {
        GivenVerdict(survived: true);

        var (verification, verdict) = await judge.EvaluateAsync(
            Owner, Repo, PrNumber, [CopilotSha], [HumanCommit(HumanSha, CopilotSha)], model: null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(verification, Is.EqualTo(CopilotFixVerification.CopilotJudgeVerifiedFix));
            Assert.That(verdict!.CopilotContributionSurvived, Is.True);
        });
    }

    [Test]
    public async Task EvaluateAsync_JudgeSaysContributionOverridden_ReturnsJudgeVerifiedFailure()
    {
        GivenVerdict(survived: false);

        var (verification, _) = await judge.EvaluateAsync(
            Owner, Repo, PrNumber, [CopilotSha], [HumanCommit(HumanSha, CopilotSha)], model: null, CancellationToken.None);

        Assert.That(verification, Is.EqualTo(CopilotFixVerification.CopilotJudgeVerifiedFailure));
    }

    // An agent failure (rate limit, 5xx, dropped connection) is an infrastructure fault on our side, not a
    // verdict about the commit, so it must not read as either a survived or an overridden fix.
    [Test]
    public async Task EvaluateAsync_AgentThrows_ReturnsUndeterminedWithNullVerdict()
    {
        agentRunner
            .Setup(r => r.RunAsync(It.IsAny<CopilotAgent<PipelineFixEvaluationJudgeVerdict>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("503 from the model gateway"));

        var (verification, verdict) = await judge.EvaluateAsync(
            Owner, Repo, PrNumber, [CopilotSha], [HumanCommit(HumanSha, CopilotSha)], model: null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(verification, Is.EqualTo(CopilotFixVerification.Undetermined));
            Assert.That(verdict, Is.Null);
        });
    }

    // Cancellation is not the same as an agent failure: it must propagate rather than be swallowed into an
    // Undetermined verdict.
    [Test]
    public void EvaluateAsync_CancellationRequested_Rethrows()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() => judge.EvaluateAsync(
            Owner, Repo, PrNumber, [CopilotSha], [HumanCommit(HumanSha, CopilotSha)], model: null, cts.Token));

        agentRunner.Verify(
            r => r.RunAsync(It.IsAny<CopilotAgent<PipelineFixEvaluationJudgeVerdict>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task EvaluateAsync_ModelProvided_ForwardsItToTheAgent()
    {
        var captured = CaptureAgent(survived: true);

        await judge.EvaluateAsync(
            Owner, Repo, PrNumber, [CopilotSha], [HumanCommit(HumanSha, CopilotSha)], model: "claude-opus-4.8", CancellationToken.None);

        Assert.That(captured()!.Model, Is.EqualTo("claude-opus-4.8"));
    }

    // A human file that also changed a file Copilot touched could override the fix, so its patch is shown.
    // A human file Copilot never touched cannot, so it is listed by name only - the change is acknowledged
    // but its contents are kept out of the prompt.
    [Test]
    public async Task EvaluateAsync_HumanFileNotTouchedByCopilot_ListedByNameWithoutPatch()
    {
        GivenCommitFiles(CopilotSha, CommitFile("sdk/core/foo.py", "@@ copilot rewrote foo"));
        GivenCommitFiles(
            HumanSha,
            CommitFile("sdk/core/foo.py", "@@ human also edited foo"),
            CommitFile("docs/changelog.md", "@@ human unrelated changelog edit"));
        var captured = CaptureAgent(survived: true);

        await judge.EvaluateAsync(
            Owner, Repo, PrNumber, [CopilotSha], [HumanCommit(HumanSha, CopilotSha)], model: null, CancellationToken.None);

        var instructions = captured()!.Instructions;
        Assert.Multiple(() =>
        {
            // The human edit to the Copilot-touched file is shown in full.
            Assert.That(instructions, Does.Contain("human also edited foo"));
            // The unrelated human file is named but its patch is withheld.
            Assert.That(instructions, Does.Contain("docs/changelog.md"));
            Assert.That(instructions, Does.Not.Contain("human unrelated changelog edit"));
        });
    }

    // File relevance follows renames: a human commit that edits the pre-rename path of a file Copilot
    // renamed is still recognised as touching Copilot's change.
    [Test]
    public async Task EvaluateAsync_HumanFileMatchingCopilotPreviousName_ShownAsPatch()
    {
        GivenCommitFiles(CopilotSha, CommitFile("sdk/core/renamed.py", "@@ copilot renamed", previousFileName: "sdk/core/original.py"));
        GivenCommitFiles(HumanSha, CommitFile("sdk/core/original.py", "@@ human edited the old path"));
        var captured = CaptureAgent(survived: true);

        await judge.EvaluateAsync(
            Owner, Repo, PrNumber, [CopilotSha], [HumanCommit(HumanSha, CopilotSha)], model: null, CancellationToken.None);

        Assert.That(captured()!.Instructions, Does.Contain("human edited the old path"));
    }

    #endregion

    #region Arrange helpers

    private void GivenVerdict(bool survived) =>
        agentRunner
            .Setup(r => r.RunAsync(It.IsAny<CopilotAgent<PipelineFixEvaluationJudgeVerdict>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Verdict(survived));

    private Func<CopilotAgent<PipelineFixEvaluationJudgeVerdict>?> CaptureAgent(bool survived)
    {
        CopilotAgent<PipelineFixEvaluationJudgeVerdict>? captured = null;
        agentRunner
            .Setup(r => r.RunAsync(It.IsAny<CopilotAgent<PipelineFixEvaluationJudgeVerdict>>(), It.IsAny<CancellationToken>()))
            .Callback<CopilotAgent<PipelineFixEvaluationJudgeVerdict>, CancellationToken>((agent, _) => captured = agent)
            .ReturnsAsync(Verdict(survived));
        return () => captured;
    }

    private void GivenCommitFiles(string sha, params GitHubCommitFile[] files) =>
        gitHubService
            .Setup(g => g.GetCommitFilesAsync(Owner, Repo, sha, It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);

    private static PipelineFixEvaluationJudgeVerdict Verdict(bool survived) =>
        new()
        {
            CopilotContributionSurvived = survived,
            CopilotFixAddressedPipelineFailure = true,
            HumanChangesWereIrrelevantToFix = survived,
            Reasoning = "test verdict",
        };

    private static PullRequestCommit HumanCommit(string sha, string parentSha, string login = "dev") =>
        PrCommit(sha, [parentSha], login, login, verified: false);

    /// <summary>
    /// A commit as it appears in a pull request's commit listing. Octokit response models expose only an
    /// all-argument constructor, so the fields the judge inspects - the SHA, its first parent, and the
    /// author and committer names - are named and everything else is left at its default.
    /// </summary>
    private static PullRequestCommit PrCommit(
        string sha,
        IEnumerable<string> parentShas,
        string authorLogin,
        string committerName,
        bool verified)
    {
        var commit = new Commit(
            null, null, null, null, sha, null, null, "message",
            new Committer(authorLogin, "author@example.com", default),
            new Committer(committerName, "committer@example.com", default),
            null, Array.Empty<GitReference>(), 0,
            new Verification(verified, VerificationReason.Valid, null, null));

        return new PullRequestCommit(
            null, UserFor(authorLogin), null, commit, null, null, parentShas.Select(Ref).ToArray(), sha, null);
    }

    private static GitHubCommitFile CommitFile(string filename, string? patch, string status = "modified", string? previousFileName = null) =>
        new(filename, 0, 0, 0, status, null, null, null, null, patch, previousFileName);

    private static User UserFor(string login) =>
        new(null, null, null, 0, null, default, default, 0, null, 0, 0, null, null, 0, 0L, null,
            login, null, null, 0, null, 0, 0, 0, null, null, false, null, null);

    private static GitReference Ref(string sha) => new(null, null, null, null, sha, null, null);

    #endregion
}
