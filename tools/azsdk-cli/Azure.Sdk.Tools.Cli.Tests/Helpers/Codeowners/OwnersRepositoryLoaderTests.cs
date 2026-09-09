// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Codeowners;

/// <summary>
/// Covers fragment discovery and the governing-fragment lookup, which every command that has to name
/// a file for an author to edit resolves through.
/// </summary>
internal class OwnersRepositoryLoaderTests
{
    private const string Fragment = """
        version: 1
        paths:
          - path: Azure.AI.Inference/
            owners: [test-user-07, test-user-09]
            pr-labels: [AI Model Inference]
        label-owners:
          - labels: [AI Model Inference]
            service-owners: [test-user-07, test-user-09]
        """;

    private static OwnersRepository Load(OwnersTestRepo repo) =>
        OwnersRepositoryLoader.Load(repo.Root, []);

    [Test]
    public void FragmentsAreDiscoveredUnderTheConfiguredFileName()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        Assert.That(
            Load(repo).Fragments.Select(f => f.FilePath),
            Is.EqualTo(new[] { "sdk/ai/owners.yaml", "sdk/openai/owners.yaml" }));
    }

    /// <summary>
    /// The config names one spelling, so a file under any other name is not a fragment at all. It is
    /// not read and not reported: declaring the name is what makes a file ownership, and this one
    /// does not carry it.
    /// </summary>
    [Test]
    public void SpellingTheConfigDoesNotAdmitIsIgnored()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.Write("sdk/ai/owners.yml", Fragment);

        Assert.That(
            Load(repo).Fragments.Select(f => f.FilePath),
            Is.EqualTo(new[] { "sdk/ai/owners.yaml", "sdk/openai/owners.yaml" }));
    }

    /// <summary>
    /// The right name in the wrong place stays a reported CFG-LOC-001: it is a location policy
    /// question, and generate is deliberately non-blocking about those.
    /// </summary>
    [Test]
    public void FragmentOutsideTheAllowedGlobsIsReportedRatherThanFatal()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.Write("eng/owners.yaml", Fragment);

        var errors = new List<OwnersValidationError>();
        var repository = OwnersRepositoryLoader.Load(repo.Root, errors);

        Assert.That(errors.Select(e => e.Code), Does.Contain("CFG-LOC-001"));
        Assert.That(repository.Fragments.Select(f => f.FilePath), Does.Not.Contain("eng/owners.yaml"));
    }

    [Test]
    public void GoverningFragmentIsTheNearestOneAtOrAboveTheDirectory()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        var repository = Load(repo);

        Assert.Multiple(() =>
        {
            Assert.That(repository.FindGoverningFragment("sdk/ai/Azure.AI.Inference")?.FilePath,
                Is.EqualTo("sdk/ai/owners.yaml"));
            Assert.That(repository.FindGoverningFragment("sdk/ai")?.FilePath,
                Is.EqualTo("sdk/ai/owners.yaml"));
            Assert.That(repository.FindGoverningFragment("/sdk/openai/Azure.AI.OpenAI/")?.FilePath,
                Is.EqualTo("sdk/openai/owners.yaml"));
        });
    }

    [Test]
    public void GoverningFragmentIsNullWhenNothingCoversTheDirectory()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        Assert.That(Load(repo).FindGoverningFragment("sdk/unmigrated/Azure.Unmigrated"), Is.Null);
    }

    /// <summary>
    /// A sibling whose name merely starts with the same characters is not underneath it, so
    /// <c>sdk/ai-extras</c> must not inherit <c>sdk/ai</c>'s fragment.
    /// </summary>
    [Test]
    public void GoverningFragmentMatchesWholePathSegments()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        Assert.That(Load(repo).FindGoverningFragment("sdk/ai-extras/Azure.AI.Extras"), Is.Null);
    }

    [Test]
    public void MissingConfigIsFatal()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        File.Delete(Path.Combine(repo.Root, ".github", "owners.config.yaml"));

        var ex = Assert.Throws<OwnersYamlException>(() => Load(repo));

        Assert.That(ex!.Message, Does.Contain("owners.config.yaml not found"));
    }

    [Test]
    public void TryLoadConfigReturnsNullWhenTheRepositoryHasNoConfig()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        File.Delete(Path.Combine(repo.Root, ".github", "owners.config.yaml"));

        Assert.That(OwnersRepositoryLoader.TryLoadConfig(repo.Root), Is.Null);
    }
}
