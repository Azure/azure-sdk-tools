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
using Microsoft.Extensions.Logging.Abstractions;

const string TestRecipient = "gaoh@microsoft.com";
const string OfflineEndpoint = "https://unit-test.invalid/email";
const string NoSdkReason = "No SDKs are recorded as released, and no SDK pull requests are linked to this Release Plan.";

var outputIndex = Array.IndexOf(args, "--output");
if (outputIndex < 0 || outputIndex + 1 >= args.Length)
{
    throw new ArgumentException("An output directory is required.");
}
var send = args.Contains("--send-test-emails", StringComparer.Ordinal);
var output = Path.GetFullPath(args[outputIndex + 1]);
Directory.CreateDirectory(output);
var now = DateTimeOffset.UtcNow;
var plan = new ReleasePlanWorkItem
{
    WorkItemId = 17192001,
    ReleasePlanId = 17192001,
    IsTestReleasePlan = true,
    ApiReleaseType = ApiReleaseType.GA,
    SDKReleaseMonth = now.AddMonths(-2).ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture),
    Title = "Synthetic verification fixture — not an Azure DevOps work item",
    ProductName = "Synthetic #17192 email test",
    Owner = "Helen Gao (test recipient)",
    IsDataPlane = true,
    ReleasePlanSubmittedByEmail = TestRecipient
};
EmailPayload[] emails =
[
    new TestEmail(new OverdueReleasePlanEmail(plan, true, NoSdkReason, now), TestRecipient),
    new TestEmail(new PastDueReleasePlanEmail(plan, NoSdkReason, now), TestRecipient)
];

var checks = new List<string>();
foreach (var (email, index) in emails.Select((email, index) => (email, index)))
{
    Require(email.EmailTo.SequenceEqual([TestRecipient]) && email.CC.Count == 0, "recipient-isolated");
    Require(email.Subject.StartsWith("[TEST ONLY #17192]", StringComparison.Ordinal), "subject-marked");
    Require(email.Body.Contains("NO RELEASE PLAN WAS CHANGED", StringComparison.Ordinal), "body-marked");
    Require(email.Body.Contains("https://aka.ms/azsdkdocs/release-plans", StringComparison.Ordinal), "documentation-link");
    Require(email.Body.Contains("https://aka.ms/azsdk/agent", StringComparison.Ordinal), "agent-link");
    Require(email.Body.Contains("Azure SDK Tools Agent Teams channel", StringComparison.Ordinal), "support-link");
    Require(!email.Body.Contains("[ReleasePlanId]", StringComparison.Ordinal), "named-values-interpolated");
    await File.WriteAllTextAsync(Path.Combine(output, index == 0 ? "test-reminder.html" : "test-confirmation.html"), email.Body);
}
checks.Add("Two shipped templates rendered; synthetic data, single test recipient, no CCs.");

// The real notification service is used below with an in-memory HTTP boundary.
// None of these negative cases can access a real email endpoint.
await CheckOutcome("disabled-configuration", string.Empty, [], [], NotificationStatus.Disabled, expectedCalls: 0);
await CheckOutcome("missing-recipient", OfflineEndpoint, [], [], NotificationStatus.SkippedNoRecipients, expectedCalls: 0);
await CheckOutcome("unsupported-recipient", OfflineEndpoint, ["someone@example.invalid"], [], NotificationStatus.SkippedNoRecipients, expectedCalls: 0);
await CheckOutcome("http-failure", OfflineEndpoint, [TestRecipient], [], NotificationStatus.Failed, expectedCalls: 1, HttpStatusCode.ServiceUnavailable);
await CheckOutcome("accepted-not-delivered", OfflineEndpoint, [TestRecipient, TestRecipient], ["external@example.invalid"], NotificationStatus.Sent, expectedCalls: 1, HttpStatusCode.Accepted);

using (var cancellation = new CancellationTokenSource())
using (var handler = new CapturingHandler(HttpStatusCode.Accepted, cancel: cancellation))
using (var client = new HttpClient(handler))
{
    var service = CreateService(client, OfflineEndpoint);
    var canceled = false;
    try
    {
        await service.SendEmailNotificationAsync(emails[0], cancellation.Token);
    }
    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
    {
        canceled = true;
    }
    Require(canceled && handler.Calls == 1, "cancellation-propagates");
    checks.Add("cancellation-propagates: passed with fake transport.");
}

