// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Net;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Services.Notification;
using Azure.Sdk.Tools.Cli.Services.Notification.Templates;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;
using Moq.Protected;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class NotificationServiceTests
{
    private const string ServiceUrl = "https://notifications.example.com/send";

    private const string AutomatedSdkPullRequestText =
        "SDK pull requests: One SDK pull request per language (.NET, Java, JavaScript/TypeScript, Python, and Go (optional for data plane)) will be generated and linked to this plan. " +
        "When each PR is ready, review and approve it, then complete the merge and release by following your release plan dashboard. " +
        "The Azure SDK Tools Agent can walk you through these steps.";

    private TestLogger<NotificationService> logger;
    private Mock<IHttpClientFactory> mockHttpClientFactory;
    private Mock<IEnvironmentHelper> mockEnvironmentHelper;

    [SetUp]
    public void Setup()
    {
        logger = new TestLogger<NotificationService>();
        mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockEnvironmentHelper = new Mock<IEnvironmentHelper>();
    }

    private (NotificationService service, List<string> captured) CreateService(string url)
    {
        mockEnvironmentHelper
            .Setup(e => e.GetStringVariable(Constants.NOTIFICATION_SERVICE_URL_ENV_VAR, It.IsAny<string>()))
            .Returns(url);

        var capturedBodies = new List<string>();
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                capturedBodies.Add(req.Content is null ? string.Empty : await req.Content.ReadAsStringAsync(ct));
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var client = new HttpClient(mockHandler.Object);
        mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        return (new NotificationService(mockHttpClientFactory.Object, mockEnvironmentHelper.Object, logger), capturedBodies);
    }

    [Test]
    public void EmailTemplate_ConstructsSubjectAndBody_FromReleasePlan()
    {
        var releasePlan = new ReleasePlanWorkItem
        {
            ReleasePlanId = 1,
            ProductName = "Contoso",
            IsManagementPlane = true,
            CreatedUsing = "Automation",
            ProductTreeId = "product-1",
            ServiceTreeId = "service-1",
            ProductType = "Offering",
            SpecPullRequests = ["https://github.com/pr/1"],
            ApiReleaseType = ApiReleaseType.GA,
            SDKInfo = [new SDKInfo { Language = ".NET", PackageName = "Azure.ResourceManager.Contoso" }]
        };

        var template = new NewReleasePlanEmail(releasePlan);

        var subject = template.Subject;
        var body = template.Body;

        Assert.That(subject, Is.EqualTo("Azure SDK Release plan created for Contoso (GA)"));
        Assert.That(body, Does.Contain("https://github.com/pr/1"));
        Assert.That(body, Does.Contain(releasePlan.ReleasePlanLink));
        Assert.That(body, Does.Contain("An Azure SDK release plan has been automatically created after merging"));
        Assert.That(body, Does.Not.Contain("created successfully"));
        Assert.That(body, Does.Contain("A release plan is a guided workflow"));
        Assert.That(body, Does.Contain("https://aka.ms/azsdkdocs/release-plans"));
        Assert.That(body, Does.Contain("<h3>What happens next</h3>"));
        Assert.That(body, Does.Contain(AutomatedSdkPullRequestText));
        Assert.That(body, Does.Not.Contain("You will be reminded automatically if an SDK is not published by the target date."));
        Assert.That(body, Does.Not.Contain("<strong>SDK pull requests:</strong>"));
        Assert.That(body, Does.Not.Contain("<h3>SDK pull requests</h3>"));
        Assert.That(body, Does.Not.Contain("<strong>Action required:</strong>"));
        Assert.That(body, Does.Contain("https://aka.ms/azsdk/agent"));
        Assert.That(body, Does.Not.Contain("{"));
    }

    [Test]
    public async Task SendEmailNotification_NoRecipients_SilentlyCompletes()
    {
        var (service, captured) = CreateService(url: ServiceUrl);

        await service.SendEmailNotificationAsync(new NewReleasePlanEmail(new ReleasePlanWorkItem { ReleasePlanId = 5 }));

        Assert.That(captured, Is.Empty);
    }

    [Test]
    public async Task SendNewReleasePlanNotification_MissingUrl_SilentlyCompletes()
    {
        var (service, captured) = CreateService(url: string.Empty);

        await service.SendEmailNotificationAsync(new NewReleasePlanEmail(new ReleasePlanWorkItem { ReleasePlanId = 5 }));

        Assert.That(captured, Is.Empty);
        mockHttpClientFactory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task SendNewReleasePlanNotification_Management_IncludesAutoGenMessage_NoKpiSection()
    {
        var (service, captured) = CreateService(url: ServiceUrl);

        var releasePlan = new ReleasePlanWorkItem
        {
            ReleasePlanId = 42,
            ProductName = "Contoso",
            IsManagementPlane = true,
            CreatedUsing = "Automation",
            ProductTreeId = "product-1",
            ServiceTreeId = "service-1",
            ProductType = "Offering",
            SpecPullRequests = ["https://github.com/Azure/azure-rest-api-specs/pull/1"],
            ReleasePlanSubmittedByEmail = "author@microsoft.com",
            ApiReleaseType = ApiReleaseType.GA,
            SDKInfo = [new SDKInfo { Language = ".NET", PackageName = "Azure.ResourceManager.Contoso" }]
        };

        var payload = new NewReleasePlanEmail(releasePlan)
        {
            EmailTo = [releasePlan.ReleasePlanSubmittedByEmail, "extra@microsoft.com"]
        };
        await service.SendEmailNotificationAsync(payload);

        Assert.That(captured, Has.Count.EqualTo(1));
        using var doc = JsonDocument.Parse(captured[0]);
        var root = doc.RootElement;

        var to = root.GetProperty("EmailTo").GetString();
        Assert.That(to, Is.EqualTo("author@microsoft.com;extra@microsoft.com"));

        var body = root.GetProperty("Body").GetString();
        Assert.That(body, Does.Contain(AutomatedSdkPullRequestText));
        Assert.That(body, Does.Not.Contain("<strong>SDK pull requests:</strong>"));
        Assert.That(body, Does.Not.Contain("<strong>Action required:</strong>"));

        var subject = root.GetProperty("Subject").GetString();
        Assert.That(subject, Is.EqualTo("Azure SDK Release plan created for Contoso (GA)"));
    }

    [Test]
    public async Task SendNewReleasePlanNotification_DataPlane_MissingProductInfo_IncludesKpiSection()
    {
        var (service, captured) = CreateService(url: ServiceUrl);

        var releasePlan = new ReleasePlanWorkItem
        {
            ReleasePlanId = 7,
            ProductName = "Fabrikam",
            IsManagementPlane = false,
            ProductTreeId = string.Empty,
            ServiceTreeId = "service-1",
            ProductType = string.Empty,
            SpecPullRequests = ["https://github.com/Azure/azure-rest-api-specs/pull/9"],
            ReleasePlanSubmittedByEmail = "author@microsoft.com",
            ApiReleaseType = ApiReleaseType.PublicPreview,
            SDKInfo = [new SDKInfo { Language = "Java", PackageName = "Azure.Fabrikam" }]
        };

        var payload = new NewReleasePlanEmail(releasePlan)
        {
            EmailTo = [releasePlan.ReleasePlanSubmittedByEmail]
        };
        await service.SendEmailNotificationAsync(payload);

        Assert.That(captured, Has.Count.EqualTo(1));
        using var doc = JsonDocument.Parse(captured[0]);
        var body = doc.RootElement.GetProperty("Body").GetString();

        Assert.That(body, Does.Contain("Use the azsdk agent to generate SDK pull requests"));
        Assert.That(body, Does.Not.Contain("<strong>SDK pull requests:</strong>"));
        Assert.That(body, Does.Contain("<strong>Action required:</strong>"));
        Assert.That(body, Does.Contain("This release plan is missing its Service Tree Product ID, Service ID, or Product Type"));
        Assert.That(body, Does.Not.Contain("<h3>Missing required information for KPI attestation</h3>"));
    }

    [Test]
    public void EmailTemplate_ManagementPlane_MissingProductType_IncludesAutoSdkAndActionRequired()
    {
        var releasePlan = new ReleasePlanWorkItem
        {
            ReleasePlanId = 9,
            ProductName = "Fabrikam Relay",
            IsManagementPlane = true,
            CreatedUsing = "Automation",
            ProductTreeId = "product-1",
            ServiceTreeId = "service-1",
            ProductType = string.Empty,
            ApiReleaseType = ApiReleaseType.GA,
            SDKInfo = [new SDKInfo { Language = ".NET", PackageName = "Azure.ResourceManager.FabrikamRelay" }]
        };

        var body = new NewReleasePlanEmail(releasePlan).Body;

        Assert.That(body, Does.Contain(AutomatedSdkPullRequestText));
        Assert.That(body, Does.Contain("<strong>Action required:</strong>"));
        Assert.That(body, Does.Not.Contain("<strong>SDK pull requests:</strong>"));
    }

    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, true)]
    public void EmailTemplate_MissingAnyProductDetail_IncludesActionRequired(
        bool missingProductTreeId,
        bool missingServiceTreeId,
        bool missingProductType)
    {
        var releasePlan = new ReleasePlanWorkItem
        {
            ReleasePlanId = 8,
            ProductName = "Fabrikam",
            ProductTreeId = missingProductTreeId ? string.Empty : "product-1",
            ServiceTreeId = missingServiceTreeId ? string.Empty : "service-1",
            ProductType = missingProductType ? string.Empty : "Offering",
            SDKInfo = [new SDKInfo { Language = "Python", PackageName = "azure-mgmt-fabrikam" }]
        };

        var body = new NewReleasePlanEmail(releasePlan).Body;

        Assert.That(body, Does.Contain("<strong>Action required:</strong>"));
        Assert.That(body, Does.Contain("This release plan is missing its Service Tree Product ID, Service ID, or Product Type"));
    }

    [Test]
    public void EmailTemplate_ComputesRecipients_NonTestManagementPlane()
    {
        var releasePlan = new ReleasePlanWorkItem
        {
            ReleasePlanId = 1,
            IsManagementPlane = true,
            IsTestReleasePlan = false,
            ReleasePlanSubmittedByEmail = "author@microsoft.com"
        };

        var template = new NewReleasePlanEmail(releasePlan);

        Assert.That(template.EmailTo, Is.EqualTo(new[] { "author@microsoft.com" }));
        Assert.That(template.CC, Is.EqualTo(new[] { "azsdkexp@microsoft.com", "sdkowners@microsoft.com" }));
    }

    [Test]
    public void EmailTemplate_ComputesRecipients_NonTestDataPlane_NoSdkOwners()
    {
        var releasePlan = new ReleasePlanWorkItem
        {
            ReleasePlanId = 2,
            IsManagementPlane = false,
            IsTestReleasePlan = false,
            ReleasePlanSubmittedByEmail = "author@microsoft.com"
        };

        var template = new NewReleasePlanEmail(releasePlan);

        Assert.That(template.EmailTo, Is.EqualTo(new[] { "author@microsoft.com" }));
        Assert.That(template.CC, Is.EqualTo(new[] { "azsdkexp@microsoft.com" }));
    }

    [Test]
    public void EmailTemplate_TestReleasePlan_OnlyNotifiesSubmitter_NoCc()
    {
        var releasePlan = new ReleasePlanWorkItem
        {
            ReleasePlanId = 3,
            IsManagementPlane = true,
            IsTestReleasePlan = true,
            ReleasePlanSubmittedByEmail = "author@microsoft.com"
        };

        var template = new NewReleasePlanEmail(releasePlan);

        Assert.That(template.EmailTo, Is.EqualTo(new[] { "author@microsoft.com" }));
        Assert.That(template.CC, Is.Empty);
    }

    [Test]
    public void EmailTemplate_NoSubmitterEmail_EmailToIsEmpty()
    {
        var releasePlan = new ReleasePlanWorkItem
        {
            ReleasePlanId = 4,
            IsManagementPlane = false,
            IsTestReleasePlan = false,
            ReleasePlanSubmittedByEmail = "   "
        };

        var template = new NewReleasePlanEmail(releasePlan);

        Assert.That(template.EmailTo, Is.Empty);
        Assert.That(template.CC, Is.EqualTo(new[] { "azsdkexp@microsoft.com" }));
    }

    [Test]
    public async Task SendEmailNotification_NormalizesRecipients_CaseInsensitive_AndRejectsMalformedDomains()
    {
        var (service, captured) = CreateService(url: ServiceUrl);

        var releasePlan = new ReleasePlanWorkItem
        {
            ReleasePlanId = 11,
            ProductName = "Contoso",
            IsManagementPlane = true,
            CreatedUsing = "Automation",
            ProductTreeId = "product-1",
            ServiceTreeId = "service-1",
            ProductType = "Offering",
            SpecPullRequests = ["https://github.com/Azure/azure-rest-api-specs/pull/1"],
            ApiReleaseType = ApiReleaseType.GA
        };

        var payload = new NewReleasePlanEmail(releasePlan)
        {
            EmailTo =
            [
                "  Author@Microsoft.COM  ",           // mixed case + whitespace, valid
                "author@microsoft.com",                // duplicate (case-insensitive)
                "attacker@microsoft.com.evil",         // malformed domain suffix, must be dropped
                "external@contoso.com",                // non-microsoft, must be dropped
                "   "                                  // whitespace only, must be dropped
            ]
        };

        await service.SendEmailNotificationAsync(payload);

        Assert.That(captured, Has.Count.EqualTo(1));
        using var doc = JsonDocument.Parse(captured[0]);
        var to = doc.RootElement.GetProperty("EmailTo").GetString();
        Assert.That(to, Is.EqualTo("Author@Microsoft.COM"));
    }

    [Test]
    public void EmailTemplate_ManagementPlane_NotAutomationCreated_UsesManualSdkGenMessage()
    {
        var releasePlan = new ReleasePlanWorkItem
        {
            ReleasePlanId = 99,
            ProductName = "Contoso",
            IsManagementPlane = true,
            CreatedUsing = "Copilot",
            ProductTreeId = "product-1",
            ServiceTreeId = "service-1",
            ProductType = "Offering",
            SpecPullRequests = ["https://github.com/Azure/azure-rest-api-specs/pull/1"],
            ApiReleaseType = ApiReleaseType.GA,
            SDKInfo = [new SDKInfo { Language = ".NET", PackageName = "Azure.ResourceManager.Contoso" }]
        };

        var body = new NewReleasePlanEmail(releasePlan).Body;

        Assert.That(body, Does.Contain("Use the azsdk agent to generate SDK pull requests"));
        Assert.That(body, Does.Not.Contain("<strong>SDK pull requests:</strong>"));
        Assert.That(body, Does.Not.Contain("One SDK pull request per language"));
    }

    [Test]
    public void EmailTemplate_MissingSdkInfo_IncludesMissingSdkDetailsMessage()
    {
        var releasePlan = new ReleasePlanWorkItem
        {
            ReleasePlanId = 21,
            ProductName = "Contoso",
            IsManagementPlane = true,
            CreatedUsing = "Automation",
            ProductTreeId = "product-1",
            ServiceTreeId = "service-1",
            ProductType = "Offering",
            ApiReleaseType = ApiReleaseType.GA
        };

        var body = new NewReleasePlanEmail(releasePlan).Body;

        Assert.That(body, Does.Contain("SDK details are currently missing from the release plan"));
        Assert.That(body, Does.Not.Contain("<strong>SDK pull requests:</strong>"));
        Assert.That(body, Does.Not.Contain("One SDK pull request per language"));
        Assert.That(body, Does.Not.Contain("Use the azsdk agent to generate SDK pull requests"));
    }

    [Test]
    public void EmailTemplate_AllSdkPackageNamesMissing_IncludesMissingSdkDetailsMessage()
    {
        var releasePlan = new ReleasePlanWorkItem
        {
            ReleasePlanId = 23,
            ProductName = "Contoso",
            IsManagementPlane = true,
            CreatedUsing = "Automation",
            ProductTreeId = "product-1",
            ServiceTreeId = "service-1",
            ProductType = "Offering",
            ApiReleaseType = ApiReleaseType.GA,
            SDKInfo =
            [
                new SDKInfo { Language = ".NET", PackageName = string.Empty },
                new SDKInfo { Language = "Java", PackageName = " " }
            ]
        };

        var body = new NewReleasePlanEmail(releasePlan).Body;

        Assert.That(body, Does.Contain("SDK details are currently missing from the release plan"));
        Assert.That(body, Does.Not.Contain("<strong>SDK pull requests:</strong>"));
        Assert.That(body, Does.Not.Contain("One SDK pull request per language"));
        Assert.That(body, Does.Not.Contain("Use the azsdk agent to generate SDK pull requests"));
    }

    [Test]
    public void EmailTemplate_WhenAnySdkPackageNameExists_DoesNotIncludeMissingSdkDetailsMessage()
    {
        var releasePlan = new ReleasePlanWorkItem
        {
            ReleasePlanId = 24,
            ProductName = "Contoso",
            IsManagementPlane = true,
            CreatedUsing = "Automation",
            ProductTreeId = "product-1",
            ServiceTreeId = "service-1",
            ProductType = "Offering",
            ApiReleaseType = ApiReleaseType.GA,
            SDKInfo =
            [
                new SDKInfo { Language = ".NET", PackageName = "Azure.ResourceManager.Contoso" },
                new SDKInfo { Language = "Java", PackageName = string.Empty }
            ]
        };

        var body = new NewReleasePlanEmail(releasePlan).Body;

        Assert.That(body, Does.Not.Contain("SDK details are currently missing from the release plan"));
    }
}
