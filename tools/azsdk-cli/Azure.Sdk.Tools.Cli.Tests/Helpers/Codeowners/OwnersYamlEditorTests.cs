// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Codeowners;

/// <summary>
/// The editor is textual, so these tests are about what it leaves alone as much as what it removes.
/// </summary>
internal class OwnersYamlEditorTests
{
    private const string Path = "sdk/ai/owners.yaml";

    private static string Remove(string yaml, string alias) => OwnersYamlEditor.RemoveOwner(yaml, Path, alias)!;

    [Test]
    public void RemovesFromTheMiddleOfAFlowSequence()
    {
        var result = Remove(
            """
            version: 1
            paths:
              - path: .
                owners: [test-user-01, test-user-02, test-user-03]
                pr-labels: [Example]
            """, "test-user-02");

        Assert.That(result, Does.Contain("owners: [test-user-01, test-user-03]"));
    }

    [Test]
    public void RemovesTheLastMemberOfAFlowSequence()
    {
        var result = Remove(
            """
            version: 1
            paths:
              - path: .
                owners: [test-user-01, test-user-02]
                pr-labels: [Example]
            """, "test-user-02");

        Assert.That(result, Does.Contain("owners: [test-user-01]"));
    }

    [Test]
    public void RemovesABlockSequenceItemEntirely()
    {
        var result = Remove(
            """
            version: 1
            paths:
              - path: .
                owners:
                  - test-user-01
                  - test-user-02
                pr-labels: [Example]
            """, "test-user-01");

        Assert.That(result, Does.Not.Contain("test-user-01"));
        Assert.That(result, Does.Contain("- test-user-02"));
        Assert.That(result, Does.Contain("pr-labels: [Example]"));
    }

    [Test]
    public void RemovesFromEveryOwnerListInTheFile()
    {
        var result = Remove(
            """
            version: 1
            paths:
              - path: .
                owners: [test-user-01, test-user-02]
                pr-labels: [Example]
            label-owners:
              - labels: [Example]
                service-owners: [test-user-01, test-user-03]
                azure-sdk-owners: [test-user-01]
            """, "test-user-01");

        Assert.That(result, Does.Not.Contain("test-user-01"));
        Assert.That(result, Does.Contain("owners: [test-user-02]"));
        Assert.That(result, Does.Contain("service-owners: [test-user-03]"));
        Assert.That(result, Does.Contain("azure-sdk-owners: []"));
    }

    [Test]
    public void PreservesCommentsAndFormatting()
    {
        var result = Remove(
            """
            # Owners for the AI service.
            version: 1

            paths:
              # The service directory itself.
              - path: .
                owners: [test-user-01, test-user-02]
                pr-labels: [Example]
            """, "test-user-02");

        Assert.That(result, Does.Contain("# Owners for the AI service."));
        Assert.That(result, Does.Contain("# The service directory itself."));
        Assert.That(result, Does.Contain("\n\npaths:"));
    }

    [Test]
    public void LeavesAnAliasMentionedInACommentAlone()
    {
        var result = Remove(
            """
            version: 1
            paths:
              - path: .
                # test-user-02 asked to be added here.
                owners: [test-user-01, test-user-02]
                pr-labels: [Example]
            """, "test-user-02");

        Assert.That(result, Does.Contain("# test-user-02 asked to be added here."));
        Assert.That(result, Does.Contain("owners: [test-user-01]"));
    }

    [Test]
    public void LeavesALabelThatLooksLikeTheAliasAlone()
    {
        var result = Remove(
            """
            version: 1
            paths:
              - path: .
                owners: [test-user-01, test-user-02]
                pr-labels: [test-user-02]
            """, "test-user-02");

        Assert.That(result, Does.Contain("pr-labels: [test-user-02]"));
        Assert.That(result, Does.Contain("owners: [test-user-01]"));
    }

    [Test]
    public void LeavesAnAliasThatIsAPrefixOfAnotherAlone()
    {
        var result = Remove(
            """
            version: 1
            paths:
              - path: .
                owners: [test-user-1, test-user-10]
                pr-labels: [Example]
            """, "test-user-1");

        Assert.That(result, Does.Contain("owners: [test-user-10]"));
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
}