var results = new List<object>();
await SaveEvidence();
if (send)
{
    var endpoint = Environment.GetEnvironmentVariable(Constants.NOTIFICATION_SERVICE_URL_ENV_VAR);
    if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
    {
        throw new InvalidOperationException("The HTTPS emailer secret is unavailable. No test messages were attempted.");
    }
    // Do not log the endpoint or raw transport exception. The SAS remains in process memory only.
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
    var service = CreateService(client, endpoint!);
    foreach (var (email, index) in emails.Select((email, index) => (email, index)))
    {
        var result = await service.SendEmailNotificationAsync(email);
        results.Add(new
        {
            kind = index == 0 ? "reminder" : "abandonment-confirmation",
            subject = email.Subject,
            recipient = TestRecipient,
            status = result.Status.ToString(),
            inboxReceiptVerified = false
        });
        await SaveEvidence();
        if (result.Status != NotificationStatus.Sent)
        {
            throw new InvalidOperationException("The emailer did not accept a test message. See sanitized evidence; do not blindly resend.");
        }
    }
}
Console.WriteLine($"Offline checks passed. Test messages accepted: {results.Count}; inbox receipt requires recipient confirmation.");
return;

async Task SaveEvidence()
{
    var evidence = new
    {
        sourceRelease = "azsdk_0.6.51",
        sourceCommit = "4cc60ce976c9549e78c0824a56d757c086eb94f9",
        recordedUtc = DateTimeOffset.UtcNow,
        syntheticFixtureOnly = true,
        workItemWrites = 0,
        productionOwnerEmails = 0,
        pipelineBuildId = Environment.GetEnvironmentVariable("BUILD_BUILDID"),
        offlineChecks = checks,
        sendRequested = send,
        emailResults = results,
        inboxReceiptVerified = false
    };
    await File.WriteAllTextAsync(Path.Combine(output, "email-verification.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
}

async Task CheckOutcome(string name, string endpoint, List<string> to, List<string> cc, NotificationStatus expected, int expectedCalls,
    HttpStatusCode responseStatus = HttpStatusCode.OK)
{
    using var handler = new CapturingHandler(responseStatus);
    using var client = new HttpClient(handler);
    var payload = new TestEmail(new OverdueReleasePlanEmail(plan, true, NoSdkReason, now), TestRecipient) { EmailTo = to, CC = cc };
    var result = await CreateService(client, endpoint).SendEmailNotificationAsync(payload);
    Require(result.Status == expected && handler.Calls == expectedCalls, name);
    if (handler.Body is { } body)
    {
        using var json = JsonDocument.Parse(body);
        Require(json.RootElement.GetProperty("EmailTo").GetString() == TestRecipient, $"{name}-recipient");
        Require(json.RootElement.GetProperty("CC").GetString() == string.Empty, $"{name}-cc");
    }
    checks.Add($"{name}: passed with fake transport.");
}

static NotificationService CreateService(HttpClient client, string endpoint) =>
    new(new SingleClientFactory(client), new FixedEnvironment(endpoint), NullLogger<NotificationService>.Instance);

static void Require(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Verification failed: {name}.");
    }
}

internal sealed class TestEmail : EmailPayload
{
    private readonly EmailPayload _inner;

    public TestEmail(EmailPayload inner, string recipient)
    {
        _inner = inner;
        EmailTo = [recipient];
        CC = [];
    }

    public override string Subject => $"[TEST ONLY #17192] {_inner.Subject}";
    public override string Body => _inner.Body.Replace("<body>", "<body><p><strong>TEST ONLY — SYNTHETIC DATA. NO RELEASE PLAN WAS CHANGED. This is a delivery/rendering test, not an actual abandonment notice.</strong></p>", StringComparison.Ordinal);
}

internal sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}

internal sealed class FixedEnvironment(string endpoint) : IEnvironmentHelper
{
    public Dictionary<string, string> GetEnvironmentVariables() => [];
    public bool GetBooleanVariable(string name, bool defaultValue = false) => defaultValue;
    public string GetStringVariable(string name, string defaultValue = "") =>
        name == Constants.NOTIFICATION_SERVICE_URL_ENV_VAR ? endpoint : defaultValue;
}

internal sealed class CapturingHandler(HttpStatusCode status, CancellationTokenSource? cancel = null) : HttpMessageHandler
{
    public int Calls { get; private set; }
    public string? Body { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        Body = await request.Content!.ReadAsStringAsync(ct);
        cancel?.Cancel();
        ct.ThrowIfCancellationRequested();
        return new HttpResponseMessage(status) { Content = new StringContent("Synthetic response") };
    }
}