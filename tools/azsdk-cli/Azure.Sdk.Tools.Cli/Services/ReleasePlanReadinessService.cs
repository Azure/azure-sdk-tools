using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace Azure.Sdk.Tools.Cli.Services;

public sealed class ReleasePlanReadinessService(IDevOpsService devOps, IServiceApiReadinessService readiness,
    TimeProvider? timeProvider = null)
{
    private const string ProjectField = "Custom.ApiSpecProjectPath";
    private const int MaxObservations = 128;
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    private sealed record Observation(
        [property: JsonPropertyName("resourceProvider")] string ResourceProvider,
        [property: JsonPropertyName("apiVersion")] string ApiVersion,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("matchedResourceTypes")] string[] MatchedResourceTypes,
        [property: JsonPropertyName("validationEnvironment")] string ValidationEnvironment,
        [property: JsonPropertyName("checkedAt")] DateTimeOffset CheckedAt);

    public async Task<ServiceApiReadinessResponse> CheckAsync(string owner, string repository, int pullRequest,
        bool recordResult, CancellationToken ct)
    {
        var field = Environment.GetEnvironmentVariable("AZSDK_SERVICE_READINESS_FIELD");
        if (string.IsNullOrEmpty(field))
        {
            return await readiness.CheckAsync(owner, repository, pullRequest, ct);
        }
        var environment = Environment.GetEnvironmentVariable("AZSDK_SERVICE_READINESS_ENVIRONMENT");
        if (!Regex.IsMatch(field, @"\ACustom\.[A-Za-z][\w.]+\z") ||
            field.Equals(ProjectField, StringComparison.OrdinalIgnoreCase) ||
            field.Equals(ReleasePlanWorkItem.SpecCommitSHAField, StringComparison.OrdinalIgnoreCase) ||
            field.StartsWith("Custom.SDKPullRequestFor", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(environment) ||
            !int.TryParse(Environment.GetEnvironmentVariable("AZSDK_SERVICE_READINESS_MAX_AGE_SECONDS"), out var seconds) || seconds <= 0)
        {
            var direct = await readiness.CheckAsync(owner, repository, pullRequest, ct);
            direct.Detail += " Release-plan reuse is not configured correctly; ARM was checked directly.";
            return direct;
        }
        var maxAge = TimeSpan.FromSeconds(seconds);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(PackageApiMetadataService.EvidenceTimeout);
        ct = budget.Token;
        var url = $"https://github.com/{owner}/{repository}/pull/{pullRequest}";
        var language = SdkLanguageHelpers.GetLanguageForRepo(repository).ToString();
        WorkItem? plan = null;
        try
        {
            if (owner.Equals("Azure", StringComparison.OrdinalIgnoreCase))
            {
                var plans = await devOps.GetReleasePlansBySdkPullRequestAsync(url, language, ct: ct);
                if (plans.Count == 1 && plans[0].WorkItemId > 0)
                {
                    var items = await devOps.GetWorkItemsByIdsAsync([plans[0].WorkItemId], ct: ct);
                    plan = items.Count == 1 ? items[0] : null;
                }
            }
            if (plan is not null)
            {
                var metadata = await readiness.GetPackageMetadataAsync(owner, repository, pullRequest, ct);
                if (MatchesPlan(plan, metadata, url, language) && metadata.Plane == SdkPackageMetadataHelper.ManagementPlane &&
                    metadata.ReadinessStatus != ServiceApiReadinessResponse.Error)
                {
                    var observations = ReadObservations(plan, field);
                    if (observations.Length > 0)
                    {
                        var changedAt = await ReadScopeChangedAtAsync(plan, ct);
                        if (changedAt.HasValue && TryReuse(metadata, observations, environment, maxAge, changedAt.Value))
                        {
                            return metadata;
                        }
                    }
                }
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException && !ct.IsCancellationRequested)
        {
            var direct = await readiness.CheckAsync(owner, repository, pullRequest, ct);
            direct.Detail += " Release-plan state was unavailable; ARM was checked directly.";
            return direct;
        }
        var result = await readiness.CheckAsync(owner, repository, pullRequest, ct);
        if (recordResult && plan is not null && MatchesPlan(plan, result, url, language) &&
            result.Plane == SdkPackageMetadataHelper.ManagementPlane && result.Projects.Count > 0)
        {
            try
            {
                await RecordAsync(plan, result, field, environment, maxAge, url, language, ct);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException && !ct.IsCancellationRequested)
            {
                result.Detail += " The optional release-plan update failed; the check result is unchanged.";
            }
        }
        return result;
    }

    private static string Text(WorkItem item, string field) =>
        item.Fields.TryGetValue(field, out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" : "";

    private static bool MatchesPlan(WorkItem plan, ServiceApiReadinessResponse result, string url, string language)
    {
        var fieldLanguage = language.Equals("DotNet", StringComparison.OrdinalIgnoreCase) ? "Dotnet" : language;
        return Text(plan, "System.WorkItemType") == "Release Plan" && Text(plan, "System.State") != "Abandoned" &&
            Text(plan, $"Custom.SDKPullRequestFor{fieldLanguage}").TrimEnd('/').Equals(url, StringComparison.OrdinalIgnoreCase) &&
            result.Packages.Count > 0 && result.Packages.All(package =>
                package.MetadataIssues.Length == 0 && package.TypeSpecLocation is { } location &&
                location.Directory == Text(plan, ProjectField).Replace('\\', '/').TrimEnd('/') &&
                location.Commit == Text(plan, ReleasePlanWorkItem.SpecCommitSHAField).Trim());
    }

    private static Observation[] ReadObservations(WorkItem plan, string field)
    {
        var text = Text(plan, field);
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }
        if (text.Length > 262144)
        {
            throw new InvalidOperationException("Readiness metadata is too large.");
        }
        var document = JsonNode.Parse(text) as JsonObject;
        if (document?["schemaVersion"]?.GetValue<int>() == 1)
        {
            return [];
        }
        if (document?["schemaVersion"]?.GetValue<int>() != 2 || document["results"] is not JsonArray results)
        {
            throw new InvalidOperationException("Unsupported readiness metadata.");
        }
        if (results.Count > MaxObservations)
        {
            throw new InvalidOperationException("Readiness metadata is full.");
        }
        var observations = results.Deserialize<Observation[]>() ?? [];
        if (observations.Any(item => string.IsNullOrWhiteSpace(item.ResourceProvider) ||
            string.IsNullOrWhiteSpace(item.ApiVersion) || item.MatchedResourceTypes is null ||
            item.CheckedAt == DateTimeOffset.MinValue ||
            item.MatchedResourceTypes.Any(string.IsNullOrWhiteSpace) ||
            string.IsNullOrWhiteSpace(item.ValidationEnvironment) ||
            item.Status is not (ServiceApiReadinessResponse.Passed or ServiceApiReadinessResponse.NotDetected or
                ServiceApiReadinessResponse.Inconclusive or ServiceApiReadinessResponse.Error)))
        {
            throw new InvalidOperationException("Readiness metadata is invalid.");
        }
        return observations;
    }

    private async Task<DateTimeOffset?> ReadScopeChangedAtAsync(WorkItem plan, CancellationToken ct)
    {
        var updates = await devOps.GetWorkItemUpdatesAsync(plan.Id!.Value, ct);
        if (updates.Count == 0 || updates.Max(update => update.Rev) < plan.Rev)
        {
            return null;
        }
        DateTimeOffset? latest = null;
        foreach (var update in updates)
        {
            if (update.Fields is null || !update.Fields.ContainsKey(ProjectField) &&
                !update.Fields.ContainsKey(ReleasePlanWorkItem.SpecCommitSHAField))
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
        TimeSpan maxAge, DateTimeOffset scopeChangedAt)
    {
        if (result.Projects.Count == 0 || result.Projects.Any(project => project.ReadinessStatus == ServiceApiReadinessResponse.Error))
        {
            return false;
        }
        var now = _clock.GetUtcNow();
        var matches = new List<(ServiceApiReadinessProject Project, Observation Observation, string[] Types)>();
        foreach (var project in result.Projects)
        {
            var types = project.Package.ResourceTypes.Where(type =>
                type.Namespace.Equals(project.Namespace, StringComparison.OrdinalIgnoreCase) && type.ApiVersion == project.ApiVersion.Version)
                .Select(type => type.ResourceType).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var observation = observations.OrderByDescending(item => item.CheckedAt).FirstOrDefault(item =>
                item.Status == ServiceApiReadinessResponse.Passed && item.ResourceProvider.Equals(project.Namespace, StringComparison.OrdinalIgnoreCase) &&
                item.ApiVersion == project.ApiVersion.Version && item.ValidationEnvironment == environment &&
                item.CheckedAt >= scopeChangedAt && item.CheckedAt <= now && now - item.CheckedAt < maxAge &&
                item.MatchedResourceTypes.Any(types.Contains));
            if (observation is null)
            {
                return false;
            }
            matches.Add((project, observation, observation.MatchedResourceTypes.Where(types.Contains).ToArray()));
        }
        foreach (var match in matches)
        {
            match.Project.ReadinessStatus = ServiceApiReadinessResponse.Passed;
            match.Project.Matches = [.. match.Types.Select(type => new ServiceApiReadinessMatch { ResourceType = type })];
            match.Project.Detail = $"Reused release-plan service evidence checked at {match.Observation.CheckedAt:O}.";
        }
        result.ReadinessStatus = ServiceApiReadinessResponse.Passed;
        result.CheckedAt = matches.Min(match => match.Observation.CheckedAt);
        result.Detail = "Reused release-plan evidence for the captured SDK head; this is not release approval or proof of regional deployment.";
        return true;
    }

    private async Task RecordAsync(WorkItem plan, ServiceApiReadinessResponse result, string field, string environment,
        TimeSpan maxAge, string url, string language, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var items = await devOps.GetWorkItemsByIdsAsync([plan.Id!.Value], ct: ct);
            if (items.Count != 1 || !MatchesPlan(items[0], result, url, language))
            {
                return;
            }
            var current = items[0];
            var changedAt = await ReadScopeChangedAtAsync(current, ct);
            if (!changedAt.HasValue || result.CheckedAt < changedAt.Value)
            {
                return;
            }
            var observations = ReadObservations(current, field).Where(item => item.CheckedAt >= changedAt.Value).ToList();
            foreach (var project in result.Projects)
            {
                var addition = new Observation(project.Namespace, project.ApiVersion.Version, project.ReadinessStatus,
                    [.. project.Matches.Select(match => match.ResourceType)], environment, result.CheckedAt);
                var index = observations.FindIndex(item => item.ResourceProvider.Equals(addition.ResourceProvider, StringComparison.OrdinalIgnoreCase) &&
                    item.ApiVersion == addition.ApiVersion && item.ValidationEnvironment == environment);
                var previous = index < 0 ? null : observations[index];
                if (previous is not null && (previous.CheckedAt > addition.CheckedAt ||
                    addition.Status == ServiceApiReadinessResponse.Error && previous.Status == ServiceApiReadinessResponse.Passed &&
                    _clock.GetUtcNow() - previous.CheckedAt < maxAge))
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
            var document = new JsonObject { ["schemaVersion"] = 2, ["results"] = JsonSerializer.SerializeToNode(observations) };
            var expectedRevision = current.Rev!.Value;
            try
            {
                await devOps.UpdateWorkItemAsync(current.Id!.Value, new Dictionary<string, string> { [field] = document.ToJsonString() }, expectedRevision, ct);
                return;
            }
            catch (Exception exception) when (attempt == 0 && exception is not OutOfMemoryException && !ct.IsCancellationRequested)
            {
                var latest = await devOps.GetWorkItemsByIdsAsync([current.Id!.Value], ct: ct);
                if (latest.Count != 1 || latest[0].Rev == expectedRevision)
                {
                    throw;
                }
            }
        }
    }
}