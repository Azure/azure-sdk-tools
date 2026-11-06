// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Tests.Models;

[TestFixture]
public class ApiReleaseTypeTests
{
    [TestCase("GA", ApiReleaseType.GA)]
    [TestCase("ga", ApiReleaseType.GA)]
    [TestCase("APEX GA", ApiReleaseType.GA)]
    [TestCase("apex ga", ApiReleaseType.GA)]
    [TestCase("APEX Public Preview", ApiReleaseType.PublicPreview)]
    [TestCase("APEX Private Preview", ApiReleaseType.PrivatePreview)]
    [TestCase(null, ApiReleaseType.Unknown)]
    [TestCase("", ApiReleaseType.Unknown)]
    [TestCase("Unknown", ApiReleaseType.Unknown)]
    [TestCase("APEX GA preview", ApiReleaseType.Unknown)]
    [TestCase("Not GA", ApiReleaseType.Unknown)]
    public void FromAdoFieldValue_RecognizesOnlySupportedValues(string? value, ApiReleaseType expected)
    {
        Assert.That(ApiReleaseTypeExtensions.FromAdoFieldValue(value), Is.EqualTo(expected));
    }

    [TestCase("GA")]
    [TestCase("APEX GA")]
    [TestCase("apex ga")]
    public void FromAdoFieldValue_GaAliasPreservesSnapshotAndWritesCanonicalValue(string value)
    {
        var plan = new ReleasePlanWorkItem { ReleasePlanType = value };

        Assert.That(plan.ApiReleaseType, Is.EqualTo(ApiReleaseType.GA));
        Assert.That(plan.ReleasePlanType, Is.EqualTo(value), "Reading must not mutate the stored value.");
        Assert.That(plan.ApiReleaseType.ToAdoFieldValue(), Is.EqualTo("GA"));
        Assert.That(ApiReleaseTypeExtensions.TryParseFromUserInput("APEX GA", out _), Is.False,
            "The legacy alias is for reading ADO data, not a new user-facing release type.");
    }
}