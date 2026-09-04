// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Codeowners;

internal class CodeownersLintHelperTests
{
    /// <summary>Owners and labels the spec's example fragments use. Anything absent is a violation.</summary>
    private static readonly string[] ValidOwners =
    [
        "test-user-02", "test-user-07", "test-user-09", "test-user-13",
        "test-user-18", "test-user-22", "test-user-23", "test-user-24",
    ];

    private static readonly string[] KnownLabels = ["AI Model Inference", "AI Projects", "OpenAI"];

    private const string AiFragment = "sdk/ai/owners.yaml";

    private static CodeownersLintHelper Lint(
        IEnumerable<string>? owners = null,
        IEnumerable<string>? labels = null,
        IDictionary<string, List<string>>? teams = null,
        IEnumerable<string>? orgVisible = null) =>
        new(
            OwnerValidatorFake.Create(owners ?? ValidOwners, teams, orgVisible),
            new FakeCommonLabelSource(labels ?? KnownLabels));

    private static async Task<IReadOnlyList<string>> RuleIds(
        CodeownersLintHelper lint,
        OwnersTestRepo repo,
        params string[] fragments)
    {
        var result = await lint.Lint(repo.Root, fragments, CancellationToken.None);

        return [.. result.AllViolations.Select(v => v.RuleId)];
    }

    [Test]
    public async Task ValidFragmentReportsNothing()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var result = await Lint().Lint(repo.Root, [AiFragment], CancellationToken.None);

