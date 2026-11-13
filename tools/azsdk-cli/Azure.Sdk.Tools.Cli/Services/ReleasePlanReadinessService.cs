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
    private const string VersionField = "Custom.APISpecversion";
    private const int MaxObservations = 128;
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    private sealed record Observation(
        [property: JsonPropertyName("resourceProvider")] string ResourceProvider,
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
            field.Equals(VersionField, StringComparison.OrdinalIgnoreCase) ||
            field.Equals(ReleasePlanWorkItem.SpecCommitSHAField, StringComparison.OrdinalIgnoreCase) ||
            field.StartsWith("Custom.SDKPullRequestFor", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(environment))
        {
            var direct = await readiness.CheckAsync(owner, repository, pullRequest, ct);
            direct.Detail += " Release-plan reuse is not configured correctly; ARM was checked directly.";
            return direct;
        }
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(PackageApiMetadataService.EvidenceTimeout);
        ct = budget.Token;
        var url = $"https://github.com/{owner}/{repository}/pull/{pullRequest}";
        var language = SdkLanguageHelpers.GetLanguageForRepo(repository).ToString();
        WorkItem? plan = null;
        WorkItem? specification = null;
        ServiceApiReadinessResponse? metadata = null;
        try
        {
            if (owner.Equals("Azure", StringComparison.OrdinalIgnoreCase))
            {
                var plans = await devOps.GetReleasePlansBySdkPullRequestAsync(url, language, ct: ct);
                if (plans.Count == 1 && plans[0].WorkItemId > 0 && plans[0].ApiSpecWorkItemId > 0)
                {
                    var items = await devOps.GetWorkItemsByIdsAsync([plans[0].WorkItemId, plans[0].ApiSpecWorkItemId], ct: ct);
                    plan = items.SingleOrDefault(item => item.Id == plans[0].WorkItemId);
                    specification = items.SingleOrDefault(item => item.Id == plans[0].ApiSpecWorkItemId);
                }
            }
            if (plan is not null && specification is not null)
            {
                metadata = await readiness.GetPackageMetadataAsync(owner, repository, pullRequest, ct);
                if (MatchesPlan(plan, specification, metadata, url, language) && metadata.Plane == SdkPackageMetadataHelper.ManagementPlane &&
                    metadata.ReadinessStatus != ServiceApiReadinessResponse.Error)
                {
                    var observations = ReadObservations(plan, field);
                    if (observations.Length > 0)
                    {
                        var changedAt = await ReadScopeChangedAtAsync(plan, specification, ct);
                        if (changedAt.HasValue && TryReuse(metadata, observations, environment, changedAt.Value))
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
                await RecordAsync(plan, specification, result, field, environment, url, language, ct);
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
        if (document?["schemaVersion"]?.GetValue<int>() is 1 or 2)
        {
            return [];
        }
        if (document?["schemaVersion"]?.GetValue<int>() != 3 || document["results"] is not JsonArray results)
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
            item.MatchedResourceTypes.Any(string.IsNullOrWhiteSpace) ||
            string.IsNullOrWhiteSpace(item.ValidationEnvironment) ||
            item.Status is not (ServiceApiReadinessResponse.Passed or ServiceApiReadinessResponse.NotDetected or
                ServiceApiReadinessResponse.Inconclusive or ServiceApiReadinessResponse.Error)))
        {
            throw new InvalidOperationException("Readiness metadata is invalid.");
        }
        return observations;
    }

    private async Task<DateTimeOffset?> ReadScopeChangedAtAsync(WorkItem plan, WorkItem specification, CancellationToken ct)
    {
        var planChangedAt = await ReadFieldsChangedAtAsync(plan, [ProjectField, ReleasePlanWorkItem.SpecCommitSHAField], ct);
        var versionChangedAt = await ReadFieldsChangedAtAsync(specification, [VersionField], ct);
        return planChangedAt.HasValue && versionChangedAt.HasValue
            ? planChangedAt.Value > versionChangedAt.Value ? planChangedAt : versionChangedAt
            : null;
    }

    private async Task<DateTimeOffset?> ReadFieldsChangedAtAsync(WorkItem item, string[] fields, CancellationToken ct)
    {
        if (item.Id is null or <= 0 || item.Rev is null or <= 0)
        {
            return null;
        }
        var updates = await devOps.GetWorkItemUpdatesAsync(item.Id!.Value, ct);
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
        DateTimeOffset scopeChangedAt)
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
                item.ValidationEnvironment == environment &&
                item.CheckedAt >= scopeChangedAt && item.CheckedAt <= now &&
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

    private async Task RecordAsync(WorkItem plan, WorkItem specification, ServiceApiReadinessResponse result, string field, string environment,
        string url, string language, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var items = await devOps.GetWorkItemsByIdsAsync([plan.Id!.Value, specification.Id!.Value], ct: ct);
            var current = items.SingleOrDefault(item => item.Id == plan.Id);
            var currentSpecification = items.SingleOrDefault(item => item.Id == specification.Id);
            if (current is null || currentSpecification is null || !MatchesPlan(current, currentSpecification, result, url, language))
            {
                return;
            }
            var changedAt = await ReadScopeChangedAtAsync(current, currentSpecification, ct);
            if (!changedAt.HasValue || result.CheckedAt < changedAt.Value)
            {
                return;
            }
            var observations = ReadObservations(current, field).Where(item => item.CheckedAt >= changedAt.Value).ToList();
            foreach (var project in result.Projects)
            {
                var addition = new Observation(project.Namespace, project.ReadinessStatus,
                    [.. project.Matches.Select(match => match.ResourceType)], environment, result.CheckedAt);
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
            var document = new JsonObject { ["schemaVersion"] = 3, ["results"] = JsonSerializer.SerializeToNode(observations) };
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
