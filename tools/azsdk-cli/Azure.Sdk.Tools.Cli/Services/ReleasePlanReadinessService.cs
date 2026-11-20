using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace Azure.Sdk.Tools.Cli.Services;

public sealed class ReleasePlanReadinessService(IDevOpsService devOps, IServiceApiReadinessService readiness,
    TimeProvider? timeProvider = null)
{
    private static readonly TimeSpan NonPassedLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan AdoTimestampPrecision = TimeSpan.FromMilliseconds(3);
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    private sealed record Observation(
        [property: JsonPropertyName("resourceProvider")] string ResourceProvider,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("matchedResourceTypes")] ServiceApiReadinessMatch[] MatchedResourceTypes,
        [property: JsonPropertyName("validationEnvironment")] string ValidationEnvironment,
        [property: JsonPropertyName("checkedAt")] DateTimeOffset CheckedAt);

    public async Task<ServiceApiReadinessResponse> CheckAsync(string owner, string repository, int pullRequest,
        bool recordResult, CancellationToken ct)
    {
        var environment = Environment.GetEnvironmentVariable("AZSDK_SERVICE_READINESS_ENVIRONMENT");
        if (string.IsNullOrWhiteSpace(environment))
        {
            return await readiness.CheckAsync(owner, repository, pullRequest, ct);
        }
        var callerToken = ct;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(PackageApiMetadataService.EvidenceTimeout);
        ct = budget.Token;
        var url = $"https://github.com/{owner}/{repository}/pull/{pullRequest}";
        var language = SdkLanguageHelpers.GetLanguageForRepo(repository).ToString();
        ReleasePlanWorkItem? plan = null;
        ApiSpecWorkItem? specification = null;
        (ReleasePlanWorkItem Plan, ApiSpecWorkItem Specification, List<WorkItemUpdate> PlanUpdates, List<WorkItemUpdate> SpecificationUpdates)? snapshot = null;
        ServiceApiReadinessResponse? metadata = null;
        try
        {
            if (owner.Equals("Azure", StringComparison.OrdinalIgnoreCase))
            {
                snapshot = await devOps.GetReleasePlanReadinessAsync(url, language, ct);
                if (snapshot.HasValue)
                {
                    plan = snapshot.Value.Plan;
                    specification = snapshot.Value.Specification;
                }
            }
            if (plan is not null && specification is not null)
            {
                metadata = await readiness.GetPackageMetadataAsync(owner, repository, pullRequest, ct);
                if (MatchesPlan(plan, specification, metadata, url, language) && metadata.Plane == SdkPackageMetadataHelper.ManagementPlane &&
                    metadata.ReadinessStatus != ServiceApiReadinessResponse.Error)
                {
                    var observations = ReadObservations(plan);
                    if (observations.Length > 0)
                    {
                        var changedAt = ReadScopeChangedAt(plan, specification, snapshot!.Value.PlanUpdates, snapshot.Value.SpecificationUpdates);
                        if (changedAt.HasValue && TryReuse(metadata, observations, environment, changedAt.Value, plan))
                        {
                            return metadata;
                        }
                    }
                }
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException && !ct.IsCancellationRequested)
        {
            var direct = metadata is null
                ? await readiness.CheckAsync(owner, repository, pullRequest, ct)
                : await readiness.ValidateAsync(metadata, ct);
            direct.Detail += " Release-plan state was unavailable; ARM was checked directly.";
            return direct;
        }
        var result = metadata is null
            ? await readiness.CheckAsync(owner, repository, pullRequest, ct)
            : await readiness.ValidateAsync(metadata, ct);
        if (recordResult && plan is not null && specification is not null && MatchesPlan(plan, specification, result, url, language) &&
            result.Plane == SdkPackageMetadataHelper.ManagementPlane && result.Projects.Count > 0)
        {
            try
            {
                await RecordAsync(plan, specification, result, environment, url, language, ct);
                callerToken.ThrowIfCancellationRequested();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException && !callerToken.IsCancellationRequested)
            {
                result.Detail += " The optional release-plan update failed; the check result is unchanged.";
            }
        }
        return result;
    }

    private static string ToAdoStatus(string status) => status switch
    {
        ServiceApiReadinessResponse.Passed => "Ready",
        ServiceApiReadinessResponse.NotDetected => "Note Ready",
        _ => "Unknown"
    };

    private static bool MatchesPlan(ReleasePlanWorkItem plan, ApiSpecWorkItem specification, ServiceApiReadinessResponse result, string url, string language)
    {
        var planLanguage = language.Equals("DotNet", StringComparison.OrdinalIgnoreCase) ? ".NET" : language;
        return plan.WorkItemId > 0 && specification.WorkItemId > 0 && plan.Status != "Abandoned" &&
            plan.ApiSpecWorkItemId == specification.WorkItemId &&
            plan.SDKInfo.Any(sdk => sdk.Language.Equals(planLanguage, StringComparison.OrdinalIgnoreCase) &&
                sdk.SdkPullRequestUrl.TrimEnd('/').Equals(url, StringComparison.OrdinalIgnoreCase)) &&
            result.Projects.Count > 0 && result.Projects.All(project => project.ApiVersion.Version == specification.SpecAPIVersion.Trim()) &&
            result.Packages.Count > 0 && result.Packages.All(package =>
                package.MetadataIssues.Length == 0 && package.TypeSpecLocation is { } location &&
                location.Directory == plan.APISpecProjectPath.Replace('\\', '/').TrimEnd('/') &&
                location.Commit == plan.SpecCommitSHA.Trim());
    }

    private static Observation[] ReadObservations(ReleasePlanWorkItem plan)
    {
        var html = plan.ServiceReadinessDetails;
        if (string.IsNullOrEmpty(html))
        {
            return [];
        }
        var text = WebUtility.HtmlDecode(html);
        var document = JsonNode.Parse(text) as JsonObject;
        if (document?["schemaVersion"]?.GetValue<int>() is 1 or 2 or 3)
        {
            return [];
        }
        if (document?["schemaVersion"]?.GetValue<int>() != 4 || document["results"] is not JsonArray results)
        {
            throw new InvalidOperationException("Unsupported readiness metadata.");
        }
        var observations = results.Deserialize<Observation[]>() ?? [];
        if (observations.Any(item => string.IsNullOrWhiteSpace(item.ResourceProvider) ||
            item.MatchedResourceTypes is null ||
            item.CheckedAt == DateTimeOffset.MinValue ||
            item.MatchedResourceTypes.Any(match => match is null || string.IsNullOrWhiteSpace(match.ResourceType) ||
                match.Regions is null || match.Regions.Any(string.IsNullOrWhiteSpace)) ||
            item.Status == ServiceApiReadinessResponse.Passed && item.MatchedResourceTypes.Length == 0 ||
            string.IsNullOrWhiteSpace(item.ValidationEnvironment) ||
            item.Status is not (ServiceApiReadinessResponse.Passed or ServiceApiReadinessResponse.NotDetected or
                ServiceApiReadinessResponse.Inconclusive or ServiceApiReadinessResponse.Error)))
        {
            throw new InvalidOperationException("Readiness metadata is invalid.");
        }
        return observations;
    }

    private static DateTimeOffset? ReadScopeChangedAt(ReleasePlanWorkItem plan, ApiSpecWorkItem specification,
        IReadOnlyList<WorkItemUpdate> planUpdates, IReadOnlyList<WorkItemUpdate> specificationUpdates)
    {
        var planChangedAt = ReadFieldsChangedAt(plan.WorkItemId, plan.Revision, planUpdates,
            [ReleasePlanWorkItem.ApiSpecProjectPathField, ReleasePlanWorkItem.SpecCommitSHAField]);
        var versionChangedAt = ReadFieldsChangedAt(specification.WorkItemId, specification.Revision, specificationUpdates,
            [ApiSpecWorkItem.SpecAPIVersionField]);
        return planChangedAt.HasValue && versionChangedAt.HasValue
            ? planChangedAt.Value > versionChangedAt.Value ? planChangedAt : versionChangedAt
            : null;
    }

    private static DateTimeOffset? ReadFieldsChangedAt(int workItemId, int revision, IReadOnlyList<WorkItemUpdate> updates, string[] fields)
    {
        if (workItemId <= 0 || revision <= 0)
        {
            return null;
        }
        if (updates.Count == 0 || updates.Max(update => update.Rev) < revision)
        {
            return null;
        }
        DateTimeOffset? latestChangedAt = null;
        foreach (var update in updates)
        {
            if (update.Fields is null || !fields.Any(update.Fields.ContainsKey))
            {
                continue;
            }
            var changedAt = update.Fields.TryGetValue("System.ChangedDate", out var changed)
                ? DevOpsService.ReadWorkItemTimestamp(changed.NewValue) : null;
            if (!changedAt.HasValue)
            {
                return null;
            }
            if (!latestChangedAt.HasValue || changedAt.Value > latestChangedAt.Value)
            {
                latestChangedAt = changedAt;
            }
        }
        return latestChangedAt;
    }

    private bool TryReuse(ServiceApiReadinessResponse result, Observation[] observations, string environment,
        DateTimeOffset scopeChangedAt, ReleasePlanWorkItem plan)
    {
        if (result.Projects.Count == 0 || result.Projects.Any(project => project.ReadinessStatus == ServiceApiReadinessResponse.Error))
        {
            return false;
        }
        var now = _clock.GetUtcNow();
        var matches = new List<(ServiceApiReadinessProject Project, Observation Observation)>();
        foreach (var project in result.Projects)
        {
            var observation = observations.Where(item =>
                item.ResourceProvider.Equals(project.Namespace, StringComparison.OrdinalIgnoreCase) &&
                item.ValidationEnvironment == environment &&
                item.CheckedAt >= scopeChangedAt && item.CheckedAt <= now)
                .MaxBy(item => item.CheckedAt);
            if (observation is null || observation.Status != ServiceApiReadinessResponse.Passed &&
                now - observation.CheckedAt >= NonPassedLifetime)
            {
                return false;
            }
            matches.Add((project, observation));
        }
        var summary = GetEvidenceSummary(matches.Select(match => match.Observation));
        var checkedAt = plan.ServiceReadinessCheckedAt;
        var storedStatus = plan.ServiceReadinessStatus;
        var matchesStatus = summary.Status == ServiceApiReadinessResponse.NotDetected
            ? storedStatus.Equals("Not ready", StringComparison.OrdinalIgnoreCase) || storedStatus == "Note Ready"
            : storedStatus == ToAdoStatus(summary.Status);
        if (!matchesStatus ||
            !checkedAt.HasValue || checkedAt > now + AdoTimestampPrecision ||
            (checkedAt.Value - summary.CheckedAt).Duration() > AdoTimestampPrecision)
        {
            return false;
        }
        foreach (var match in matches)
        {
            match.Project.ReadinessStatus = match.Observation.Status;
            match.Project.Matches = [.. match.Observation.MatchedResourceTypes.Select(type => new ServiceApiReadinessMatch
            {
                ResourceType = type.ResourceType,
                Regions = [.. type.Regions]
            })];
            match.Project.Detail = $"Reused release-plan service evidence checked at {match.Observation.CheckedAt:O}.";
        }
        ServiceApiReadinessService.SetOverallResult(result);
        result.CheckedAt = summary.CheckedAt;
        result.Detail = "Reused release-plan evidence for the captured SDK head; this is not release approval or proof of regional deployment.";
        return true;
    }

    private static (string Status, DateTimeOffset CheckedAt) GetEvidenceSummary(IEnumerable<Observation> observations)
    {
        var items = observations.ToArray();
        var status = ServiceApiReadinessService.GetOverallStatus(items.Select(item => item.Status));
        var timedItems = status == ServiceApiReadinessResponse.Passed
            ? items : items.Where(item => item.Status != ServiceApiReadinessResponse.Passed);
        return (status, timedItems.Min(item => item.CheckedAt));
    }

    private async Task RecordAsync(ReleasePlanWorkItem plan, ApiSpecWorkItem specification, ServiceApiReadinessResponse result, string environment,
        string url, string language, CancellationToken ct)
    {
        var snapshot = await devOps.GetReleasePlanReadinessAsync(url, language, ct);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (!snapshot.HasValue)
            {
                return;
            }
            var current = snapshot.Value.Plan;
            var currentSpecification = snapshot.Value.Specification;
            if (current.WorkItemId != plan.WorkItemId || currentSpecification.WorkItemId != specification.WorkItemId || !MatchesPlan(current, currentSpecification, result, url, language))
            {
                return;
            }
            var changedAt = ReadScopeChangedAt(current, currentSpecification, snapshot.Value.PlanUpdates, snapshot.Value.SpecificationUpdates);
            if (!changedAt.HasValue || result.CheckedAt < changedAt.Value)
            {
                return;
            }
            var observations = ReadObservations(current).Where(item => item.CheckedAt >= changedAt.Value).ToList();
            foreach (var project in result.Projects.DistinctBy(project => project.Namespace, StringComparer.OrdinalIgnoreCase))
            {
                var addition = new Observation(project.Namespace, project.ReadinessStatus,
                    [.. project.Matches.Select(match => new ServiceApiReadinessMatch
                    {
                        ResourceType = match.ResourceType,
                        Regions = [.. match.Regions]
                    })], environment, result.CheckedAt);
                var index = observations.FindIndex(item => item.ResourceProvider.Equals(addition.ResourceProvider, StringComparison.OrdinalIgnoreCase) &&
                    item.ValidationEnvironment == environment);
                var previous = index < 0 ? null : observations[index];
                if (previous is not null && (previous.CheckedAt > addition.CheckedAt ||
                    addition.Status == ServiceApiReadinessResponse.Error && previous.Status == ServiceApiReadinessResponse.Passed))
                {
                    continue;
                }
                if (index < 0)
                {
                    observations.Add(addition);
                }
                else
                {
                    observations[index] = addition;
                }
            }
            var document = new JsonObject { ["schemaVersion"] = 4, ["results"] = JsonSerializer.SerializeToNode(observations) };
            var json = document.ToJsonString();
            var html = WebUtility.HtmlEncode(json);
            var summary = GetEvidenceSummary(result.Projects.Select(project => observations.Single(item =>
                item.ResourceProvider.Equals(project.Namespace, StringComparison.OrdinalIgnoreCase) &&
                item.ValidationEnvironment == environment)));
            var expectedRevision = current.Revision;
            try
            {
                await devOps.UpdateWorkItemAsync(current.WorkItemId, new Dictionary<string, string>
                {
                    [ReleasePlanWorkItem.ServiceReadinessStatusField] = ToAdoStatus(summary.Status),
                    [ReleasePlanWorkItem.ServiceReadinessCheckedAtField] = summary.CheckedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                    [ReleasePlanWorkItem.ServiceReadinessDetailsField] = html
                }, expectedRevision, ct);
                return;
            }
            catch (Exception exception) when (attempt == 0 && exception is not OutOfMemoryException && !ct.IsCancellationRequested)
            {
                var latest = await devOps.GetReleasePlanReadinessAsync(url, language, ct);
                if (!latest.HasValue || latest.Value.Plan.WorkItemId != current.WorkItemId || latest.Value.Plan.Revision == expectedRevision)
                {
                    throw;
                }
                snapshot = latest;
            }
        }
    }
}
