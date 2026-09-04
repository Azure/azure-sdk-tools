// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using Azure.Sdk.Tools.CodeownersUtils.Caches;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using Microsoft.Extensions.Logging.Abstractions;
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

    private static CodeownersLintHelper Lint(
        IEnumerable<string>? owners = null,
        IEnumerable<string>? labels = null,
        IDictionary<string, List<string>>? teams = null) =>
        new(
            OwnerValidatorFake.Create(owners ?? ValidOwners, teams),
            new FakeCommonLabelSource(labels ?? KnownLabels),
            NullLogger<CodeownersLintHelper>.Instance);

    private static IEnumerable<string> RuleIds(CodeownersLintResponse response) =>
        response.Violations.Select(v => v.RuleId);

    [Test]
    public async Task ValidRepositoryReportsNothing()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var result = await Lint().RunLint(repo.Root, CancellationToken.None);

        Assert.That(result.Violations.Select(v => $"{v.RuleId} {v.Description}"), Is.Empty);
    }

    [Test]
    public async Task OwnerMissingFromTheMembershipCacheIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var result = await Lint(owners: ValidOwners.Except(["test-user-09"]))
            .RunLint(repo.Root, CancellationToken.None);

        Assert.That(RuleIds(result), Does.Contain("LNT-OWN-001"));
        Assert.That(result.Violations.First(v => v.RuleId == "LNT-OWN-001").Description, Does.Contain("test-user-09"));
    }

    [Test]
    public async Task PrivateOrgMembershipIsReportedAsSelfServiceable()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var result = await Lint(owners: ValidOwners)
            .RunLint(repo.Root, CancellationToken.None);

        Assert.That(result.Violations, Is.Empty);

        var hidden = new CodeownersLintHelper(
            OwnerValidatorFake.Create(ValidOwners, orgVisible: ValidOwners.Except(["test-user-09"])),
            new FakeCommonLabelSource(KnownLabels),
            NullLogger<CodeownersLintHelper>.Instance);

        var hiddenResult = await hidden.RunLint(repo.Root, CancellationToken.None);

        Assert.That(
            hiddenResult.Violations.First(v => v.RuleId == "LNT-OWN-001").Detail,
            Does.Contain("orgs/Azure/people"));
    }

    [Test]
    public void EmptyMembershipCacheFailsClosed()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Lint(owners: []).RunLint(repo.Root, CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("empty cache"));
    }

    [Test]
    public async Task MalformedTeamAliasIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai", FragmentOwnedBy("someorg/some-team"));

        var result = await Lint().RunLint(repo.Root, CancellationToken.None);

        Assert.That(RuleIds(result), Does.Contain("LNT-OWN-002"));
    }

    [Test]
    public async Task TeamOutsideAzureSdkWriteIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai", FragmentOwnedBy("Azure/not-a-descendant"));

        var result = await Lint().RunLint(repo.Root, CancellationToken.None);

        Assert.That(RuleIds(result), Does.Contain("LNT-OWN-002"));
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

        var result = await Lint(teams: teams).RunLint(repo.Root, CancellationToken.None);

        Assert.That(RuleIds(result), Does.Not.Contain("LNT-OWN-002"));
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

        var result = await Lint().RunLint(repo.Root, CancellationToken.None);

        Assert.That(RuleIds(result), Does.Contain("LNT-OWN-003"));
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

        var result = await Lint().RunLint(repo.Root, CancellationToken.None);

        Assert.That(RuleIds(result), Does.Not.Contain("LNT-OWN-004"));
    }

    [Test]
    public async Task LabelOutsideTheCommonSetIsReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var result = await Lint(labels: KnownLabels.Except(["OpenAI"]))
            .RunLint(repo.Root, CancellationToken.None);

        Assert.That(RuleIds(result), Does.Contain("LNT-LBL-001"));
        Assert.That(result.Violations.First(v => v.RuleId == "LNT-LBL-001").Description, Does.Contain("OpenAI"));
    }

    [Test]
    public async Task LabelsAreMatchedWithoutRegardToCase()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var result = await Lint(labels: ["ai model inference", "AI PROJECTS", "openai"])
            .RunLint(repo.Root, CancellationToken.None);

        Assert.That(RuleIds(result), Does.Not.Contain("LNT-LBL-001"));
    }

    [Test]
    public async Task ConfigLabelsAreNotLinted()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        // The base config names many labels the common set does not carry; only fragments are linted.
        var result = await Lint().RunLint(repo.Root, CancellationToken.None);

        Assert.That(RuleIds(result), Does.Not.Contain("LNT-LBL-001"));
    }

    [Test]
    public async Task ValidationErrorsAreReportedInsteadOfLintRules()
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

        var result = await Lint().RunLint(repo.Root, CancellationToken.None);

        Assert.That(RuleIds(result).ToArray(), Is.EqualTo(new[] { "CFG-PATH-001" }));
    }

    private static string FragmentOwnedBy(string team) =>
        $"""
        version: 1
        paths:
          - path: .
            owners: [test-user-02, test-user-24, {team}]
            pr-labels: [OpenAI]
        """;

    private sealed class FakeCommonLabelSource(IEnumerable<string> labels) : ICommonLabelSource
    {
        private readonly IReadOnlySet<string> labels = labels.ToHashSet(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlySet<string>> GetLabelsAsync(CancellationToken ct) => Task.FromResult(this.labels);
    }
}
