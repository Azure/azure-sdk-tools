// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Notification;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Moq;
using Moq.Protected;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.ReleasePlan;

[TestFixture]
public class PackageReleaseCompletionNotificationTests
{
    private Mock<IDevOpsService> _devOps = null!;
    private Mock<IEnvironmentHelper> _environment = null!;
    private Mock<HttpMessageHandler> _handler = null!;
    private HttpClient _client = null!;
    private ReleasePlanWorkItem _plan = null!;
    private PackageReleaseStatusTool _tool = null!;
    private readonly List<string> _emails = [];
    private bool _failCompletion;

    [SetUp]
    public void Setup()
    {
        _emails.Clear();
        _failCompletion = false;
        _plan = new ReleasePlanWorkItem
        {
            WorkItemId = 12345,
            ReleasePlanId = 100,
            Revision = 7,
            Status = "In Progress",
            Title = "Contoso release",
            Owner = "Test Owner",
            ReleasePlanSubmittedByEmail = "owner@microsoft.com",
            IsTestReleasePlan = bool.TryParse(Environment.GetEnvironmentVariable("AZSDKTOOLS_AGENT_TESTING"), out var testing) && testing,
            ApiReleaseType = ApiReleaseType.GA,
            IsManagementPlane = true,
            APISpecProjectPath = "specification/contoso/project",
            SpecAPIVersion = "2026-11-01",
            SDKInfo = new[] { ".NET", "Java", "JavaScript", "Python", "Go" }.Select(language => new SDKInfo
            {
                Language = language,
                PackageName = $"{language}-package",
                ReleasedVersion = "1.0.0",
                ReleaseStatus = language == "Python" ? "Pending" : "Released"
            }).ToList()
        };
        _devOps = new Mock<IDevOpsService>();
        _devOps.Setup(s => s.GetReleasePlanAsync(100, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(() => _plan);
        _devOps.Setup(s => s.GetReleasePlansForPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _plan.Status == "In Progress" ? [_plan] : []);
        _devOps.Setup(s => s.GetReleasePlanForWorkItemAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _plan);
        _devOps.Setup(s => s.GetActiveReleasePlansByTypeSpecProjectPathAsync(It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _devOps.Setup(s => s.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<int, Dictionary<string, string>, int, CancellationToken>((_, fields, _, _) => Task.FromResult(ApplyUpdate(fields, refreshVersion: true)));
        _devOps.Setup(s => s.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Returns<int, Dictionary<string, string>, CancellationToken>((_, fields, _) => Task.FromResult(ApplyUpdate(fields, refreshVersion: false)));

        _devOps.Setup(s => s.GetPackageWorkItemAsync("Java-package", "Java", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageWorkitemResponse
            {
                Fields = new Dictionary<string, object> { ["Custom.ServiceName"] = "Contoso Service", ["Custom.GroupId"] = "com.azure.resourcemanager" }
            });
        _devOps.Setup(s => s.GetPackageWorkItemAsync("Go-package", "Go", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("lookup failed"));

        _environment = new Mock<IEnvironmentHelper>();
        _environment.Setup(e => e.GetStringVariable(Constants.NOTIFICATION_SERVICE_URL_ENV_VAR, It.IsAny<string>()))
            .Returns("https://notifications.example.com/send");
        _handler = new Mock<HttpMessageHandler>();
        _handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (request, ct) =>
            {
                Assert.That(_plan.Status, Is.EqualTo("Finished"), "Send only after the Finished update succeeds.");
                Assert.That(request.RequestUri!.AbsoluteUri, Is.EqualTo("https://notifications.example.com/send"));
                _emails.Add(await request.Content!.ReadAsStringAsync(ct));
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
        _client = new HttpClient(_handler.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_client);
        var notifications = new NotificationService(factory.Object, _environment.Object, new TestLogger<NotificationService>());
        _tool = new PackageReleaseStatusTool(_devOps.Object, new TestLogger<PackageReleaseStatusTool>(), notifications);
    }

    [TearDown]
    public void TearDown() => _client.Dispose();

    [Test]
    public async Task FinishedPlan_EmailsOwnerOnlyForPublicReleases(
        [Values(false, true)] bool legacy,
        [Values(false, true)] bool management,
        [Values(ApiReleaseType.PublicPreview, ApiReleaseType.GA, ApiReleaseType.PrivatePreview, ApiReleaseType.Unknown)] ApiReleaseType releaseType)
    {
        _plan.ApiReleaseType = releaseType;
        _plan.IsManagementPlane = management;
        _plan.IsDataPlane = !management;

        var result = await UpdateAsync(legacy);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.ReleasePlanFinished, Is.True);
        if (releaseType is ApiReleaseType.PrivatePreview or ApiReleaseType.Unknown)
        {
            Assert.That(_emails, Is.Empty);
            return;
        }

        Assert.That(_emails, Has.Count.EqualTo(1));
        using var email = JsonDocument.Parse(_emails[0]);
        Assert.That(email.RootElement.GetProperty("EmailTo").GetString(), Is.EqualTo("owner@microsoft.com"));
        Assert.That(email.RootElement.GetProperty("CC").GetString(), Is.Empty);
        var body = email.RootElement.GetProperty("Body").GetString()!;
        Assert.That(body, Does.Contain("<td>Python-package</td><td>2.0.0</td>"),
            "The final package version must not come from the stale pre-release snapshot.");
        Assert.That(body.Contains("https://forms.cloud.microsoft/r/rQMr2bc7DT"), Is.EqualTo(management));
        Assert.That(body, Does.Contain("<td>Contoso Service / Not recorded</td>"));
        Assert.That(body, Does.Contain("https://central.sonatype.com/artifact/com.azure.resourcemanager/Java-package/1.0.0"));
        Assert.That(body, Does.Contain("<td>Go-package</td><td>1.0.0</td><td>Not recorded</td>"),
            "A failed package lookup must not block the email.");
    }

    [Test]
    public async Task MissingEmailerUri_DoesNotSend(
        [Values(false, true)] bool legacy, [Values(null, "", "   ")] string? uri)
    {
        _environment.Setup(e => e.GetStringVariable(Constants.NOTIFICATION_SERVICE_URL_ENV_VAR, It.IsAny<string>())).Returns(uri!);

        var result = await UpdateAsync(legacy);

        Assert.That(result.ReleasePlanFinished, Is.True);
        Assert.That(result.Warnings, Is.Null.Or.Empty);
        _handler.Protected().Verify("SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Test]
    public async Task AutomaticSdkPullRequest_SendsCompletionEmail()
    {
        const string sdkPullRequest = "https://github.com/Azure/azure-sdk-for-python/pull/42";
        _plan.SDKInfo.Single(s => s.Language == "Python").SdkPullRequestUrl = sdkPullRequest;
        _devOps.Setup(s => s.GetReleasePlansBySdkPullRequestAsync(sdkPullRequest, "Python", _plan.IsTestReleasePlan, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_plan]);

        var result = await _tool.UpdatePackageReleaseStatus(
            "Python-package", "Python", "Released", "2.0.0", sdkPullRequest: sdkPullRequest);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.ReleasePlanFinished, Is.True);
        Assert.That(_emails, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task MissingOwnerEmail_WarnsWithoutSendingToAnyoneElse()
    {
        _plan.ReleasePlanSubmittedByEmail = "";

        var result = await UpdateAsync(legacy: false);

        Assert.That(result.ReleasePlanFinished, Is.True);
        Assert.That(result.Warnings, Has.Some.Contains("No valid release plan owner email address"));
        Assert.That(_emails, Is.Empty);
    }

    [Test]
    public async Task IncompletePlan_DoesNotSend([Values(false, true)] bool legacy)
    {
        _plan.SDKInfo.Single(s => s.Language == "Java").ReleaseStatus = "Pending";

        var result = await UpdateAsync(legacy);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.ReleasePlanFinished, Is.False);
        Assert.That(_emails, Is.Empty);
    }

    [Test]
    public async Task FailedFinishedWrite_DoesNotSend_AndRetrySendsOnce([Values(false, true)] bool legacy)
    {
        _failCompletion = true;
        var initial = await UpdateAsync(legacy);
        Assert.That(initial.ReleasePlanFinished, Is.False);
        Assert.That(_emails, Is.Empty);

        _failCompletion = false;
        var retry = await UpdateAsync(legacy);
        Assert.That(retry.ReleasePlanFinished, Is.True);
        Assert.That(_emails, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task AlreadyFinishedRetry_DoesNotResend([Values(false, true)] bool legacy)
    {
        await UpdateAsync(legacy);
        await UpdateAsync(legacy);

        Assert.That(_emails, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DeliveryFailure_IsWarningWithoutUndoingCompletion([Values(false, true)] bool legacy)
    {
        _handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var result = await UpdateAsync(legacy);

        Assert.That(result.ResponseError, Is.Null);
        Assert.That(result.ReleasePlanFinished, Is.True);
        Assert.That(_plan.Status, Is.EqualTo("Finished"));
        Assert.That(result.Warnings, Has.Some.Contains("completion email was not sent"));
        Assert.That(result.Warnings, Has.Some.Contains("503"));
    }

    private Task<ReleaseStatusUpdateResponse> UpdateAsync(bool legacy) =>
        _tool.UpdatePackageReleaseStatus("Python-package", "Python", "Released", "2.0.0", legacy ? 0 : 100);

    private WorkItem ApplyUpdate(Dictionary<string, string> fields, bool refreshVersion)
    {
        if (fields.TryGetValue("System.State", out var state))
        {
            if (_failCompletion)
            {
                throw new InvalidOperationException("Finished update failed.");
            }
            _plan.Status = state;
        }
        foreach (var sdk in _plan.SDKInfo)
        {
            var languageId = DevOpsService.MapLanguageToId(sdk.Language);
            if (fields.TryGetValue($"Custom.ReleaseStatusFor{languageId}", out var status))
            {
                sdk.ReleaseStatus = status;
            }
            // Legacy lookups must populate the new version in memory themselves.
            if (fields.TryGetValue($"Custom.ReleasedVersionFor{languageId}", out var version) &&
                refreshVersion)
            {
                sdk.ReleasedVersion = version;
            }
        }
        return new WorkItem { Id = _plan.WorkItemId, Rev = ++_plan.Revision };
    }
}
