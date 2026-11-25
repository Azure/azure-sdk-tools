// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Services.Notification.Templates;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class FinishedReleasePlanEmailTests
{
    private static readonly DateTimeOffset CompletedOn = new(2026, 11, 25, 14, 30, 0, TimeSpan.FromHours(-8));

    [TestCase(true)]
    [TestCase(false)]
    public void Body_UsesIssueTemplateAndOnlyIncludesReleasedSdks(bool management)
    {
        var plan = CreatePlan();
        plan.IsManagementPlane = management;
        plan.IsDataPlane = !management;

        var metadata = new Dictionary<string, ReleasedPackageMetadata>
        {
            [".NET"] = new("", ""),
            ["python"] = new("Contoso Service", "")
        };

        var email = new FinishedReleasePlanEmail(plan, CompletedOn, metadata);

        Assert.Multiple(() =>
        {
            Assert.That(email.EmailTo, Is.EqualTo(new[] { "owner@microsoft.com" }));
            Assert.That(email.CC, Is.Empty);
            Assert.That(email.Subject, Does.Contain("42").And.Contain(plan.Title));
            Assert.That(email.Body, Does.Contain("<p>Hello Test Owner,</p>"));
            Assert.That(email.Body, Does.Contain("now marked <b>Finished</b>"));
            Assert.That(email.Body, Does.Contain($"<a href=\"{plan.ReleasePlanLink}\">{plan.Title}</a> (ID: 42)"));
            Assert.That(email.Body, Does.Contain("<td>Contoso Service / Contoso</td>"));
            Assert.That(email.Body, Does.Contain("<td>Public Preview</td>"));
            Assert.That(email.Body, Does.Contain($"<td>{(management ? "Management plane" : "Data plane")}</td>"));
            Assert.That(email.Body, Does.Contain($"<td>{plan.SpecAPIVersion}</td>"));
            Assert.That(email.Body, Does.Contain($"<a href=\"{plan.ActiveSpecPullRequest}\">{plan.ActiveSpecPullRequest}</a>"));
            Assert.That(email.Body, Does.Contain("<td>November 2026</td>"));
            Assert.That(email.Body, Does.Contain("<td>2026-11-25 22:30:00 UTC</td>"));
            Assert.That(email.Body, Does.Contain("<tr><th>Language</th><th>Package name</th><th>Version released</th><th>Package link</th></tr>"));
            Assert.That(email.Body, Does.Contain("<tr><td>Python</td><td>azure-contoso</td><td>1.2.3b1</td><td><a href=\"https://pypi.org/project/azure-contoso/1.2.3b1/\">https://pypi.org/project/azure-contoso/1.2.3b1/</a></td></tr>"));
            Assert.That(email.Body, Does.Contain("<tr><td>.NET</td><td>Azure.Contoso</td><td>1.2.3-beta.1</td><td><a href=\"https://www.nuget.org/packages/Azure.Contoso/1.2.3-beta.1\">https://www.nuget.org/packages/Azure.Contoso/1.2.3-beta.1</a></td></tr>"));
            Assert.That(email.Body, Does.Not.Contain("excluded-package").And.Not.Contain("pending-package"));
            Assert.That(email.Body.Contains("<h3>Share your feedback</h3>"), Is.EqualTo(management));
            Assert.That(email.Body.Contains("https://forms.cloud.microsoft/r/rQMr2bc7DT"), Is.EqualTo(management));
            Assert.That(email.Body.Contains("what worked well, what got in your way, and what we should improve."), Is.EqualTo(management));
            Assert.That(email.Body, Does.Contain("<p>If anything in this release looks wrong, reply to this email or contact the Azure SDK team.</p>"));
            Assert.That(email.Body, Does.Contain("<p>Thanks,<br/>Azure SDK Engineering Systems</p>"));
        });
    }

    [Test]
    public void Body_EncodesWorkItemTextAndRejectsUnsafeLinks()
    {
        var plan = CreatePlan();
        plan.Owner = "Owner <admin> & \"name\"";
        plan.Title = "Title <script> & \"release\"";
        plan.ProductName = "Product <script>";
        plan.SpecAPIVersion = "<version>";
        plan.SDKReleaseMonth = "<month>";
        plan.ActiveSpecPullRequest = "javascript:alert('spec')";
        plan.SDKInfo[0].PackageName = "<package>";
        plan.SDKInfo[0].ReleasedVersion = "<version-released>";
        plan.SDKInfo[0].Language = "<language>";

        var body = new FinishedReleasePlanEmail(plan, CompletedOn).Body;

        foreach (var value in new[] { plan.Owner, plan.Title, plan.ProductName, plan.SpecAPIVersion,
            plan.SDKReleaseMonth, plan.SDKInfo[0].PackageName, plan.SDKInfo[0].ReleasedVersion, plan.SDKInfo[0].Language })
        {
            Assert.That(body, Does.Contain(WebUtility.HtmlEncode(value)));
            Assert.That(body, Does.Not.Contain(value));
        }
        Assert.That(body, Does.Not.Contain("href=\"javascript:"));
    }

    [TestCase("JavaScript", "@azure/arm-contoso", "1.0.0", null, "https://www.npmjs.com/package/@azure/arm-contoso/v/1.0.0")]
    [TestCase("Java", "azure-resourcemanager-contoso", "1.0.0", "com.azure.resourcemanager", "https://central.sonatype.com/artifact/com.azure.resourcemanager/azure-resourcemanager-contoso/1.0.0")]
    [TestCase("Java", "com.azure:azure-contoso", "1.0.0", null, "https://central.sonatype.com/artifact/com.azure/azure-contoso/1.0.0")]
    [TestCase("Java", "azure-contoso", "1.0.0", "", "")]
    [TestCase("Go", "sdk/resourcemanager/contoso/armcontoso", "1.0.0", null, "https://pkg.go.dev/github.com/Azure/azure-sdk-for-go/sdk/resourcemanager/contoso/armcontoso@v1.0.0")]
    [TestCase("Go", "armcontoso", "v1.0.0", null, "")]
    [TestCase("Python", "azure-contoso", "", null, "")]
    [TestCase("Unknown", "contoso", "1.0.0", null, "")]
    public void GetPackageRegistryLink_UsesLanguageRegistry(string language, string packageName, string version, string? groupId, string expected)
    {
        Assert.That(FinishedReleasePlanEmail.GetPackageRegistryLink(language, packageName, version, groupId), Is.EqualTo(expected));
    }

    [Test]
    public void MissingActiveSpecPullRequest_UsesRecordedSpecPullRequest()
    {
        var plan = CreatePlan();
        var specPullRequest = plan.ActiveSpecPullRequest;
        plan.SpecPullRequests = [specPullRequest];
        plan.ActiveSpecPullRequest = "";

        var body = new FinishedReleasePlanEmail(plan, CompletedOn).Body;

        Assert.That(body, Does.Contain($"<a href=\"{specPullRequest}\">{specPullRequest}</a>"));
    }

    [Test]
    public void MissingDetails_AreExplicitAndDoNotCreateEmptyLinksOrRecipients()
    {
        var plan = CreatePlan();
        plan.ReleasePlanSubmittedByEmail = "";
        plan.ActiveSpecPullRequest = "";
        plan.SDKInfo[0].ReleasedVersion = "";

        var email = new FinishedReleasePlanEmail(plan, CompletedOn);

        Assert.That(email.EmailTo, Is.Empty);
        Assert.That(email.CC, Is.Empty);
        Assert.That(email.Body, Does.Contain("<b>API spec PR</b></td><td>Not recorded</td>"));
        Assert.That(email.Body, Does.Contain("<td>azure-contoso</td><td>Not recorded</td><td>Not recorded</td>"));
        Assert.That(email.Body, Does.Contain("<td>Not recorded / Contoso</td>"));
        Assert.That(email.Body, Does.Not.Contain("href=\"\""));
    }

    private static ReleasePlanWorkItem CreatePlan() => new()
    {
        ReleasePlanId = 42,
        Title = "Contoso release",
        Owner = "Test Owner",
        ReleasePlanSubmittedByEmail = "owner@microsoft.com",
        ProductName = "Contoso",
        ApiReleaseType = ApiReleaseType.PublicPreview,
        SpecAPIVersion = "2026-11-01-preview",
        ActiveSpecPullRequest = "https://github.com/Azure/azure-rest-api-specs/pull/42",
        SDKReleaseMonth = "November 2026",
        SDKInfo =
        [
            new SDKInfo { Language = "Python", PackageName = "azure-contoso", ReleasedVersion = "1.2.3b1", ReleaseStatus = "Released" },
            new SDKInfo { Language = ".NET", PackageName = "Azure.Contoso", ReleasedVersion = "1.2.3-beta.1", ReleaseStatus = "released" },
            new SDKInfo { Language = "Java", PackageName = "excluded-package", ReleaseExclusionStatus = "Approved" },
            new SDKInfo { Language = "Go", PackageName = "pending-package", ReleaseStatus = "Pending" }
        ]
    };
}
