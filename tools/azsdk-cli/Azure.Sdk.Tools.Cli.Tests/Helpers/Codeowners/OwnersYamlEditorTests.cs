// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Codeowners;

/// <summary>
/// The editor rewrites the whole file from the parsed model, so these tests assert on the model the
/// result loads back as rather than on its text. Formatting and comments are deliberately not
/// preserved and are not worth pinning.
/// </summary>
internal class OwnersYamlEditorTests
{
    private const string Path = "sdk/ai/owners.yaml";

    /// <summary>Removes the alias and reloads the result, so a test can assert on what survived.</summary>
    private static OwnersFragment RemoveAndReload(string yaml, string alias)
    {
        var edited = OwnersYamlEditor.RemoveOwner(yaml, Path, alias);
        Assert.That(edited, Is.Not.Null, "Expected the removal to be applied.");

        return OwnersYamlLoader.LoadFragment(edited!, Path);
    }

    [TestCase("test-user-02", TestName = "RemovesAPlainOwner")]
    [TestCase("\"test-user-02\"", TestName = "RemovesADoubleQuotedOwner")]
    [TestCase("'test-user-02'", TestName = "RemovesASingleQuotedOwner")]
    [TestCase("\"@test-user-02\"", TestName = "RemovesAnOwnerWrittenWithAnAtSign")]
    public void RemovesAnOwnerHoweverItIsSpelled(string spelling)
    {
        var fragment = RemoveAndReload(
            $"""
            version: 1
            paths:
              - path: .
                owners: [test-user-01, {spelling}, test-user-03]
                pr-labels: [Example]
            """, "test-user-02");

        Assert.That(fragment.Paths[0].Owners, Is.EqualTo(new[] { "test-user-01", "test-user-03" }));
    }

    [Test]
    public void RemovesFromEveryOwnerListInTheFile()
    {
        var fragment = RemoveAndReload(
            """
            version: 1
            paths:
              - path: .
                owners: [test-user-01, test-user-02]
                pr-labels: [Example]
            label-owners:
              - labels: [Example]
                service-owners: [test-user-01, test-user-03]
                azure-sdk-owners: [test-user-01, test-user-04]
            """, "test-user-01");

        Assert.That(fragment.Paths[0].Owners, Is.EqualTo(new[] { "test-user-02" }));
        Assert.That(fragment.LabelOwners[0].ServiceOwners, Is.EqualTo(new[] { "test-user-03" }));
        Assert.That(fragment.LabelOwners[0].AzureSdkOwners, Is.EqualTo(new[] { "test-user-04" }));
    }

    [Test]
    public void KeepsEverythingTheRemovalDoesNotTouch()
    {
        var fragment = RemoveAndReload(
            """
            version: 1
            section: Client Libraries
            paths:
              - path: .
                owners: [test-user-01, test-user-02]
                pr-labels: [Example, Other]
              - path: Sub/
                owners: [test-user-03]
                pr-labels: [Example]
                section: Service Attention
            label-owners:
              - labels: [Example]
                service-owners: [test-user-03]
            """, "test-user-01");

        Assert.That(fragment.Version, Is.EqualTo(1));
        Assert.That(fragment.Section, Is.EqualTo("Client Libraries"));
        Assert.That(fragment.Paths[0].PrLabels, Is.EqualTo(new[] { "Example", "Other" }));
        Assert.That(fragment.Paths[1].Path, Is.EqualTo("Sub/"));
        Assert.That(fragment.Paths[1].Owners, Is.EqualTo(new[] { "test-user-03" }));
        Assert.That(fragment.Paths[1].Section, Is.EqualTo("Service Attention"));
        Assert.That(fragment.LabelOwners[0].Labels, Is.EqualTo(new[] { "Example" }));
    }

    [Test]
    public void LeavesAPathWithNoOwnersLeftInPlace()
    {
        // An empty owner list is legal: rendering drops the path, which is how a service says
        // "nobody owns this yet" rather than an error to fix here.
        var fragment = RemoveAndReload(
            """
            version: 1
            paths:
              - path: .
                owners: [test-user-01]
                pr-labels: [Example]
            """, "test-user-01");

        Assert.That(fragment.Paths[0].Path, Is.EqualTo("."));
        Assert.That(fragment.Paths[0].Owners, Is.Empty);
    }

    [Test]
    public void LeavesALabelThatLooksLikeTheAliasAlone()
    {
        var fragment = RemoveAndReload(
            """
            version: 1
            paths:
              - path: .
                owners: [test-user-01, test-user-02]
                pr-labels: [test-user-02]
            """, "test-user-02");

        Assert.That(fragment.Paths[0].PrLabels, Is.EqualTo(new[] { "test-user-02" }));
        Assert.That(fragment.Paths[0].Owners, Is.EqualTo(new[] { "test-user-01" }));
    }

    [Test]
    public void LeavesAnAliasThatIsAPrefixOfAnotherAlone()
    {
        var fragment = RemoveAndReload(
            """
            version: 1
            paths:
              - path: .
                owners: [test-user-1, test-user-10]
                pr-labels: [Example]
            """, "test-user-1");

        Assert.That(fragment.Paths[0].Owners, Is.EqualTo(new[] { "test-user-10" }));
    }

    [Test]
    public void ReturnsNullWhenTheAliasIsNotPresent()
    {
        var yaml =
            """
            version: 1
            paths:
              - path: .
                owners: [test-user-01]
                pr-labels: [Example]
            """;

        Assert.That(OwnersYamlEditor.RemoveOwner(yaml, Path, "test-user-99"), Is.Null);
    }

    [Test]
    public void ReturnsNullWhenTheFileDoesNotParse()
    {
        Assert.That(OwnersYamlEditor.RemoveOwner("version: 1\npaths: [", Path, "test-user-01"), Is.Null);
    }

    [Test]
    public void WritesUnixLineEndings()
    {
        var edited = OwnersYamlEditor.RemoveOwner(
            """
            version: 1
            paths:
              - path: .
                owners: [test-user-01, test-user-02]
                pr-labels: [Example]
            """, Path, "test-user-02");

        Assert.That(edited, Does.Not.Contain("\r"));
    }
}
