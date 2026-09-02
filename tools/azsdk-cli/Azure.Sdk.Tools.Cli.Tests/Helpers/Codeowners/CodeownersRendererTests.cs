// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Codeowners;

/// <summary>
/// The renderer's contract, pinned against the spec's worked example and then narrowed to the
/// individual rules that example cannot exercise.
/// </summary>
internal class CodeownersRendererTests
{
    /// <summary>
    /// Rendered content always uses '\n', so expectations are built the same way rather than with
    /// multi-line literals whose line endings depend on how this file was checked out.
    /// </summary>
    private static string Block(params string[] lines) => string.Join("\n", lines);

    [Test]
    public void SpecExampleRendersTheSpecOutput()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        var result = repo.Render();

        Assert.That(result.Errors, Is.Empty);
        Assert.That(result.Content, Is.EqualTo(OwnersTestRepo.ReadAsset("CODEOWNERS.rendered")));
    }

    [Test]
    public void SharedLabelSetUnionsOwnersAndRecordsBothSources()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        Assert.That(repo.Render().Content, Does.Contain(Block(
            "# Sources: sdk/ai/owners.yaml, sdk/openai/owners.yaml",
            "# AzureSdkOwners: @test-user-07",
            "# ServiceLabel: %AI Projects",
            "# ServiceOwners: @test-user-07 @test-user-18 @test-user-23 @test-user-02 @test-user-24")));
    }

    [Test]
    public void SingleContributorLabelBlockStillRecordsItsSource()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        Assert.That(repo.Render().Content, Does.Contain(Block(
            "# Sources: sdk/openai/owners.yaml",
            "# AzureSdkOwners: @test-user-13",
            "# ServiceLabel: %OpenAI")));
    }

    [Test]
    public void StaticLabelBlocksAreNotAttributedToAFragment()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        var content = repo.Render().Content;

        var serviceLabel = content.IndexOf("# ServiceLabel: %Azure.Core", StringComparison.Ordinal);
        var precedingSources = content.LastIndexOf("# Sources:", serviceLabel, StringComparison.Ordinal);
        var precedingBlank = content.LastIndexOf("\n\n", serviceLabel, StringComparison.Ordinal);

        Assert.That(precedingSources, Is.LessThan(precedingBlank), "static blocks must not be given provenance");
    }

    [Test]
    public void PathEntriesCarryNoSourcesComment()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();

        Assert.That(repo.Render().Content, Does.Contain(Block(
            "# PRLabel: %AI Model Inference",
            "/sdk/ai/Azure.AI.Inference/    @test-user-07 @test-user-09 @test-user-23")));
    }

    [Test]
    public void FragmentEntriesLandInTheConfiguredDefaultSection()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        var content = repo.Render().Content;

        var clientLibraries = content.IndexOf("# Client Libraries", StringComparison.Ordinal);
        var managementLibraries = content.IndexOf("# Management Libraries", StringComparison.Ordinal);
        var fragmentEntry = content.IndexOf("/sdk/openai/", StringComparison.Ordinal);

        Assert.That(fragmentEntry, Is.GreaterThan(clientLibraries).And.LessThan(managementLibraries));
    }
}
