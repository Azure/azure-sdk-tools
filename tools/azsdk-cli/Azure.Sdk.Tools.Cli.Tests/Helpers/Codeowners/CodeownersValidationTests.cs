// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Codeowners;

/// <summary>
/// One test per CFG-* rule. Each starts from the spec's own repository and introduces exactly the
/// mistake the rule exists to catch, so a passing test also demonstrates the rule does not fire on
/// the known-good input.
/// </summary>
internal class CodeownersValidationTests
{
    /// <summary>A fragment with one path entry, for tests that only care about the path expression.</summary>
    private static string FragmentWithPath(string path) =>
        $"""
        version: 1
        paths:
          - path: "{path}"
            owners: [test-user-01, test-user-02]
            pr-labels: [Example]
        """;

    private static IEnumerable<string> Codes(OwnersTestRepo repo) =>
        repo.Render().Errors.Select(e => e.Code);

    [Test]
    public void ParentSegmentInAFragmentPathIsRejected()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai", FragmentWithPath("../ai/"));

        Assert.That(Codes(repo), Does.Contain("CFG-PATH-001"));
    }

    [Test]
    public void ParentSegmentIsRejectedEvenWhenItResolvesBackInsideTheFragment()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai", FragmentWithPath("Azure.AI.OpenAI/../"));

        Assert.That(Codes(repo), Does.Contain("CFG-PATH-001"));
    }

    [Test]
    public void RepoAbsoluteFragmentPathIsRejected()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai", FragmentWithPath("/sdk/openai/"));

        Assert.That(Codes(repo), Does.Contain("CFG-PATH-003"));
    }

    [TestCase("Azure.AI.OpenAI?/", TestName = "QuestionMarkIsNotAWildcard")]
    [TestCase("[abc]/", TestName = "CharacterRangesAreNotSupported")]
    [TestCase("!Azure.AI.OpenAI/", TestName = "NegationIsNotSupported")]
    [TestCase("Azure.AI.*", TestName = "GlobEndingInABareAsteriskDoesNotMatch")]
    [TestCase("**", TestName = "SubtreeGlobIsEquivalentToTheFragmentDirectory")]
    public void FragmentPathThatTheMatcherCannotEvaluateIsRejected(string path)
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai", FragmentWithPath(path));

        Assert.That(Codes(repo), Does.Contain("CFG-PATH-002"));
    }

    [Test]
    public void GlobsTheMatcherCanEvaluateAreAccepted()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai", FragmentWithPath("**/*.md"));

        Assert.That(Codes(repo), Is.Empty);
    }

    [Test]
    public void DirectoryPathMustBeAuthoredWithATrailingSlash()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.CreateDirectory("sdk/openai/Azure.AI.OpenAI");
        repo.WriteFragment("sdk/openai", FragmentWithPath("Azure.AI.OpenAI"));

        Assert.That(Codes(repo), Does.Contain("CFG-PATH-005"));
    }

    [Test]
    public void FilePathMustNotBeAuthoredWithATrailingSlash()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.Write("sdk/openai/ci.yml", "# pipeline");
        repo.WriteFragment("sdk/openai", FragmentWithPath("ci.yml/"));

        Assert.That(Codes(repo), Does.Contain("CFG-PATH-005"));
    }

    [Test]
    public void GlobsAreNotCheckedAgainstTheWorkingTree()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai", FragmentWithPath("Azure.AI.*/"));

        Assert.That(repo.Render().Errors, Is.Empty);
    }

    [Test]
    public void FragmentPathEntryWithoutPrLabelsIsRejected()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai",
            """
            version: 1
            paths:
              - path: .
                owners: [test-user-01, test-user-02]
            """);

        Assert.That(Codes(repo), Does.Contain("CFG-LBL-001"));
    }

    [Test]
    public void FragmentOutsideTheAllowedGlobsIsRejected()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai/Azure.AI.OpenAI", FragmentWithPath("."));

        Assert.That(Codes(repo), Does.Contain("CFG-LOC-001"));
    }

    [Test]
    public void FragmentTargetingAnUnknownSectionIsRejected()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai",
            """
            version: 1
            section: Not A Real Section
            paths:
              - path: .
                owners: [test-user-01, test-user-02]
                pr-labels: [Example]
            """);

        Assert.That(Codes(repo), Does.Contain("CFG-SEC-001"));
    }

    [Test]
    public void FragmentTargetingASectionThatDoesNotAcceptFragmentsIsRejected()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai",
            """
            version: 1
            section: EngSys
            paths:
              - path: .
                owners: [test-user-01, test-user-02]
                pr-labels: [Example]
            """);

        Assert.That(Codes(repo), Does.Contain("CFG-SEC-001"));
    }

    [Test]
    public void PathClaimedByBothTheConfigAndAFragmentIsRejected()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        // The config already declares /sdk/ai/Azure.AI.Projects.Agents/ as a static entry.
        repo.WriteFragment("sdk/ai", FragmentWithPath("Azure.AI.Projects.Agents/"));

        Assert.That(Codes(repo), Does.Contain("CFG-DUP-001"));
    }

    [Test]
    public void PathDeclaredTwiceInOneConfigSectionIsRejected()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteConfig(OwnersTestRepo.ReadAsset("owners.config.yaml").Replace(
            SdkCatchAllSection,
            SdkCatchAllSection + """

                      - path: /sdk/
                        owners: [test-user-13]
                """));

        Assert.That(Codes(repo), Does.Contain("CFG-DUP-003"));
    }

    [Test]
    public void SamePathInTwoDifferentConfigSectionsIsAllowed()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        // The owners config is seeded from a CODEOWNERS file that already works, so cross-section
        // repetition is a migration artifact rather than a defect.
        repo.WriteConfig(OwnersTestRepo.ReadAsset("owners.config.yaml").Replace(
            SdkCatchAllSection,
            SdkCatchAllSection + """


                  - name: Duplicated
                    paths:
                      - path: /sdk/
                        owners: [test-user-13]
                """));

        Assert.That(repo.Render().Errors, Is.Empty);
    }

    private const string SdkCatchAllSection =
        """
              - path: /sdk/
                owners: [test-user-13, Azure/azure-sdk-write-net-core]
        """;

    [Test]
    public void PathDeclaredTwiceInAFragmentIsRejected()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai",
            """
            version: 1
            paths:
              - path: Azure.AI.OpenAI/
                owners: [test-user-01, test-user-02]
                pr-labels: [Example]
              - path: Azure.AI.OpenAI/
                owners: [test-user-03, test-user-04]
                pr-labels: [Example]
            """);

        Assert.That(Codes(repo), Does.Contain("CFG-DUP-002"));
    }

    [Test]
    public void LabelSetDeclaredBothStaticallyAndByAFragmentIsRejected()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai",
            """
            version: 1
            label-owners:
              - labels: [Azure.Core]
                service-owners: [test-user-01]
            """);

        Assert.That(Codes(repo), Does.Contain("CFG-DUP-004"));
    }

    [Test]
    public void PathsDifferingOnlyByCaseAreDistinct()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai",
            """
            version: 1
            paths:
              - path: Tables/
                owners: [test-user-01, test-user-02]
                pr-labels: [Example]
              - path: tables/
                owners: [test-user-03, test-user-04]
                pr-labels: [Example]
            """);

        // GitHub matches CODEOWNERS paths case-sensitively, so these are two different expressions.
        Assert.That(repo.Render().Errors, Is.Empty);
    }

    [Test]
    public void EveryErrorIsReportedNotJustTheFirst()
    {
        using var repo = OwnersTestRepo.FromSpecAssets();
        repo.WriteFragment("sdk/openai",
            """
            version: 1
            paths:
              - path: ../ai/
                owners: [test-user-01]
                pr-labels: [Example]
              - path: /sdk/openai/
                owners: [test-user-02]
            """);

        Assert.That(Codes(repo), Is.EquivalentTo(new[] { "CFG-PATH-001", "CFG-LBL-001", "CFG-PATH-003" }));
    }
}
