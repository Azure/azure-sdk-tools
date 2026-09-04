// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Codeowners;

/// <summary>
/// Covers the step that only exists in the builder: deciding what does not make it into the rendered
/// file. Generation never fails, so everything this drops has to be reported instead.
/// </summary>
internal class CodeownersModelBuilderTests
{
    private static readonly string[] ValidOwners =
    [
        "test-user-02", "test-user-07", "test-user-09", "test-user-13",
        "test-user-18", "test-user-22", "test-user-23", "test-user-24",
    ];

    private static Task<CodeownersModel> Build(
        OwnersTestRepo repo,
        IOwnerValidator validator,
        bool omitFallbackSections = false) =>
        new CodeownersModelBuilder(validator).Build(repo.Root, omitFallbackSections, CancellationToken.None);

    [Test]
    public async Task InvalidFragmentOwnerIsDroppedAndReported()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var model = await Build(repo, OwnerValidatorFake.Rejecting("test-user-09"));

        Assert.That(model.Content, Does.Not.Contain("test-user-09"));
        Assert.That(model.Dropped.Select(d => d.Subject), Does.Contain("test-user-09"));
    }

    [Test]
    public async Task ConfigOwnersAreRenderedWithoutValidation()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        // Static config entries carry over from a CODEOWNERS file GitHub already enforces, so they
        // render exactly as written even when the cache would reject the owner.
        var model = await Build(repo, OwnerValidatorFake.Rejecting("test-user-01"));

        Assert.That(model.Content, Does.Contain("test-user-01"));
        Assert.That(model.Dropped.Select(d => d.Subject), Does.Not.Contain("test-user-01"));
    }

    [Test]
    public async Task PathLeftWithNoOwnersIsNotRenderedAsAnOwnerlessEntry()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/ai", """
            version: 1
            paths:
              - path: Azure.AI.Inference/
                owners: [departed-one, departed-two]
                pr-labels: [AI Model Inference]
            label-owners:
              - labels: [AI Model Inference]
                service-owners: [test-user-07, test-user-09]
            """);

        var model = await Build(repo, OwnerValidatorFake.Rejecting("departed-one", "departed-two"));

        // An ownerless path in CODEOWNERS means "nobody owns this", which would stop the path from
        // falling through to the repository backstop.
        Assert.That(model.Content, Does.Not.Contain("/sdk/ai/Azure.AI.Inference/"));
        Assert.That(model.Dropped.Select(d => d.RuleId), Does.Contain("GEN-DROP-001"));
    }

    [Test]
    public async Task LabelBlockLeftWithNoOwnersIsDropped()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/ai", """
            version: 1
            paths:
              - path: Azure.AI.Inference/
                owners: [test-user-07, test-user-09]
                pr-labels: [AI Model Inference]
            label-owners:
              - labels: [AI Model Inference]
                service-owners: [departed-one]
            """);

        var model = await Build(repo, OwnerValidatorFake.Rejecting("departed-one"));

        Assert.That(model.Dropped.Select(d => d.RuleId), Does.Contain("GEN-DROP-002"));
    }

    [Test]
    public async Task PathEscapingTheFragmentSubtreeIsDroppedRatherThanFailing()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/ai", """
            version: 1
            paths:
              - path: ../openai/
                owners: [test-user-07, test-user-09]
                pr-labels: [AI Model Inference]
            label-owners:
              - labels: [AI Model Inference]
                service-owners: [test-user-07, test-user-09]
            """);

        var model = await Build(repo, OwnerValidatorFake.AcceptAll());

        Assert.That(model.Content, Is.Not.Empty);
        Assert.That(model.Dropped.Select(d => d.RuleId), Does.Contain("CFG-PATH-001"));
    }

    [Test]
    public async Task ValidRepositoryDropsNothing()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var model = await Build(repo, OwnerValidatorFake.Create(ValidOwners));

        Assert.That(
            model.Dropped.Select(d => $"{d.RuleId} {d.Subject} {d.Reason}"),
            Is.Empty);
    }

    [Test]
    public async Task FallbackSectionsAreOmittedOnRequest()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var full = await Build(repo, OwnerValidatorFake.AcceptAll());
        var trimmed = await Build(repo, OwnerValidatorFake.AcceptAll(), omitFallbackSections: true);

        Assert.That(trimmed.Entries.Count, Is.LessThan(full.Entries.Count));
        Assert.That(full.Content, Does.Contain("/sdk/ "));
        Assert.That(trimmed.Content, Does.Not.Contain("/sdk/ "));
    }
}
