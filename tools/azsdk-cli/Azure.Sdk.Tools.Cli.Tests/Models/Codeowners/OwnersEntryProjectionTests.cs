// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Tests.Models.Codeowners;

/// <summary>
/// Pins the projection from the YAML models onto <c>CodeownersEntry</c>, which is what lets the
/// renderer reuse <c>CodeownersEntrySorter</c> and <c>FormatCodeownersEntry</c> unchanged.
/// </summary>
[TestFixture]
public class OwnersEntryProjectionTests
{
    [Test]
    public void PathEntry_ProjectsToSourcePathBlock()
    {
        var entry = new OwnersPathEntry
        {
            Path = "Azure.AI.Inference/",
            Owners = ["test-user-07", "Azure/azure-sdk-write-net-core"],
            PrLabels = ["AI Model Inference"],
        };

        var codeowners = entry.ToCodeownersEntry("/sdk/ai/Azure.AI.Inference/");

        Assert.Multiple(() =>
        {
            Assert.That(codeowners.PathExpression, Is.EqualTo("/sdk/ai/Azure.AI.Inference/"));
            Assert.That(codeowners.SourceOwners, Is.EqualTo(entry.Owners));
            Assert.That(codeowners.PRLabels, Is.EqualTo(entry.PrLabels));
            Assert.That(codeowners.ServiceLabels, Is.Empty);
            Assert.That(codeowners.ServiceOwners, Is.Empty);
        });
    }

    [Test]
    public void LabelOwnerEntry_ProjectsToPathlessBlock()
    {
        var entry = new OwnersLabelOwnerEntry
        {
            Labels = ["AI Projects"],
            ServiceOwners = ["test-user-07", "test-user-18"],
            AzureSdkOwners = ["test-user-07"],
        };

        var codeowners = entry.ToCodeownersEntry();

        Assert.Multiple(() =>
        {
            Assert.That(codeowners.PathExpression, Is.Empty);
            Assert.That(codeowners.ServiceLabels, Is.EqualTo(entry.Labels));
            Assert.That(codeowners.ServiceOwners, Is.EqualTo(entry.ServiceOwners));
            Assert.That(codeowners.AzureSdkOwners, Is.EqualTo(entry.AzureSdkOwners));
        });
    }

    [Test]
    public void Projection_CopiesLists_SoLaterEditsDoNotLeakIntoTheModel()
    {
        var entry = new OwnersPathEntry { Path = ".", Owners = ["test-user-07"], PrLabels = ["AI Projects"] };

        var codeowners = entry.ToCodeownersEntry("/sdk/ai/");
        codeowners.SourceOwners.Add("test-user-99");

        Assert.That(entry.Owners, Is.EqualTo(new[] { "test-user-07" }));
    }
}