        Assert.That(
            result.AllViolations.Select(v => $"{v.RuleId} {v.Description}"),
            Is.Empty);
    }

    [Test]
    public async Task EveryFragmentIsCheckedWhenNoneAreNamed()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var result = await Lint().Lint(repo.Root, [], CancellationToken.None);

        Assert.That(
            result.Fragments.Select(f => f.FilePath),
            Is.EquivalentTo(new[] { AiFragment, "sdk/openai/owners.yaml" }));
    }

    [Test]
    public async Task OneFragmentIsJudgedWithoutLoadingTheOthers()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        // A fragment nobody asked about is broken beyond parsing. Linting a different file must
        // still succeed, which is what makes this usable as a per-file pull request gate.
        repo.WriteFragment("sdk/broken", "this: is: not: valid: yaml:");

        var result = await Lint().Lint(repo.Root, [AiFragment], CancellationToken.None);

        Assert.That(result.Fragments.Select(f => f.FilePath), Is.EqualTo(new[] { AiFragment }));
        Assert.That(result.AllViolations, Is.Empty);
    }

    [Test]
    public async Task OwnerMissingFromTheMembershipCacheIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var result = await Lint(owners: ValidOwners.Except(["test-user-09"]))
            .Lint(repo.Root, [AiFragment], CancellationToken.None);

        var violation = result.AllViolations.First(v => v.RuleId == "LNT-OWN-001");
        Assert.That(violation.Description, Does.Contain("test-user-09"));
    }

    [Test]
    public async Task PrivateOrgMembershipIsReportedAsSelfServiceable()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var result = await Lint(orgVisible: ValidOwners.Except(["test-user-09"]))
            .Lint(repo.Root, [AiFragment], CancellationToken.None);

        var violation = result.AllViolations.First(v => v.RuleId == "LNT-OWN-001");
        Assert.That(violation.Detail, Does.Contain("github.com/orgs/Azure/people"));
    }

    [Test]
    public async Task TeamThatDoesNotDescendFromWriteIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/ai", FragmentOwnedBy("@Azure/some-other-team"));

        Assert.That(await RuleIds(Lint(), repo, AiFragment), Does.Contain("LNT-OWN-002"));
    }

    [Test]
    public async Task MalformedTeamAliasIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/ai", FragmentOwnedBy("@Contoso/team/extra"));

        Assert.That(await RuleIds(Lint(), repo, AiFragment), Does.Contain("LNT-OWN-002"));
    }

    [Test]
    public async Task TeamMembersCountTowardThePathMinimum()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/ai", FragmentWithOwners("@Azure/ai-team"));

        var teams = new Dictionary<string, List<string>>
        {
            ["Azure/ai-team"] = ["test-user-02", "test-user-24"],
        };

        // One declared owner, but it expands to two people, so the minimum of 2 is met.
        Assert.That(await RuleIds(Lint(teams: teams), repo, AiFragment), Does.Not.Contain("LNT-OWN-003"));
    }

    [Test]
    public async Task PathWithTooFewOwnersIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/ai", FragmentWithOwners("test-user-02"));

        Assert.That(await RuleIds(Lint(), repo, AiFragment), Does.Contain("LNT-OWN-003"));
    }

    [Test]
    public async Task LabelBlockWithTooFewServiceOwnersIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/ai", FragmentWithServiceOwners("test-user-02"));

        Assert.That(await RuleIds(Lint(), repo, AiFragment), Does.Contain("LNT-OWN-004"));
    }

    [Test]
    public async Task LabelOutsideTheCommonSetIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var result = await Lint(labels: ["OpenAI"]).Lint(repo.Root, [AiFragment], CancellationToken.None);

        Assert.That(result.AllViolations.Select(v => v.RuleId), Does.Contain("LNT-LBL-001"));
    }

    [Test]
    public async Task PathWithNoPrLabelsIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/ai", """
            version: 1
            paths:
              - path: .
                owners: [test-user-02, test-user-24]
            label-owners:
              - labels: [OpenAI]
                service-owners: [test-user-02, test-user-24]
            """);

        Assert.That(await RuleIds(Lint(), repo, AiFragment), Does.Contain("LNT-LBL-002"));
    }

    [Test]
    public async Task PrLabelWithNoOwnersInTheSameFileIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/ai", """
            version: 1
            paths:
              - path: .
                owners: [test-user-02, test-user-24]
                pr-labels: [OpenAI]
            label-owners:
              - labels: [AI Projects]
                service-owners: [test-user-02, test-user-24]
            """);

        var result = await Lint().Lint(repo.Root, [AiFragment], CancellationToken.None);

        var violation = result.AllViolations.First(v => v.RuleId == "LNT-LBL-003");
        Assert.That(violation.Description, Does.Contain("OpenAI"));
    }

    [Test]
    public async Task PathEscapingTheFragmentSubtreeIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/ai", """
            version: 1
            paths:
              - path: ../openai/
                owners: [test-user-02, test-user-24]
                pr-labels: [OpenAI]
            label-owners:
              - labels: [OpenAI]
                service-owners: [test-user-02, test-user-24]
            """);

        // generate drops this silently, so lint is the only thing that reports it.
        Assert.That(await RuleIds(Lint(), repo, AiFragment), Does.Contain("CFG-PATH-001"));
    }

    [Test]
    public async Task UnparseableFragmentIsReportedRatherThanThrown()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/ai", "version: 1\npaths: not-a-list\n");

        Assert.That(await RuleIds(Lint(), repo, AiFragment), Does.Contain("LNT-SCHEMA-001"));
    }

    [Test]
    public async Task DirectoriesUnderTheFragmentAreReportedWithTheirOwners()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var result = await Lint().Lint(repo.Root, [AiFragment], CancellationToken.None);
        var directories = result.Fragments.Single().Directories;

        Assert.That(
            directories.Select(d => d.Directory),
            Is.EqualTo(new[] { "sdk/ai/Azure.AI.Inference", "sdk/ai/Azure.AI.Projects" }));
        Assert.That(directories.All(d => d.Owners.Count > 0), Is.True);
    }

    [Test]
    public async Task DirectoryNoPathEntryClaimsIsReportedAsUnowned()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.CreateDirectory("sdk/ai/Azure.AI.Unclaimed");
        repo.WriteFragment("sdk/ai", """
            version: 1
            paths:
              - path: Azure.AI.Inference/
                owners: [test-user-02, test-user-24]
                pr-labels: [OpenAI]
            label-owners:
              - labels: [OpenAI]
                service-owners: [test-user-02, test-user-24]
            """);

        var result = await Lint().Lint(repo.Root, [AiFragment], CancellationToken.None);
        var unclaimed = result.Fragments.Single().Directories
            .Single(d => d.Directory == "sdk/ai/Azure.AI.Unclaimed");

        Assert.That(unclaimed.MatchedPath, Is.Null);
        Assert.That(unclaimed.Owners, Is.Empty);
    }

    private static string FragmentOwnedBy(string team) =>
        $"""
        version: 1
        paths:
          - path: .
            owners: [test-user-02, test-user-24, {team}]
            pr-labels: [OpenAI]
        label-owners:
          - labels: [OpenAI]
            service-owners: [test-user-02, test-user-24]
        """;

    private static string FragmentWithOwners(string owners) =>
        $"""
        version: 1
        paths:
          - path: .
            owners: [{owners}]
            pr-labels: [OpenAI]
        label-owners:
          - labels: [OpenAI]
            service-owners: [test-user-02, test-user-24]
        """;

    private static string FragmentWithServiceOwners(string serviceOwners) =>
        $"""
        version: 1
        paths:
          - path: .
            owners: [test-user-02, test-user-24]
            pr-labels: [OpenAI]
        label-owners:
          - labels: [OpenAI]
            service-owners: [{serviceOwners}]
        """;

    private sealed class FakeCommonLabelSource(IEnumerable<string> labels) : ICommonLabelSource
    {
        private readonly IReadOnlySet<string> labels = labels.ToHashSet(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlySet<string>> GetLabelsAsync(CancellationToken ct) => Task.FromResult(this.labels);
    }
}
