// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using Azure.Sdk.Tools.CodeownersUtils.Caches;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Codeowners;

internal class CodeownersAuditHelperTests
{
    private const string Repo = "Azure/azure-sdk-for-net";

    /// <summary>Owners and labels the spec's example fragments use. Anything absent is a violation.</summary>
    private static readonly string[] ValidOwners =
    [
        "test-user-02", "test-user-07", "test-user-09", "test-user-13",
        "test-user-18", "test-user-22", "test-user-23", "test-user-24",
    ];

    private static readonly string[] KnownLabels = ["AI Model Inference", "AI Projects", "OpenAI"];

    private static CodeownersAuditHelper Audit(
        IEnumerable<string>? owners = null,
        IEnumerable<string>? labels = null,
        IDictionary<string, List<string>>? teams = null)
    {
        var members = (owners ?? ValidOwners).ToList();

        var teamUsers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Azure/azure-sdk-write"] = members,
        };

        foreach (var team in teams ?? new Dictionary<string, List<string>>())
        {
            teamUsers[team.Key] = team.Value;
        }

        return new CodeownersAuditHelper(
            new FakeTeamUserCache(teamUsers),
            new RepoLabelCache
            {
                RepoLabelDict = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    [Repo] = new HashSet<string>(labels ?? KnownLabels, StringComparer.OrdinalIgnoreCase),
                },
            },
            new UserOrgVisibilityCache
            {
                UserOrgVisibilityDict = members.ToDictionary(o => o, _ => true, StringComparer.OrdinalIgnoreCase),
            },
            new FakeCacheValidator(),
            NullLogger<CodeownersAuditHelper>.Instance);
    }

    private static IEnumerable<string> RuleIds(CodeownersAuditResponse response) =>
        response.Violations.Select(v => v.RuleId);

    [Test]
    public async Task ValidRepositoryReportsNothingBeyondOrdering()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var result = await Audit().RunAudit(repo.Root, Repo, fix: false, force: false, CancellationToken.None);

        Assert.That(
            result.Violations.Where(v => v.RuleId != "AUD-ORD-001").Select(v => $"{v.RuleId} {v.Description}"),
            Is.Empty);
    }

    [Test]
    public async Task SortedSectionSurfacesTheInversionsItCreates()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var result = await Audit().RunAudit(repo.Root, Repo, fix: false, force: false, CancellationToken.None);

        // Both packages sort above /sdk/ai/ because they carry no PR label, so the service-wide
        // entry below them takes their paths.
        Assert.That(
            result.Violations.Where(v => v.RuleId == "AUD-ORD-001").Select(v => v.Description),
            Has.Exactly(2).Items
                .And.All.Contains("'/sdk/ai/', which contains it"));
    }

    [Test]
    public async Task OrderingIsNotComparedAcrossSections()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var result = await Audit().RunAudit(repo.Root, Repo, fix: false, force: false, CancellationToken.None);

        // /sdk/ is a repo-wide catch-all in an earlier section; every fragment entry is meant to
        // render below it and win.
        Assert.That(result.Violations.Select(v => v.Description), Has.None.Contains("'/sdk/', which contains it"));
    }

    [Test]
    public async Task OwnerMissingFromTheMembershipCacheIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var result = await Audit(owners: ValidOwners.Except(["test-user-09"]))
            .RunAudit(repo.Root, Repo, fix: false, force: false, CancellationToken.None);

        Assert.That(RuleIds(result), Does.Contain("AUD-OWN-001"));
        Assert.That(result.Violations.First(v => v.RuleId == "AUD-OWN-001").Description, Does.Contain("test-user-09"));
    }

    [Test]
    public void EmptyMembershipCacheFailsClosed()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Audit(owners: []).RunAudit(repo.Root, Repo, fix: false, force: false, CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("empty cache"));
    }

    [Test]
    public async Task MalformedTeamAliasIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai", FragmentOwnedBy("someorg/some-team"));

        var result = await Audit().RunAudit(repo.Root, Repo, fix: false, force: false, CancellationToken.None);

        Assert.That(RuleIds(result), Does.Contain("AUD-OWN-002"));
    }

    [Test]
    public async Task TeamOutsideAzureSdkWriteIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai", FragmentOwnedBy("Azure/not-a-descendant"));

        var result = await Audit().RunAudit(repo.Root, Repo, fix: false, force: false, CancellationToken.None);

        Assert.That(RuleIds(result), Does.Contain("AUD-OWN-003"));
    }

    [Test]
    public async Task TeamInsideAzureSdkWriteIsAccepted()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai", FragmentOwnedBy("Azure/azure-sdk-write-net-core"));

        var teams = new Dictionary<string, List<string>>
        {
            ["Azure/azure-sdk-write-net-core"] = ["test-user-13"],
        };

        var result = await Audit(teams: teams).RunAudit(repo.Root, Repo, fix: false, force: false, CancellationToken.None);

        Assert.That(RuleIds(result), Does.Not.Contain("AUD-OWN-002").And.Not.Contain("AUD-OWN-003"));
    }

    [Test]
    public async Task PathWithTooFewIndividualOwnersIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai",
            """
            version: 1
            paths:
              - path: .
                owners: [test-user-02]
                pr-labels: [OpenAI]
            label-owners:
              - labels: [OpenAI]
                service-owners: [test-user-02, test-user-24]
            """);

        var result = await Audit().RunAudit(repo.Root, Repo, fix: false, force: false, CancellationToken.None);

        Assert.That(RuleIds(result), Does.Contain("AUD-OWN-004"));
    }

    [Test]
    public async Task LabelOwnerCountIsMeasuredAfterTheUnion()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        // On its own this block is below the minimum; unioned with sdk/ai's it is not.
        repo.WriteFragment("sdk/openai",
            """
            version: 1
            paths:
              - path: .
                owners: [test-user-02, test-user-24]
                pr-labels: [AI Projects]
            label-owners:
              - labels: [AI Projects]
                service-owners: [test-user-02]
            """);

        var result = await Audit().RunAudit(repo.Root, Repo, fix: false, force: false, CancellationToken.None);

        Assert.That(RuleIds(result), Does.Not.Contain("AUD-OWN-005"));
    }

    [Test]
    public async Task LabelMissingFromTheRepoIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var result = await Audit(labels: KnownLabels.Except(["OpenAI"]))
            .RunAudit(repo.Root, Repo, fix: false, force: false, CancellationToken.None);

        Assert.That(RuleIds(result), Does.Contain("AUD-LBL-001"));
    }

    [Test]
    public async Task ServiceAttentionAsAPrLabelIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai",
            """
            version: 1
            paths:
              - path: .
                owners: [test-user-02, test-user-24]
                pr-labels: [Service Attention]
            """);

        var result = await Audit(labels: [.. KnownLabels, "Service Attention"])
            .RunAudit(repo.Root, Repo, fix: false, force: false, CancellationToken.None);

        Assert.That(RuleIds(result), Does.Contain("AUD-LBL-002"));
    }

    [Test]
    public async Task PathThatMatchesNothingOnDiskIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai",
            """
            version: 1
            paths:
              - path: .
                owners: [test-user-02, test-user-24]
                pr-labels: [OpenAI]
              - path: Azure.AI.Removed/
                owners: [test-user-02, test-user-24]
                pr-labels: [OpenAI]
            """);

        var result = await Audit().RunAudit(repo.Root, Repo, fix: false, force: false, CancellationToken.None);

        var violation = result.Violations.Single(v => v.RuleId == "AUD-PATH-001");
        Assert.That(violation.Description, Does.Contain("/sdk/openai/Azure.AI.Removed/"));
    }

    [Test]
    public async Task OwnershipInversionIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.CreateDirectory("sdk/openai/Azure.AI.OpenAI");
        // "AI Projects" sorts before "OpenAI", so the package entry renders above the service-wide
        // entry that contains it and is overridden by it.
        repo.WriteFragment("sdk/openai",
            """
            version: 1
            paths:
              - path: .
                owners: [test-user-02, test-user-24]
                pr-labels: [OpenAI]
              - path: Azure.AI.OpenAI/
                owners: [test-user-02, test-user-13]
                pr-labels: [AI Projects]
            """);

        var result = await Audit().RunAudit(repo.Root, Repo, fix: false, force: false, CancellationToken.None);

        Assert.That(
            result.Violations.Where(v => v.RuleId == "AUD-ORD-001").Select(v => v.Description),
            Has.Some.Contains("'/sdk/openai/Azure.AI.OpenAI/' renders before '/sdk/openai/'"));
    }

    [Test]
    public async Task FixRemovesTheInvalidOwnerFromTheFragment()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var result = await Audit(owners: ValidOwners.Except(["test-user-09"]))
            .RunAudit(repo.Root, Repo, fix: true, force: false, CancellationToken.None);

        Assert.That(result.FixesFailed, Is.Zero);
        Assert.That(result.FixesApplied, Is.GreaterThan(0));
        Assert.That(repo.Read("sdk/ai/owners.yaml"), Does.Not.Contain("test-user-09"));
    }

    [Test]
    public async Task FixLeavesTheRenderedFileForGenerateToUpdate()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.Write(".github/CODEOWNERS", "# stale");

        await Audit(owners: ValidOwners.Except(["test-user-09"]))
            .RunAudit(repo.Root, Repo, fix: true, force: false, CancellationToken.None);

        Assert.That(repo.Read(".github/CODEOWNERS"), Is.EqualTo("# stale"));
    }

    [Test]
    public void FixStopsWhenTooManyOwnersWouldBeRemoved()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        // Only two of the fragments' owners survive, so the run would strip the rest.
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Audit(owners: ["test-user-02", "test-user-07"])
                .RunAudit(repo.Root, Repo, fix: true, force: false, CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("--force"));
    }

    [Test]
    public async Task ValidationErrorsAreReportedInsteadOfAuditRules()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai",
            """
            version: 1
            paths:
              - path: ../ai/
                owners: [test-user-02]
                pr-labels: [OpenAI]
            """);

        var result = await Audit().RunAudit(repo.Root, Repo, fix: false, force: false, CancellationToken.None);

        Assert.That(RuleIds(result), Is.EqualTo(new[] { "CFG-PATH-001" }));
    }

    private static string FragmentOwnedBy(string team) =>
        $"""
        version: 1
        paths:
          - path: .
            owners: [test-user-02, test-user-24, {team}]
            pr-labels: [OpenAI]
        """;

    private sealed class FakeTeamUserCache(Dictionary<string, List<string>> teams) : ITeamUserCache
    {
        public Dictionary<string, List<string>> TeamUserDict { get; set; } = teams;

        public List<string> GetUsersForTeam(string teamName) =>
            TeamUserDict.TryGetValue(teamName, out var users) ? users : [];
    }

    private sealed class FakeCacheValidator : ICacheValidator
    {
        public Task ThrowIfCacheOlderThan(string cacheSource, DateTime minimumLastModifiedUtc, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
