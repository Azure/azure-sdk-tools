// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Services.Notification.Templates;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class ReleasePlanSdkGenerationEmailTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void Notification_IncludesSubmitterSupportAndConditionalManagementAlias(bool management)
    {
        var completed = new ReleasePlanWorkItem { ReleasePlanId = 123 };
        var pending = new ReleasePlanWorkItem
        {
            ReleasePlanId = 456,
            SpecAPIVersion = "2025-02-01-preview",
            IsManagementPlane = management,
            ReleasePlanSubmittedByEmail = "submitter@microsoft.com"
        };
        var email = new ReleasePlanSdkGenerationEmail(completed, pending, generationQueued: true);

        Assert.That(email.EmailTo, Is.EquivalentTo(new[] { "submitter@microsoft.com", "azsdkexp@microsoft.com" }));
        Assert.That(email.CC.Contains("sdkreleaseowners@microsoft.com"), Is.EqualTo(management));
        Assert.That(email.Subject, Does.Contain("456").And.Contain("2025-02-01-preview"));
        Assert.That(email.Body, Does.Contain("Release plan 123").And.Contain("complete (Finished)"));
        Assert.That(email.Body, Does.Contain("release plan 456").And.Contain("2025-02-01-preview"));
        Assert.That(email.Body, Does.Contain("https://azsdk-releaseplan-dashboard-hveph5aqhhcfhtgu.westus-01.azurewebsites.net/?releasePlan=123"));
        Assert.That(email.Body, Does.Contain("https://azsdk-releaseplan-dashboard-hveph5aqhhcfhtgu.westus-01.azurewebsites.net/?releasePlan=456"));
        Assert.That(email.Body, Does.Contain("queued SDK generation"));
        Assert.That(email.Body, Does.Contain("once they are ready"));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(email));
        Assert.That(json.RootElement.GetProperty("EmailTo").GetString(), Does.Contain("submitter@microsoft.com"));
        Assert.That(json.RootElement.GetProperty("Body").GetString(), Is.EqualTo(email.Body));
    }

    [Test]
    public void Notification_TestPlan_OnlyNotifiesSubmitterAndUsesTestDashboard()
    {
        var email = new ReleasePlanSdkGenerationEmail(
            new ReleasePlanWorkItem { ReleasePlanId = 123, IsTestReleasePlan = true },
            new ReleasePlanWorkItem
            {
                ReleasePlanId = 456,
                IsTestReleasePlan = true,
                IsManagementPlane = true,
                ReleasePlanSubmittedByEmail = "submitter@microsoft.com"
            }, generationQueued: true);
        Assert.That(email.EmailTo, Is.EqualTo(new[] { "submitter@microsoft.com" }));
        Assert.That(email.CC, Is.Empty);
        Assert.That(email.Body, Does.Contain("https://releaseplan-dashboard-test.azurewebsites.net/?releasePlan=123"));
        Assert.That(email.Body, Does.Contain("https://releaseplan-dashboard-test.azurewebsites.net/?releasePlan=456"));
        Assert.That(email.Body, Does.Not.Contain(ReleasePlanWorkItem.DashboardBaseUrl.Split('?')[0]));
    }

    [Test]
    public void Notification_MissingSubmitter_StillNotifiesSupport()
    {
        var email = new ReleasePlanSdkGenerationEmail(new ReleasePlanWorkItem(), new ReleasePlanWorkItem { IsManagementPlane = true }, generationQueued: true);
        Assert.That(email.EmailTo, Is.EqualTo(new[] { "azsdkexp@microsoft.com" }));
        Assert.That(email.CC, Is.EqualTo(new[] { "sdkreleaseowners@microsoft.com" }));
    }

    [Test]
    public void Notification_EncodesDynamicHtml()
    {
        var email = new ReleasePlanSdkGenerationEmail(
            new ReleasePlanWorkItem { ReleasePlanId = 123 },
            new ReleasePlanWorkItem { ReleasePlanId = 456, SpecAPIVersion = "<invalid&version>" }, generationQueued: true);
        Assert.That(email.Body, Does.Contain("&lt;invalid&amp;version&gt;"));
        Assert.That(email.Body, Does.Not.Contain("<invalid&version>"));
    }

    [Test]
    public void Notification_QueueFailure_DirectsSubmitterToAgentAndDashboard()
    {
        var email = new ReleasePlanSdkGenerationEmail(
            new ReleasePlanWorkItem { ReleasePlanId = 123 },
            new ReleasePlanWorkItem
            {
                ReleasePlanId = 456,
                SpecAPIVersion = "2025-02-01-preview",
                IsManagementPlane = true,
                ReleasePlanSubmittedByEmail = "submitter@microsoft.com"
            }, generationQueued: false);

        Assert.That(email.Subject, Does.Contain("Action required").And.Contain("456"));
        Assert.That(email.EmailTo, Does.Contain("submitter@microsoft.com"));
        Assert.That(email.EmailTo, Does.Contain("azsdkexp@microsoft.com"));
        Assert.That(email.CC, Does.Contain("sdkreleaseowners@microsoft.com"));
        Assert.That(email.Body, Does.Contain("unable to queue SDK generation automatically"));
        Assert.That(email.Body, Does.Contain("azsdk agent").And.Contain("https://aka.ms/azsdk/agent"));
        Assert.That(email.Body, Does.Contain("2025-02-01-preview"));
        Assert.That(email.Body, Does.Contain("?releasePlan=123").And.Contain("?releasePlan=456"));
        Assert.That(email.Body, Does.Not.Contain("We have automatically queued"));
    }
}
