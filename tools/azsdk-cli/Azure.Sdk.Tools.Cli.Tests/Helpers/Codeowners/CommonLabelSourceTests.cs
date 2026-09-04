// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Codeowners;

internal class CommonLabelSourceTests
{
    [Test]
    public void TakesTheNameFromEachRow()
    {
        var labels = CommonLabelSource.ParseLabelNames(
            """
            Storage,Storage description,ffffff
            Event Hubs,Event Hubs description,000000
            """);

        Assert.That(labels, Is.EquivalentTo(new[] { "Storage", "Event Hubs" }));
    }

    [Test]
    public void MatchesNamesWithoutRegardToCase()
    {
        var labels = CommonLabelSource.ParseLabelNames("Storage,description,ffffff");

        // NUnit's Does.Contain uses its own comparer, so ask the set the way the lint rules do.
        Assert.That(labels.Contains("storage"), Is.True);
    }

    [Test]
    public void KeepsCommasInsideAQuotedName()
    {
        var labels = CommonLabelSource.ParseLabelNames("\"Storage, Blob\",description,ffffff");

        Assert.That(labels, Is.EquivalentTo(new[] { "Storage, Blob" }));
    }

    [Test]
    public void IgnoresBlankRowsAndCarriageReturns()
    {
        var labels = CommonLabelSource.ParseLabelNames("Storage,d,f\r\n\r\nEvent Hubs,d,f\r\n");

        Assert.That(labels, Is.EquivalentTo(new[] { "Storage", "Event Hubs" }));
    }
}
