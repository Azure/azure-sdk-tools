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

    private const string NoLinkedSdkReason = "No SDKs are recorded as released, and no SDK pull requests are linked to this Release Plan.";
    private static readonly DateTimeOffset MaintenanceSentAt = new(2027, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private static ReleasePlanWorkItem CreateMaintenancePlan(ApiReleaseType releaseType = ApiReleaseType.GA) => new()
    {
        WorkItemId = 100,
        ReleasePlanId = 42,
        ApiReleaseType = releaseType,
        SDKReleaseMonth = "December 2026",
        Owner = "Test Owner",
        ProductName = "Contoso",
        Title = "Fallback service",
        ReleasePlanSubmittedByEmail = "owner@microsoft.com"
    };

    private static EmailPayload CreateMaintenanceEmail(bool abandoned, ReleasePlanWorkItem plan) =>
        abandoned
            ? new PastDueReleasePlanEmail(plan, NoLinkedSdkReason, MaintenanceSentAt)
            : new OverdueReleasePlanEmail(plan, true, NoLinkedSdkReason, MaintenanceSentAt);

    private static DateTimeOffset ParseMaintenanceTime(string value) =>
        DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    [Test]
    public void MaintenanceEmail_UsesExpectedSubjectDashboardAndSharedContent(
        [Values(false, true)] bool abandoned,
        [Values(42, 0, -1)] int releasePlanId,
        [Values(false, true)] bool testPlan,
        [Values(false, true)] bool managementPlane)
    {
        var plan = CreateMaintenancePlan();
        plan.ReleasePlanId = releasePlanId;
        plan.IsTestReleasePlan = testPlan;
        plan.IsManagementPlane = managementPlane;
        var id = releasePlanId > 0 ? releasePlanId : plan.WorkItemId;
        var url = (testPlan ? ReleasePlanWorkItem.DashboardBaseUrlTest : ReleasePlanWorkItem.DashboardBaseUrl) + id;
        var template = CreateMaintenanceEmail(abandoned, plan);
        var body = template.Body;
        const string supportUrl = "https://teams.microsoft.com/l/channel/19%3A6d2c19322c254a80bcc521675134da03%40thread.skype/AzSDK%20Tools%20Agent?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&amp;tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47";

        Assert.Multiple(() =>
        {
            Assert.That(template.Subject, Is.EqualTo(abandoned
                ? $"Your Azure SDK Release Plan ({id}) has been abandoned"
                : $"Your Azure SDK Release Plan ({id}) is now past due"));
            Assert.That(body, Does.Contain($"<a href=\"{url}\">Azure SDK Release Plan {id}</a>"));
            Assert.That(body, Does.Contain($"<li><strong>Release Plan:</strong> <a href=\"{url}\">{id}</a></li>"));
            Assert.That(body, Does.Contain("<p>Hello Test Owner,</p>"));
            Assert.That(body, Does.Contain("<li><strong>Service:</strong> Contoso</li>"));
            Assert.That(body, Does.Contain("<li><strong>Release type:</strong> GA</li>"));
            Assert.That(body, Does.Contain($"<li><strong>SDK plane:</strong> {(managementPlane ? "Management (ARM) plane" : "Data plane")}</li>"));
            Assert.That(body, Does.Contain("<li><strong>Target release month:</strong> December 2026</li>"));
            Assert.That(body, Does.Contain("A Release Plan is a guided workflow"));
            Assert.That(body, Does.Contain("<a href=\"https://aka.ms/azsdkdocs/release-plans\">Learn more about Release Plans.</a>"));
            Assert.That(body, Does.Contain("<a href=\"https://aka.ms/azsdk/agent\">Azure SDK Tools Agent</a>"));
            Assert.That(body, Does.Contain($"<a href=\"{supportUrl}\">Azure SDK Tools Agent Teams channel</a>"));
            Assert.That(body, Does.Not.Contain("&tenantId="));
            Assert.That(body, Does.Contain("Please include your Release Plan ID."));
            Assert.That(body, Does.Contain("<p>Best regards,</p>").And.Contain("<p>Azure SDK Team</p>"));
            Assert.That(body, Does.Not.Match(@"\[[A-Za-z][A-Za-z0-9_ ]*\]|\{[^}]+\}"));
            Assert.That(template.CC, Is.Empty);
        });
    }

    [Test]
    public void MaintenanceEmail_OnlyTargetsTheSuppliedSubmitter(
        [Values(false, true)] bool abandoned,
        [Values(null, "", " \t ", "owner@microsoft.com", "external@example.com")] string? submitter)
    {
        var plan = CreateMaintenancePlan();
        plan.IsManagementPlane = true;
        plan.ReleasePlanSubmittedByEmail = submitter!;

        var template = CreateMaintenanceEmail(abandoned, plan);

        Assert.That(template.EmailTo, Is.EqualTo(string.IsNullOrWhiteSpace(submitter) ? Array.Empty<string>() : new[] { submitter }));
        Assert.That(template.CC, Is.Empty);
    }

    [TestCase(false, true)]
    [TestCase(false, false)]
    [TestCase(true, true)]
    public void MaintenanceEmail_EncodesValuesAndPreservesTheExactReason(bool abandoned, bool inactive)
    {
        var plan = CreateMaintenancePlan();
        plan.Owner = "Owner <admin> & \"reviewer\"";
        plan.ProductName = "Product <script> & 'service'";
        plan.SDKReleaseMonth = "December <2026> & \"target\"";
        const string reason = "SDK <approval> & \"merge\" details are unresolved.";

        var body = abandoned
            ? new PastDueReleasePlanEmail(plan, reason, MaintenanceSentAt).Body
            : new OverdueReleasePlanEmail(plan, inactive, reason, MaintenanceSentAt).Body;

        Assert.Multiple(() =>
        {
            foreach (var value in new[] { plan.Owner, plan.ProductName, plan.SDKReleaseMonth, reason })
            {
                Assert.That(body, Does.Contain(WebUtility.HtmlEncode(value)));
                Assert.That(body, Does.Not.Contain(value));
                Assert.That(body, Does.Not.Contain(WebUtility.HtmlEncode(WebUtility.HtmlEncode(value))));
            }
        });
    }

    [Test]
    public void MaintenanceEmail_MissingProductName_UsesEncodedTitle(
        [Values(false, true)] bool abandoned,
        [Values(null, "", "   ")] string? productName)
    {
        var plan = CreateMaintenancePlan();
        plan.ProductName = productName!;
        plan.Title = "Fallback <service> & \"product\"";

        var body = CreateMaintenanceEmail(abandoned, plan).Body;

        Assert.That(body, Does.Contain($"<li><strong>Service:</strong> {WebUtility.HtmlEncode(plan.Title)}</li>"));
        Assert.That(body, Does.Not.Contain(plan.Title));
    }

    private static IEnumerable<TestCaseData> InactiveReleaseWorkCases()
    {
        foreach (var releaseType in new[] { ApiReleaseType.GA, ApiReleaseType.PublicPreview })
        {
            foreach (var (name, reason) in new[]
            {
                ("NoPr", NoLinkedSdkReason),
                ("AllClosed", "No SDKs are recorded as released, and all linked SDK pull requests are closed without being merged."),
                ("UnapprovedOpen", "No SDKs are recorded as released, no linked SDK pull request has been merged, and all open SDK pull requests are awaiting approval.")
            })
            {
                yield return new TestCaseData(releaseType, reason).SetName($"MaintenanceEmails_ReuseAssessmentReason_{releaseType}_{name}");
            }
        }
        yield return new TestCaseData(ApiReleaseType.PrivatePreview, "No specification pull request is linked to this Release Plan.")
            .SetName("MaintenanceEmails_ReuseAssessmentReason_PrivatePreview_MissingSpec");
        yield return new TestCaseData(ApiReleaseType.PrivatePreview, "The specification pull request linked to this Private Preview Release Plan has not been merged.")
            .SetName("MaintenanceEmails_ReuseAssessmentReason_PrivatePreview_UnmergedSpec");
    }

    [TestCaseSource(nameof(InactiveReleaseWorkCases))]
    public void MaintenanceEmails_ReuseAssessmentReasonWithoutInferringWorkState(ApiReleaseType releaseType, string reason)
    {
        // The templates must render the supplied assessment, not infer activity from SDKInfo.
        var plan = CreateMaintenancePlan(releaseType);
        var reminder = new OverdueReleasePlanEmail(plan, true, reason, MaintenanceSentAt);
        var confirmation = new PastDueReleasePlanEmail(plan, reason, MaintenanceSentAt);

        Assert.Multiple(() =>
        {
            Assert.That(reminder.Body, Does.Contain($"<strong>Why you are receiving this reminder:</strong> {WebUtility.HtmlEncode(reason)}</p>"));
            Assert.That(confirmation.Body, Does.Contain(WebUtility.HtmlEncode(reason)));
            Assert.That(reminder.Body, Does.Contain("Abandon Release Plan 42"));
            Assert.That(reminder.Body, Does.Not.Contain("has been marked <strong>Abandoned</strong>"));
            if (releaseType == ApiReleaseType.PrivatePreview)
            {
                Assert.That(reminder.Body, Does.Contain("Complete the required reviews and merge the pull request."));
                Assert.That(reminder.Body, Does.Contain("opening a specification pull request is not sufficient to prevent abandonment"));
                foreach (var body in new[] { reminder.Body, confirmation.Body })
                {
                    Assert.That(body, Does.Not.Contain("publish the remaining SDKs"));
                    Assert.That(body, Does.Not.Contain("generating or locating the required SDK pull requests"));
                    Assert.That(body, Does.Not.Contain("no SDK is recorded as released"));
                }
            }
            else
            {
                Assert.That(reminder.Body, Does.Contain("no SDK is recorded as released, no linked SDK pull request is merged, and no open SDK pull request is approved"));
                Assert.That(reminder.Body, Does.Contain("Review and merge them, then follow the dashboard to publish the remaining SDKs"));
            }
        });
    }

    [TestCase(ApiReleaseType.GA, "A linked SDK pull request is approved but has not yet been merged.")]
    [TestCase(ApiReleaseType.GA, "A linked SDK pull request has been merged.")]
    [TestCase(ApiReleaseType.GA, "At least one SDK is recorded as released.")]
    [TestCase(ApiReleaseType.PublicPreview, "A linked SDK pull request is approved but has not yet been merged.")]
    [TestCase(ApiReleaseType.PublicPreview, "A linked SDK pull request has been merged.")]
    [TestCase(ApiReleaseType.PublicPreview, "At least one SDK is recorded as released.")]
    [TestCase(ApiReleaseType.PrivatePreview, "The specification pull request linked to this Private Preview Release Plan has been merged.")]
    public void OverdueReleasePlanEmail_ProtectedWork_StatesReasonWithoutAnEligibilityDeadline(ApiReleaseType releaseType, string reason)
    {
        var body = new OverdueReleasePlanEmail(CreateMaintenancePlan(releaseType), false, reason, MaintenanceSentAt).Body;

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain(WebUtility.HtmlEncode(reason)));
            Assert.That(body, Does.Contain("not currently eligible for automatic abandonment"));
            Assert.That(body, Does.Not.Contain("is eligible for automatic abandonment now"));
            Assert.That(body, Does.Not.Contain("Please take action before"));
            Assert.That(body, Does.Not.Contain("Starting with the"));
            Assert.That(body, Does.Not.Contain("this Release Plan will be marked"));
            Assert.That(body, Does.Contain(releaseType == ApiReleaseType.PrivatePreview
                ? "A merged specification pull request prevents automatic abandonment for a Private Preview release."
                : "an approved open or merged SDK pull request, or any SDK recorded as released, prevents automatic abandonment."));
            if (releaseType == ApiReleaseType.PrivatePreview)
            {
                Assert.That(body, Does.Not.Contain("publish the remaining SDKs"));
            }
        });
    }

    [TestCase("GA", false)]
    [TestCase("APEX GA", false)]
    [TestCase("APEX Public Preview", false)]
    [TestCase("APEX Private Preview", false)]
    [TestCase("", true)]
    [TestCase("Unrecognized <release type>", true)]
    public void OverdueReleasePlanEmail_UnverifiedWork_DistinguishesUnknownTypeFromGitHubFailure(string recordedType, bool unknownType)
    {
        var plan = CreateMaintenancePlan();
        plan.ReleasePlanType = recordedType;
        var body = new OverdueReleasePlanEmail(plan, null, null, MaintenanceSentAt).Body;
        var staleReasonBody = new OverdueReleasePlanEmail(plan, null, "Confirmed inactivity <stale assessment>", MaintenanceSentAt).Body;

        Assert.Multiple(() =>
        {
            Assert.That(staleReasonBody, Is.EqualTo(body));
            Assert.That(body, Does.Contain(unknownType
                ? "The recorded release type is missing or unrecognized."
                : "We could not retrieve or validate the current status or approval of one or more linked GitHub pull requests."));
            Assert.That(body, Does.Contain(unknownType ? "Confirm the release type:" : "Check the linked pull requests:"));
            Assert.That(body, Does.Not.Contain(unknownType
                ? "We could not retrieve or validate"
                : "The recorded release type is missing or unrecognized"));
            Assert.That(body, Does.Contain("This verification result does not establish that your Release Plan is inactive."));
            Assert.That(body, Does.Contain("Automatic abandonment requires a successful eligibility check."));
            Assert.That(body, Does.Contain("Update the target release month for Release Plan 42"));
            Assert.That(body, Does.Not.Contain("Abandon Release Plan"));
            Assert.That(body, Does.Not.Contain("If the release is no longer needed:"));
            Assert.That(body, Does.Not.Contain("Complete the required reviews and merge the pull request"));
            Assert.That(body, Does.Not.Contain("publish the remaining SDKs"));
            Assert.That(body, Does.Not.Contain("is eligible for automatic abandonment now"));
            Assert.That(body, Does.Not.Contain("Please take action before"));
            Assert.That(body, Does.Not.Contain("has been marked <strong>Abandoned</strong>"));
            if (unknownType)
            {
                Assert.That(body, Does.Contain("<li><strong>Release type:</strong> Unrecognized</li>"));
            }
            else
            {
                Assert.That(body, Does.Contain("This is a verification issue, not confirmation that your release is inactive."));
            }
        });
    }

    [TestCase("December 2026", "2027-01-15T12:00:00Z", "January 2027", "February 1, 2027", "February 2027")]
    [TestCase("Dec 2026", "2027-01-31T23:59:00Z", "January 2027", "February 1, 2027", "February 2027")]
    [TestCase("September 2026", "2026-10-15T12:00:00Z", "October 2026", "November 1, 2026", "November 2026")]
    [TestCase("January 2027", "2027-02-15T12:00:00Z", "February 2027", "March 1, 2027", "March 2027")]
    public void OverdueReleasePlanEmail_FutureDeadline_FollowsTargetMonthAndFullGraceMonth(
        string targetMonth, string sentAt, string graceMonth, string deadline, string cleanupMonth)
    {
        var plan = CreateMaintenancePlan();
        plan.SDKReleaseMonth = targetMonth;

        var body = new OverdueReleasePlanEmail(plan, true, NoLinkedSdkReason, ParseMaintenanceTime(sentAt)).Body;

        Assert.That(body, Does.Contain($"Please take action before {deadline}."));
        Assert.That(body, Does.Contain($"{graceMonth} is the grace month for the {targetMonth} target."));
        Assert.That(body, Does.Contain($"Starting with the {cleanupMonth} monthly cleanup"));
        Assert.That(body, Does.Contain("Setting a future target release month moves the cleanup eligibility date; editing other details alone does not."));
        Assert.That(body, Does.Not.Contain("eligible for automatic abandonment now"));
    }

    [TestCase("2027-02-01T00:00:00Z", true)]
    [TestCase("2027-03-15T12:00:00Z", true)]
    [TestCase("2027-02-01T00:30:00+02:00", false)]
    [TestCase("2027-01-31T23:30:00-02:00", true)]
    public void OverdueReleasePlanEmail_EligibilityUsesUtcDate_AndNeverGivesAPastActionDeadline(string sentAt, bool eligibleNow)
    {
        var body = new OverdueReleasePlanEmail(CreateMaintenancePlan(), true, NoLinkedSdkReason, ParseMaintenanceTime(sentAt)).Body;
        const string nowMessage = "This Release Plan is eligible for automatic abandonment now. Please take action now.";

        if (eligibleNow)
        {
            Assert.That(body, Does.Contain(nowMessage));
            Assert.That(body, Does.Not.Contain("Please take action before"));
        }
        else
        {
            Assert.That(body, Does.Contain("Please take action before February 1, 2027."));
            Assert.That(body, Does.Not.Contain(nowMessage));
        }
    }

    [TestCase("2026-12-15T12:00:00Z", "January 2027")]
    [TestCase("2031-06-18T12:00:00Z", "July 2031")]
    [TestCase("2027-03-01T00:30:00+02:00", "March 2027")]
    [TestCase("2027-12-31T23:30:00-02:00", "February 2028")]
    public void MaintenanceEmails_SuggestTheMonthAfterTheSuppliedUtcDate(string sentAt, string futureMonth)
    {
        var plan = CreateMaintenancePlan();
        var timestamp = ParseMaintenanceTime(sentAt);

        var reminder = new OverdueReleasePlanEmail(plan, true, NoLinkedSdkReason, timestamp);
        var confirmation = new PastDueReleasePlanEmail(plan, NoLinkedSdkReason, timestamp);

        Assert.That(reminder.Body, Does.Contain($"Update the target release month for Release Plan 42 to {futureMonth}&rdquo;"));
        Assert.That(confirmation.Body, Does.Contain($"Help me create a new Release Plan for Contoso, targeting {futureMonth}. Use Release Plan 42 as context&rdquo;"));
    }

    [TestCase("2027-03-15T12:00:00Z", "March 15, 2027")]
    [TestCase("2027-03-01T00:30:00+02:00", "February 28, 2027")]
    [TestCase("2027-02-28T23:30:00-02:00", "March 1, 2027")]
    public void PastDueReleasePlanEmail_UsesActualSuccessfulCleanupUtcDate_NotScheduledEligibility(string abandonedAt, string actualDate)
    {
        var body = new PastDueReleasePlanEmail(CreateMaintenancePlan(), NoLinkedSdkReason, ParseMaintenanceTime(abandonedAt)).Body;

        Assert.That(body, Does.Contain($"has been marked <strong>Abandoned</strong> during the {actualDate} monthly cleanup."));
        Assert.That(body, Does.Contain("the January 2027 grace month has ended."));
        Assert.That(body, Does.Not.Contain("February 1, 2027"));
        Assert.That(body, Does.Not.Contain("Please take action before"));
    }

    [TestCase(ApiReleaseType.GA)]
    [TestCase(ApiReleaseType.PublicPreview)]
    [TestCase(ApiReleaseType.PrivatePreview)]
    public void PastDueReleasePlanEmail_ContinuingReleaseRequiresANewPlan_NotReopening(ApiReleaseType releaseType)
    {
        var body = new PastDueReleasePlanEmail(CreateMaintenancePlan(releaseType), NoLinkedSdkReason, MaintenanceSentAt).Body;

        Assert.That(body, Does.Contain("Create a <strong>new Release Plan</strong> with an updated target release month"));
        Assert.That(body, Does.Contain("Creating a new Release Plan does not reopen the abandoned Release Plan."));
        Assert.That(body, Does.Contain("No further action is required for this abandoned Release Plan."));
        Assert.That(body, Does.Not.Contain("Reopen Release Plan"));
        Assert.That(body, Does.Not.Contain("Update the target release month for Release Plan 42"));
        Assert.That(body, Does.Not.Contain("Abandon Release Plan 42"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not a month")]
    public void OverdueReleasePlanEmail_InvalidTargetMonth_DoesNotInventADeadline(string? targetMonth)
    {
        var plan = CreateMaintenancePlan();
        plan.SDKReleaseMonth = targetMonth!;

        var body = new OverdueReleasePlanEmail(plan, true, NoLinkedSdkReason, MaintenanceSentAt).Body;

        Assert.That(body, Does.Contain("The cleanup eligibility date could not be calculated."));
        Assert.That(body, Does.Not.Contain("Please take action before"));
        Assert.That(body, Does.Not.Contain("is eligible for automatic abandonment now"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MaintenanceEmail_RejectsNullPlan(bool abandoned)
    {
        Assert.Throws<ArgumentNullException>(() => CreateMaintenanceEmail(abandoned, null!));
    }

    [Test]
    public void OverdueReleasePlanEmail_KnownActivity_RequiresAReason(
        [Values(false, true)] bool inactive, [Values(null, "", " \t ")] string? reason)
    {
        var exception = Assert.Catch<ArgumentException>(() => new OverdueReleasePlanEmail(CreateMaintenancePlan(), inactive, reason, MaintenanceSentAt));

        Assert.That(exception!.ParamName, Is.EqualTo("workReason"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \t ")]
    public void PastDueReleasePlanEmail_RequiresAReason(string? reason)
    {
        var exception = Assert.Catch<ArgumentException>(() => new PastDueReleasePlanEmail(CreateMaintenancePlan(), reason!, MaintenanceSentAt));

        Assert.That(exception!.ParamName, Is.EqualTo("reason"));
    }

    [Test]
    public async Task SendEmailNotification_NoRecipients_ReturnsSkipped()
    {
        var (service, captured) = CreateService(url: ServiceUrl);

        var result = await service.SendEmailNotificationAsync(new NewReleasePlanEmail(new ReleasePlanWorkItem { ReleasePlanId = 5 }));

        Assert.That(result.Status, Is.EqualTo(NotificationStatus.SkippedNoRecipients));
        Assert.That(captured, Is.Empty);
    }

    [Test]
    public async Task SendNewReleasePlanNotification_MissingUrl_ReturnsDisabled()
    {
        var (service, captured) = CreateService(url: string.Empty);

        var result = await service.SendEmailNotificationAsync(new NewReleasePlanEmail(new ReleasePlanWorkItem { ReleasePlanId = 5 }));

        Assert.That(result.Status, Is.EqualTo(NotificationStatus.Disabled));
        Assert.That(captured, Is.Empty);
        mockHttpClientFactory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task SendEmailNotification_HttpFailure_ReturnsFailureDetails()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("service unavailable")
            });
        var service = CreateService(mockHandler.Object);

        var result = await service.SendEmailNotificationAsync(CreatePayload());

        Assert.That(result.Status, Is.EqualTo(NotificationStatus.Failed));
        Assert.That(result.ErrorMessage, Does.Contain("HTTP 500").And.Contain("service unavailable"));
    }

    [Test]
    public async Task SendEmailNotification_TransportFailure_ReturnsFailureDetails()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        var service = CreateService(mockHandler.Object);

        var result = await service.SendEmailNotificationAsync(CreatePayload());

        Assert.That(result.Status, Is.EqualTo(NotificationStatus.Failed));
        Assert.That(result.ErrorMessage, Is.EqualTo("Connection refused"));
    }

    [Test]
    public void SendEmailNotification_RequestedCancellation_Propagates()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((_, ct) => Task.Delay(Timeout.Infinite, ct)
                .ContinueWith(_ => new HttpResponseMessage(HttpStatusCode.OK), ct));
        var service = CreateService(mockHandler.Object);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.CatchAsync<OperationCanceledException>(
            async () => await service.SendEmailNotificationAsync(CreatePayload(), cts.Token));
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
        var result = await service.SendEmailNotificationAsync(payload);

        Assert.That(result.Status, Is.EqualTo(NotificationStatus.Sent));
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
        Assert.That(template.CC, Is.EqualTo(new[] { "azsdkexp@microsoft.com", "sdkreleaseowners@microsoft.com" }));
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

    private NotificationService CreateService(HttpMessageHandler handler)
    {
        mockEnvironmentHelper
            .Setup(e => e.GetStringVariable(Constants.NOTIFICATION_SERVICE_URL_ENV_VAR, It.IsAny<string>()))
            .Returns(ServiceUrl);
        mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));
        return new NotificationService(mockHttpClientFactory.Object, mockEnvironmentHelper.Object, logger);
    }

    private static EmailPayload CreatePayload() => new NewReleasePlanEmail(new ReleasePlanWorkItem
    {
        ReleasePlanId = 5,
        ReleasePlanSubmittedByEmail = "owner@microsoft.com"
    });
}
