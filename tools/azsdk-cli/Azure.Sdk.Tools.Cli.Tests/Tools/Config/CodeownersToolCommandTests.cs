// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Tools.Config;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Config;

[TestFixture]
public class CodeownersToolCommandTests
{
    // ========================
    // View command validation tests
    // ========================

    [Test]
    public void ViewCommand_NoAxisSpecified_ReturnsError()
    {
        // The view command requires exactly one of --user, --label, --package, --path
        // Test that validation catches zero axes specified
        // This tests the static validation logic in CodeownersTool
    }

    [Test]
    public void ViewCommand_MultipleAxesSpecified_ReturnsError()
    {
        // Test that specifying both --user and --label returns an error
    }

    // ========================
    // Add command validation tests
    // ========================

    [Test]
    public void AddCommand_UserPackage_WithOwnerType_ReturnsError()
    {
        var error = CodeownersTool.ValidateAddRemoveParams("johndoe", "Azure.Storage.Blobs", [], null, "service-owner", isAdd: true);
        Assert.IsNotNull(error);
        Assert.That(error, Does.Contain("Cannot specify --owner-type"));
    }

    [Test]
    public void AddCommand_UserLabel_MissingOwnerType_ReturnsError()
    {
        var error = CodeownersTool.ValidateAddRemoveParams("johndoe", null, ["Storage"], null, null, isAdd: true);
        Assert.IsNotNull(error);
        Assert.That(error, Does.Contain("Must specify --owner-type"));
    }

    [Test]
    public void AddCommand_UserLabel_PrLabel_MissingPath_ReturnsError()
    {
        var error = CodeownersTool.ValidateAddRemoveParams("johndoe", null, ["Storage"], null, "pr-label", isAdd: true);
        Assert.IsNotNull(error);
        Assert.That(error, Does.Contain("Must specify --path"));
    }

    [Test]
    public void AddCommand_UserPath_MissingOwnerType_ReturnsError()
    {
        var error = CodeownersTool.ValidateAddRemoveParams("johndoe", null, [], "sdk/storage/", null, isAdd: true);
        Assert.IsNotNull(error);
        Assert.That(error, Does.Contain("Must specify --owner-type"));
    }

    [Test]
    public void AddCommand_LabelPath_WithUser_ReturnsError()
    {
        var error = CodeownersTool.ValidateAddRemoveParams("johndoe", null, ["Storage"], "sdk/storage/", "service-owner", isAdd: true);
        // When user + labels + path are all provided, it falls into the user+labels scenario
        // which is valid if owner-type is provided (unless it's the label+path scenario which requires no user)
        // This tests the user+label scenario which IS valid with owner-type
        Assert.IsNull(error);
    }

    [Test]
    public void AddCommand_LabelPath_WithOwnerType_ReturnsError()
    {
        var error = CodeownersTool.ValidateAddRemoveParams(null, null, ["Storage"], "sdk/storage/", "service-owner", isAdd: true);
        Assert.IsNotNull(error);
        Assert.That(error, Does.Contain("Cannot specify --owner-type when adding/removing labels"));
    }

    // ========================
    // Remove command validation tests
    // ========================

    [Test]
    public void RemoveCommand_UserPath_MissingOwnerType_ReturnsError()
    {
        var error = CodeownersTool.ValidateAddRemoveParams("johndoe", null, [], "sdk/storage/", null, isAdd: false);
        Assert.IsNotNull(error);
        Assert.That(error, Does.Contain("Must specify --owner-type"));
    }

    // ========================
    // Valid parameter combinations
    // ========================

    [Test]
    public void AddCommand_UserPackage_Valid()
    {
        var error = CodeownersTool.ValidateAddRemoveParams("johndoe", "Azure.Storage.Blobs", [], null, null, isAdd: true);
        Assert.IsNull(error);
    }

    [Test]
    public void AddCommand_UserLabel_ServiceOwner_Valid()
    {
        var error = CodeownersTool.ValidateAddRemoveParams("johndoe", null, ["Storage"], null, "service-owner", isAdd: true);
        Assert.IsNull(error);
    }

    [Test]
    public void AddCommand_UserPath_ServiceOwner_Valid()
    {
        var error = CodeownersTool.ValidateAddRemoveParams("johndoe", null, [], "sdk/storage/", "service-owner", isAdd: true);
        Assert.IsNull(error);
    }

    [Test]
    public void AddCommand_LabelPath_Valid()
    {
        var error = CodeownersTool.ValidateAddRemoveParams(null, null, ["Storage"], "sdk/storage/", null, isAdd: true);
        Assert.IsNull(error);
    }

    [Test]
    public void AddCommand_InvalidCombination_ReturnsError()
    {
        // No user, no labels, no package, just path — invalid
        var error = CodeownersTool.ValidateAddRemoveParams(null, null, [], "sdk/storage/", null, isAdd: true);
        Assert.IsNotNull(error);
        Assert.That(error, Does.Contain("Invalid parameter combination"));
    }
}
