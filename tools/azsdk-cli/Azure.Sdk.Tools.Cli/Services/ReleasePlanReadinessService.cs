using System.Globalization;
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
    private const string ProjectField = "Custom.ApiSpecProjectPath";
    private const string VersionField = "Custom.APISpecversion";
    private const string StatusField = "Custom.ServiceApiVersionReadinessStatus";
    private const string CheckedAtField = "Custom.ServiceApiVersionReadinessCheckedAt";
    private const string DetailsField = "Custom.ServiceApiVersionReadinessDetails";
    private const int MaxObservations = 128;
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
        WorkItem? plan = null;
        WorkItem? specification = null;
        (WorkItem Plan, WorkItem Specification, List<WorkItemUpdate> PlanUpdates, List<WorkItemUpdate> SpecificationUpdates)? snapshot = null;
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

    private static string Text(WorkItem item, string field) =>
        item.Fields.TryGetValue(field, out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" : "";

    private static bool MatchesPlan(WorkItem plan, WorkItem specification, ServiceApiReadinessResponse result, string url, string language)
    {
        var fieldLanguage = language.Equals("DotNet", StringComparison.OrdinalIgnoreCase) ? "Dotnet" : language;
        return Text(plan, "System.WorkItemType") == "Release Plan" && Text(plan, "System.State") != "Abandoned" &&
            Text(specification, "System.WorkItemType") == "API Spec" &&
            plan.Relations?.Any(relation => relation.Rel == "System.LinkTypes.Hierarchy-Forward" &&
                relation.Url.TrimEnd('/').EndsWith($"/{specification.Id}", StringComparison.Ordinal)) == true &&
            Text(plan, $"Custom.SDKPullRequestFor{fieldLanguage}").TrimEnd('/').Equals(url, StringComparison.OrdinalIgnoreCase) &&
            result.Projects.Count > 0 && result.Projects.All(project => project.ApiVersion.Version == Text(specification, VersionField).Trim()) &&
            result.Packages.Count > 0 && result.Packages.All(package =>
                package.MetadataIssues.Length == 0 && package.TypeSpecLocation is { } location &&
                location.Directory == Text(plan, ProjectField).Replace('\\', '/').TrimEnd('/') &&
                location.Commit == Text(plan, ReleasePlanWorkItem.SpecCommitSHAField).Trim());
    }

    private static Observation[] ReadObservations(WorkItem plan)
    {
        var text = Text(plan, DetailsField);
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }
        if (text.Length > 262144)
        {
            throw new InvalidOperationException("Readiness metadata is too large.");
        }
        var document = JsonNode.Parse(text) as JsonObject;
        if (document?["schemaVersion"]?.GetValue<int>() is 1 or 2 or 3)
        {
            return [];
        }
        if (document?["schemaVersion"]?.GetValue<int>() != 4 || document["results"] is not JsonArray results)
        {
            throw new InvalidOperationException("Unsupported readiness metadata.");
        }
        if (results.Count > MaxObservations)
        {
            throw new InvalidOperationException("Readiness metadata is full.");
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

    private static DateTimeOffset? ReadScopeChangedAt(WorkItem plan, WorkItem specification,
        IReadOnlyList<WorkItemUpdate> planUpdates, IReadOnlyList<WorkItemUpdate> specificationUpdates)
    {
        var planChangedAt = ReadFieldsChangedAt(plan, planUpdates, [ProjectField, ReleasePlanWorkItem.SpecCommitSHAField]);
        var versionChangedAt = ReadFieldsChangedAt(specification, specificationUpdates, [VersionField]);
        return planChangedAt.HasValue && versionChangedAt.HasValue
            ? planChangedAt.Value > versionChangedAt.Value ? planChangedAt : versionChangedAt
            : null;
    }

    private static DateTimeOffset? ReadFieldsChangedAt(WorkItem item, IReadOnlyList<WorkItemUpdate> updates, string[] fields)
    {
        if (item.Id is null or <= 0 || item.Rev is null or <= 0)
        {
            return null;
        }
        if (updates.Count == 0 || updates.Max(update => update.Rev) < item.Rev)
        {
            return null;
        }
        DateTimeOffset? latest = null;
        foreach (var update in updates)
        {
            if (update.Fields is null || !fields.Any(update.Fields.ContainsKey))
            {
                continue;
            }
            if (!update.Fields.TryGetValue("System.ChangedDate", out var changed) ||
                !DateTimeOffset.TryParse(Convert.ToString(changed.NewValue, CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time))
            {
                return null;
            }
            if (!latest.HasValue || time > latest.Value)
            {
                latest = time;
            }
        }
        return latest;
    }

    private bool TryReuse(ServiceApiReadinessResponse result, Observation[] observations, string environment,
        DateTimeOffset scopeChangedAt, WorkItem plan)
    {
        if (result.Projects.Count == 0 || result.Projects.Any(project => project.ReadinessStatus == ServiceApiReadinessResponse.Error))
        {
            return false;
        }
        var now = _clock.GetUtcNow();
        var matches = new List<(ServiceApiReadinessProject Project, Observation Observation)>();
        foreach (var project in result.Projects)
        {
            var observation = observations.OrderByDescending(item => item.CheckedAt).FirstOrDefault(item =>
                item.ResourceProvider.Equals(project.Namespace, StringComparison.OrdinalIgnoreCase) &&
                item.ValidationEnvironment == environment &&
                item.CheckedAt >= scopeChangedAt && item.CheckedAt <= now);
            if (observation is null || observation.Status != ServiceApiReadinessResponse.Passed &&
                now - observation.CheckedAt >= NonPassedLifetime)
            {
                return false;
            }
            matches.Add((project, observation));
        }
        var summary = Summarize(matches.Select(match => match.Observation));
        var checkedAt = ReadCheckedAt(plan);
        if (Text(plan, StatusField) != summary.Status ||
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

    private static DateTimeOffset? ReadCheckedAt(WorkItem plan)
    {
        plan.Fields.TryGetValue(CheckedAtField, out var value);
        return value switch
        {
            DateTimeOffset time => time,
            DateTime time => new DateTimeOffset(time.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : time),
            _ => DateTimeOffset.TryParse(Text(plan, CheckedAtField), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var time) ? time : null
        };
    }

    private static (string Status, DateTimeOffset CheckedAt) Summarize(IEnumerable<Observation> observations)
    {
        var items = observations.ToArray();
        var result = new ServiceApiReadinessResponse
        {
            Projects = [.. items.Select(item => new ServiceApiReadinessProject
            {
                Package = new PackageInfo(),
                ReadinessStatus = item.Status
            })]
        };
        ServiceApiReadinessService.SetOverallResult(result);
        var timedItems = result.ReadinessStatus == ServiceApiReadinessResponse.Passed
            ? items : items.Where(item => item.Status != ServiceApiReadinessResponse.Passed);
        return (result.ReadinessStatus, timedItems.Min(item => item.CheckedAt));
    }

    private async Task RecordAsync(WorkItem plan, WorkItem specification, ServiceApiReadinessResponse result, string environment,
        string url, string language, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var snapshot = await devOps.GetReleasePlanReadinessAsync(url, language, ct);
            if (!snapshot.HasValue)
            {
                return;
            }
            var current = snapshot.Value.Plan;
            var currentSpecification = snapshot.Value.Specification;
            if (current.Id != plan.Id || currentSpecification.Id != specification.Id || !MatchesPlan(current, currentSpecification, result, url, language))
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
            if (observations.Count > MaxObservations)
            {
                throw new InvalidOperationException("Readiness metadata is full.");
            }
            var document = new JsonObject { ["schemaVersion"] = 4, ["results"] = JsonSerializer.SerializeToNode(observations) };
            var summary = Summarize(result.Projects.Select(project => observations.Single(item =>
                item.ResourceProvider.Equals(project.Namespace, StringComparison.OrdinalIgnoreCase) &&
                item.ValidationEnvironment == environment)));
            var expectedRevision = current.Rev!.Value;
            try
            {
                await devOps.UpdateWorkItemAsync(current.Id!.Value, new Dictionary<string, string>
                {
                    [StatusField] = summary.Status,
                    [CheckedAtField] = summary.CheckedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                    [DetailsField] = document.ToJsonString()
                }, expectedRevision, ct);
                return;
            }
            catch (Exception exception) when (attempt == 0 && exception is not OutOfMemoryException && !ct.IsCancellationRequested)
            {
                var latest = await devOps.GetReleasePlanReadinessAsync(url, language, ct);
                if (!latest.HasValue || latest.Value.Plan.Id != current.Id || latest.Value.Plan.Rev == expectedRevision)
                {
                    throw;
                }
            }
        }
    }
}
