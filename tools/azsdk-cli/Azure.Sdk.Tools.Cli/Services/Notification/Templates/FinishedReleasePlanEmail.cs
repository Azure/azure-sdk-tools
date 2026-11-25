// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Services.Notification.Templates;

/// <summary>
/// Package details recorded on a released SDK's Azure DevOps package work item.
/// </summary>
public record ReleasedPackageMetadata(string ServiceName, string GroupId);

public class FinishedReleasePlanEmail : EmailPayload
{
    private readonly ReleasePlanWorkItem _plan;
    private readonly DateTimeOffset _completedOn;
    private readonly IReadOnlyDictionary<string, ReleasedPackageMetadata> _packageMetadata;

    /// <param name="packageMetadata">Package work item details keyed by the SDK language recorded in the release plan.</param>
    public FinishedReleasePlanEmail(
        ReleasePlanWorkItem plan,
        DateTimeOffset completedOn,
        IReadOnlyDictionary<string, ReleasedPackageMetadata>? packageMetadata = null)
    {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _completedOn = completedOn;
        _packageMetadata = packageMetadata ?? new Dictionary<string, ReleasedPackageMetadata>();
        EmailTo = string.IsNullOrWhiteSpace(plan.ReleasePlanSubmittedByEmail)
            ? []
            : [plan.ReleasePlanSubmittedByEmail];
    }

    public override string Subject => $"Azure SDK release plan {_plan.ReleasePlanId} finished: {_plan.Title}";

    public override string Body =>
        $"""
        <p>Hello {Encode(_plan.Owner)},</p>
        <p>The Azure SDK release plan below is now marked <b>Finished</b>. Thank you for working with the Azure SDK team on this release.</p>
        <h3>Release plan details</h3>
        <table border="1" cellpadding="6" cellspacing="0">
          <tr><td><b>Release plan</b></td><td>{Link(_plan.ReleasePlanLink, _plan.Title)} (ID: {_plan.ReleasePlanId})</td></tr>
          <tr><td><b>Service / Product</b></td><td>{Encode(ServiceName)} / {Encode(_plan.ProductName)}</td></tr>
          <tr><td><b>Release type</b></td><td>{Encode(_plan.ApiReleaseType.ToDisplayLabel())}</td></tr>
          <tr><td><b>Plane</b></td><td>{(_plan.IsManagementPlane ? "Management plane" : "Data plane")}</td></tr>
          <tr><td><b>API version</b></td><td>{Encode(_plan.SpecAPIVersion)}</td></tr>
          <tr><td><b>API spec PR</b></td><td>{Link(SpecPullRequest, SpecPullRequest)}</td></tr>
          <tr><td><b>Release month</b></td><td>{Encode(_plan.SDKReleaseMonth)}</td></tr>
          <tr><td><b>Completed on</b></td><td>{_completedOn.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)}</td></tr>
        </table>
        <h3>Released SDKs</h3>
        <table border="1" cellpadding="6" cellspacing="0">
          <tr><th>Language</th><th>Package name</th><th>Version released</th><th>Package link</th></tr>
          {ReleasedSdkRows}
        </table>
        {Feedback}
        <p>If anything in this release looks wrong, reply to this email or contact the Azure SDK team.</p>
        <p>Thanks,<br/>Azure SDK Engineering Systems</p>
        """;

    private string SpecPullRequest => string.IsNullOrWhiteSpace(_plan.ActiveSpecPullRequest)
        ? _plan.SpecPullRequests.FirstOrDefault() ?? string.Empty
        : _plan.ActiveSpecPullRequest;

    private IEnumerable<SDKInfo> ReleasedSdks => _plan.SDKInfo
        .Where(sdk => string.Equals(sdk.ReleaseStatus, "Released", StringComparison.OrdinalIgnoreCase));

    private string ServiceName => ReleasedSdks
        .Select(sdk => GetMetadata(sdk)?.ServiceName)
        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? string.Empty;

    private string ReleasedSdkRows => string.Join(Environment.NewLine, ReleasedSdks.Select(sdk =>
    {
        var registryLink = GetPackageRegistryLink(sdk.Language, sdk.PackageName, sdk.ReleasedVersion, GetMetadata(sdk)?.GroupId);
        return $"<tr><td>{Encode(sdk.Language)}</td><td>{Encode(sdk.PackageName)}</td><td>{Encode(sdk.ReleasedVersion)}</td><td>{Link(registryLink, registryLink)}</td></tr>";
    }));

    private ReleasedPackageMetadata? GetMetadata(SDKInfo sdk) =>
        _packageMetadata.FirstOrDefault(kvp => string.Equals(kvp.Key, sdk.Language, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>
    /// Returns the public package registry page for a released SDK version, or an empty string when
    /// the registry location cannot be determined from the recorded package details.
    /// </summary>
    public static string GetPackageRegistryLink(string language, string packageName, string version, string? groupId = null)
    {
        if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(version))
        {
            return string.Empty;
        }

        packageName = packageName.Trim();
        version = version.Trim();
        var escapedVersion = Uri.EscapeDataString(version);
        switch (DevOpsService.MapLanguageIdToName(language?.Trim() ?? string.Empty).ToLowerInvariant())
        {
            case ".net":
                return $"https://www.nuget.org/packages/{Uri.EscapeDataString(packageName)}/{escapedVersion}";
            case "python":
                return $"https://pypi.org/project/{Uri.EscapeDataString(packageName)}/{escapedVersion}/";
            case "javascript":
                return $"https://www.npmjs.com/package/{EscapePath(packageName).Replace("%40", "@")}/v/{escapedVersion}";
            case "java":
                var separator = packageName.IndexOf(':');
                if (separator > 0)
                {
                    groupId = packageName[..separator];
                    packageName = packageName[(separator + 1)..];
                }
                return string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(packageName)
                    ? string.Empty
                    : $"https://central.sonatype.com/artifact/{Uri.EscapeDataString(groupId.Trim())}/{Uri.EscapeDataString(packageName)}/{escapedVersion}";
            case "go":
                return packageName.StartsWith("sdk/", StringComparison.Ordinal)
                    ? $"https://pkg.go.dev/github.com/Azure/azure-sdk-for-go/{EscapePath(packageName)}@{Uri.EscapeDataString(version.StartsWith('v') ? version : $"v{version}")}"
                    : string.Empty;
            case "rust":
                return $"https://crates.io/crates/{Uri.EscapeDataString(packageName)}/{escapedVersion}";
            default:
                return string.Empty;
        }
    }

    private static string EscapePath(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private string Feedback => _plan.IsManagementPlane
        ? """
          <h3>Share your feedback</h3>
          <p>We want to make the SDK release workflow faster and easier for service teams. Please take a few minutes to fill out a short survey about your experience with this release: what worked well, what got in your way, and what we should improve.</p>
          <p><a href="https://forms.cloud.microsoft/r/rQMr2bc7DT">Service Partner Feedback about Azure SDK Release Process &ndash; Fill out form</a></p>
          """
        : string.Empty;

    private static string Encode(string value) =>
        string.IsNullOrWhiteSpace(value) ? "Not recorded" : WebUtility.HtmlEncode(value);

    private static string Link(string url, string text) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? $"<a href=\"{WebUtility.HtmlEncode(url)}\">{Encode(text)}</a>"
            : Encode(url);
}
