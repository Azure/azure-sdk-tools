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
            ApiReleaseType = ApiReleaseType.GA
        };

        var template = new NewReleasePlanEmail(releasePlan);

        var subject = template.Subject;
        var body = template.Body;

        Assert.That(subject, Is.EqualTo("Release plan created for Contoso (GA)"));
        Assert.That(body, Does.Contain("https://github.com/pr/1"));
        Assert.That(body, Does.Contain(releasePlan.ReleasePlanLink));
        Assert.That(body, Does.Contain("SDK pull requests will be auto generated"));
        Assert.That(body, Does.Contain("<h3>SDK pull requests</h3>"));
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
            ApiReleaseType = ApiReleaseType.GA
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
        Assert.That(body, Does.Contain("SDK pull requests will be auto generated"));
        Assert.That(body, Does.Not.Contain("KPI attestation"));

        var subject = root.GetProperty("Subject").GetString();
        Assert.That(subject, Is.EqualTo("Release plan created for Contoso (GA)"));
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
            ApiReleaseType = ApiReleaseType.PublicPreview
        };

        var payload = new NewReleasePlanEmail(releasePlan)
        {
            EmailTo = [releasePlan.ReleasePlanSubmittedByEmail]
        };
        await service.SendEmailNotificationAsync(payload);

        Assert.That(captured, Has.Count.EqualTo(1));
        using var doc = JsonDocument.Parse(captured[0]);
        var body = doc.RootElement.GetProperty("Body").GetString();

        Assert.That(body, Does.Contain("Please use azsdk agent to generate the SDK pull requests"));
        Assert.That(body, Does.Contain("<h3>Missing required information for KPI attestation</h3>"));
        Assert.That(body, Does.Contain("complete KPI attestation"));
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
            ApiReleaseType = ApiReleaseType.GA
        };

        var body = new NewReleasePlanEmail(releasePlan).Body;

        Assert.That(body, Does.Contain("Please use azsdk agent to generate the SDK pull requests"));
        Assert.That(body, Does.Not.Contain("SDK pull requests will be auto generated"));
    }
}
