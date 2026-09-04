// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Codeowners;

/// <summary>
/// The single answer to "can this alias own code" and "who does it expand to". Both feed decisions
/// that block releases, and expansion now counts toward the owner minimums, so it is covered here
/// rather than only through its callers.
/// </summary>
internal class OwnerValidatorTests
{
    private static IOwnerValidator Validator(
        IEnumerable<string>? writeTeamMembers = null,
        IDictionary<string, List<string>>? teams = null,
        IEnumerable<string>? orgVisible = null) =>
        OwnerValidatorFake.Create(writeTeamMembers ?? ["alice", "bob"], teams, orgVisible);

    [Test]
    public void IndividualWithWriteAccessAndPublicOrgMembershipIsValid()
    {
        Assert.That(Validator().Validate("alice", where: null), Is.Null);
    }

    [Test]
    public void LeadingAtSignIsAccepted()
    {
        Assert.That(Validator().Validate("@alice", where: null), Is.Null);
    }

    [Test]
    public void PrivateOrgMembershipIsReportedSeparatelyFromMissingWriteAccess()
    {
        // These are fixed by different people, so the detail has to name which half failed.
        var validator = Validator(["alice", "bob"], orgVisible: ["bob"]);

        var violation = validator.Validate("alice", where: "sdk/ai/owners.yaml:4");

        Assert.That(violation, Is.Not.Null);
        Assert.That(violation!.RuleId, Is.EqualTo("LNT-OWN-001"));
        Assert.That(violation.SourceFile, Is.EqualTo("sdk/ai/owners.yaml:4"));
        Assert.That(violation.Detail, Does.Contain("private"));
    }

    [Test]
    public void MissingWriteAccessIsReported()
    {
        var violation = Validator().Validate("carol", where: null);

        Assert.That(violation, Is.Not.Null);
        Assert.That(violation!.RuleId, Is.EqualTo("LNT-OWN-001"));
    }

    [Test]
    public void TeamOutsideAzureOrgIsMalformed()
    {
        var violation = Validator().Validate("SomeOrg/team", where: null);

        Assert.That(violation, Is.Not.Null);
        Assert.That(violation!.RuleId, Is.EqualTo("LNT-OWN-002"));
        Assert.That(violation.Description, Does.Contain("malformed"));
    }

    [Test]
    public void TeamWithNoCachedMembershipDoesNotDescendFromWriteTeam()
    {
        var violation = Validator().Validate("Azure/not-a-real-team", where: null);

        Assert.That(violation, Is.Not.Null);
        Assert.That(violation!.RuleId, Is.EqualTo("LNT-OWN-002"));
        Assert.That(violation.Description, Does.Contain("does not descend"));
    }

    [Test]
    public void TeamWithCachedMembershipIsValid()
    {
        var validator = Validator(teams: new Dictionary<string, List<string>> { ["Azure/ai"] = ["alice"] });

        Assert.That(validator.Validate("Azure/ai", where: null), Is.Null);
    }

    [Test]
    public void IndividualExpandsToItself()
    {
        Assert.That(Validator().ExpandToIndividuals(["@alice"]).ToArray(), Is.EqualTo(new[] { "alice" }));
    }

    [Test]
    public void TeamExpandsToItsMembers()
    {
        var validator = Validator(teams: new Dictionary<string, List<string>> { ["Azure/ai"] = ["alice", "bob"] });

        Assert.That(
            validator.ExpandToIndividuals(["Azure/ai"]).ToArray(),
            Is.EquivalentTo(new[] { "alice", "bob" }));
    }

    [Test]
    public void MemberCountedOnceWhenNamedDirectlyAndViaTeam()
    {
        // Owner minimums are about distinct people, so overlap between a team and a named owner must
        // not inflate the count.
        var validator = Validator(teams: new Dictionary<string, List<string>> { ["Azure/ai"] = ["alice", "bob"] });

        Assert.That(
            validator.ExpandToIndividuals(["alice", "Azure/ai"]).ToArray(),
            Is.EquivalentTo(new[] { "alice", "bob" }));
    }

    [Test]
    public void TeamWithNoCachedMembershipContributesNobody()
    {
        Assert.That(Validator().ExpandToIndividuals(["Azure/not-a-real-team"]), Is.Empty);
    }
}
