// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class SdkChangeHelperTests
{
    private TempDirectory _directory = null!;
    private string _reportPath = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = TempDirectory.Create("sdk-change-report");
        _reportPath = Path.Combine(_directory.DirectoryPath, "changes.json");
    }

    [TearDown]
    public void TearDown() => _directory.Dispose();

    [TestCase("not json")]
    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("{}")]
    [TestCase("{\"changes\":\"text\"}")]
    [TestCase("{\"hasBreakingChange\":false}")]
    [TestCase("{\"changes\":\"text\",\"hasBreakingChange\":null}")]
    [TestCase("{\"changes\":\"text\",\"hasBreakingChange\":\"false\"}")]
    [TestCase("{\"changes\":\"text\",\"hasBreakingChange\":0}")]
    [TestCase("{\"changes\":42,\"hasBreakingChange\":false}")]
    [TestCase("{\"changes\":[],\"hasBreakingChange\":false}")]
    public void MalformedReport_IsNotACleanResult(string report)
    {
        File.WriteAllText(_reportPath, report);

        Assert.ThrowsAsync<JsonException>(() => SdkChangeHelper.ReadFromFileAsync(_reportPath, CancellationToken.None));
    }

    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase("  \r\n\t", false)]
    [TestCase(null, true)]
    [TestCase("", true)]
    [TestCase("  \r\n\t", true)]
    public void EmptyMarkdown_IsRejectedRegardlessOfBreakingFlag(string? changes, bool breaking)
    {
        File.WriteAllText(_reportPath, JsonSerializer.Serialize(new { changes, hasBreakingChange = breaking }));

        var exception = Assert.ThrowsAsync<JsonException>(() =>
            SdkChangeHelper.ReadFromFileAsync(_reportPath, CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("changes (Markdown)").And.Not.Contain("details"));
    }

    [TestCase("No SDK breaking changes detected.", false)]
    [TestCase("### Features Added\n- Widget", false)]
    [TestCase("### Breaking Changes\n- Widget removed", true)]
    public async Task ValidLegacyReport_DoesNotRequireOptionalDetails(string changes, bool breaking)
    {
        await File.WriteAllTextAsync(_reportPath, JsonSerializer.Serialize(new { changes, hasBreakingChange = breaking }));

        var result = await SdkChangeHelper.ReadFromFileAsync(_reportPath, CancellationToken.None);

        Assert.That(result.SdkChangeMD, Is.EqualTo(changes));
        Assert.That(result.HasBreakingChange, Is.EqualTo(breaking));
        Assert.That(result.Details, Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public async Task NoBaselineReport_PreservesLimitationsWithoutInventingProvenance(string? baseline)
    {
        await File.WriteAllTextAsync(_reportPath, JsonSerializer.Serialize(new
        {
            changes = "No released baseline.",
            hasBreakingChange = false,
            details = new { baselineVersion = baseline, limitations = new[] { "Compatibility not evaluated." } },
        }));

        var result = await SdkChangeHelper.ReadFromFileAsync(_reportPath, CancellationToken.None);

        Assert.That(result.Details!.BaselineVersion, Is.EqualTo(baseline));
        Assert.That(result.Details.Limitations.Single(), Is.EqualTo("Compatibility not evaluated."));
    }

    [Test]
    public async Task NativeEvidence_RoundTripsUnknownProvenanceAndApiFields()
    {
        await File.WriteAllTextAsync(_reportPath, """
            {"changes":"### Breaking Changes\n- Widget removed","hasBreakingChange":true,
             "details":{"baselineVersion":"1.2.3","baselineSource":{"feed":"NuGet"},"detectorVersion":2,
               "apiChanges":[{"kind":"removed","symbol":"Widget","description":"Removed","isBreaking":true,
                 "diagnosticId":"CP0001","targetFramework":"net8.0","oldSignature":"class Widget","newSignature":null}],
               "diagnostics":["CP0001"],"limitations":["Behavior not compared"]}}
            """);

        var result = await SdkChangeHelper.ReadFromFileAsync(_reportPath, CancellationToken.None);
        var json = JsonSerializer.SerializeToElement(result).GetProperty("details");

        Assert.That(result.Details!.ApiChanges.Single(), Is.TypeOf<Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection.DotnetSdkApiChange>());
        Assert.That(json.GetProperty("baselineSource").GetProperty("feed").GetString(), Is.EqualTo("NuGet"));
        Assert.That(json.GetProperty("detectorVersion").GetInt32(), Is.EqualTo(2));
        Assert.That(json.GetProperty("apiChanges")[0].GetProperty("oldSignature").GetString(), Is.EqualTo("class Widget"));
        Assert.That(json.GetProperty("apiChanges")[0].GetProperty("newSignature").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public void Cancellation_Propagates()
    {
        File.WriteAllText(_reportPath, """{"changes":"No changes","hasBreakingChange":false}""");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() => SdkChangeHelper.ReadFromFileAsync(_reportPath, cts.Token));
    }
}
