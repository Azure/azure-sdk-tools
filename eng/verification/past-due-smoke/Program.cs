using System.Globalization;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Services.Notification.Templates;

if (args.Length != 1)
{
    throw new ArgumentException("Supply the output directory for synthetic reminder HTML.");
}

var outputDirectory = Path.GetFullPath(args[0]);
Directory.CreateDirectory(outputDirectory);
var now = DateTimeOffset.UtcNow;
var scenarios = new (string Name, ApiReleaseType ReleaseType, bool? Inactive, string? Reason)[]
{
    ("recent-plan", ApiReleaseType.GA, true, "No SDKs are recorded as released, and no SDK pull requests are linked to this Release Plan."),
    ("unapproved-sdk-work", ApiReleaseType.PublicPreview, true, "No SDKs are recorded as released, no linked SDK pull request has been merged, and all open SDK pull requests are awaiting approval."),
    ("private-preview", ApiReleaseType.PrivatePreview, true, "No specification pull request is linked to this Release Plan."),
    ("protected-work", ApiReleaseType.GA, false, "At least one SDK is recorded as released."),
    ("unverified-work", ApiReleaseType.GA, null, null)
};
var rendered = new List<object>();
foreach (var scenario in scenarios)
{
    var plan = new ReleasePlanWorkItem
    {
        WorkItemId = 17552001 + rendered.Count,
        ReleasePlanId = 17552001 + rendered.Count,
        ApiReleaseType = scenario.ReleaseType,
        SDKReleaseMonth = now.AddMonths(-2).ToString("MMMM yyyy", CultureInfo.InvariantCulture),
        ChangedDate = now.AddDays(-1).UtcDateTime,
        Owner = "Synthetic smoke-test owner",
        ProductName = "Synthetic smoke-test service",
        IsTestReleasePlan = true,
        ReleasePlanSubmittedByEmail = string.Empty
    };
    var template = new OverdueReleasePlanEmail(plan, scenario.Inactive, scenario.Reason, now);
    var body = template.Body;
    if (template.EmailTo.Any() || template.CC.Any()
        || body.Contains("is eligible for automatic abandonment now", StringComparison.Ordinal)
        || body.Contains("this Release Plan will be marked", StringComparison.Ordinal)
        || body.Contains("editing other details alone does not", StringComparison.Ordinal)
        || !body.Contains("Automatic abandonment also requires verified inactivity for the configured window.", StringComparison.Ordinal)
        || !body.Contains("Recent release-plan edits, SDK PR commits, submitted reviews, or human comments can postpone automatic abandonment.", StringComparison.Ordinal)
        || !body.Contains("Update the target release month for Release Plan", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Reminder safety assertion failed for {scenario.Name}.");
    }
    if (scenario.Inactive == true && !body.Contains("the inactivity checks also pass", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Conditional abandonment notice missing for {scenario.Name}.");
    }
    var fileName = scenario.Name + ".html";
    File.WriteAllText(Path.Combine(outputDirectory, fileName), "<!-- Synthetic smoke test. Not sent. -->\n" + body);
    rendered.Add(new { scenario = scenario.Name, html = fileName, subject = template.Subject, recipients = 0 });
}

File.WriteAllText(Path.Combine(outputDirectory, "rendered-reminders.json"),
    JsonSerializer.Serialize(new { synthetic = true, sendInvoked = false, rendered }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"PASS: rendered {rendered.Count} synthetic reminders with zero recipients and no send operation.");