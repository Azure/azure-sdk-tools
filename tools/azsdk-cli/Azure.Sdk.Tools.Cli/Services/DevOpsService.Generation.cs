// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using DevOpsPatch = Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument;
using PatchOperation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation;

namespace Azure.Sdk.Tools.Cli.Services
{
    public partial class DevOpsService
    {
        /// <summary>
        /// Validates a run against the current release target and returns its immutable build inputs.
        /// This checks the requested snapshot, not the generated output.
        /// </summary>
        public async Task<Build> ValidateSdkGenerationRunAsync(int workItemId, int buildId, string language, CancellationToken ct)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workItemId);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(buildId);
            ct.ThrowIfCancellationRequested();

            var build = await GetPipelineRunAsync(buildId, ct);
            var plan = await GetReleasePlanForWorkItemAsync(workItemId, ct);
            ct.ThrowIfCancellationRequested();
            ValidateSdkGenerationSnapshot(build, plan, workItemId, buildId, language);
            return build;
        }

        /// <summary>
        /// Records completion only while the first observed parent revision, pin, and latest run still match.
        /// Conflicts and cancellation propagate to the caller; completion is never automatically retried.
        /// </summary>
        public async Task<bool> CompleteSdkGenerationAsync(
            int workItemId, int buildId, string language, string sdkPrUrl, string status, CancellationToken ct)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workItemId);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(buildId);
            ct.ThrowIfCancellationRequested();

            var languageId = SdkGenerationTargetHelper.GetLanguageId(language);
            var completionStatus = status?.ToLowerInvariant() switch
            {
                "draft" => "draft",
                "ready for review" => "ready for review",
                "failed to generate sdk." => "Failed to generate SDK.",
                "no changes" => "No changes",
                _ => throw new ArgumentException($"Expected status 'draft', 'ready for review', or 'Failed to generate SDK.'; actual '{status}'.", nameof(status))
            };
            if (string.IsNullOrWhiteSpace(sdkPrUrl))
            {
                if (completionStatus is not "Failed to generate SDK." and not "No changes")
                {
                    throw new ArgumentException($"Expected an SDK pull request URL for status '{completionStatus}'; actual '<missing>'.", nameof(sdkPrUrl));
                }
            }
            else
            {
                ValidateSdkGenerationPullRequest(sdkPrUrl, languageId);
            }

            var build = await GetPipelineRunAsync(buildId, ct);
            var workItemClient = connection.GetWorkItemClient(ct);
            var parent = await workItemClient.GetWorkItemAsync(workItemId, expand: WorkItemExpand.All, cancellationToken: ct);
            if (parent?.Id != workItemId)
            {
                throw new InvalidOperationException($"Expected release plan work item '{workItemId}', actual '{parent?.Id?.ToString() ?? "<missing>"}'.");
            }
            if (parent.Fields == null)
            {
                throw new InvalidOperationException("Expected Release Plan fields, actual '<missing>'.");
            }
            var workItemType = parent.Fields.TryGetValue("System.WorkItemType", out var type)
                ? type?.ToString()
                : null;
            if (!string.Equals(workItemType, "Release Plan", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Expected work item type 'Release Plan', actual '{workItemType ?? "<missing>"}'.");
            }
            if (parent.Rev is not > 0)
            {
                throw new InvalidOperationException($"Expected a positive parent revision, actual '{parent.Rev?.ToString() ?? "<missing>"}'.");
            }

            // Keep the FIRST snapshot, even though mapping re-reads the parent to locate the child and check its pin.
            var revision = parent.Rev.Value;
            var pipelineField = $"Custom.SDKGenerationPipelineFor{languageId}";
            parent.Fields.TryGetValue(ReleasePlanWorkItem.SpecCommitSHAField, out var parentPin);
            parent.Fields.TryGetValue(pipelineField, out var latestPipelineUrl);
            var plan = await MapWorkItemToReleasePlanAsync(parent, ct);
            ct.ThrowIfCancellationRequested();
            ValidateSdkGenerationSnapshot(build, plan, workItemId, buildId, languageId);

            var triggerSource = build.TemplateParameters.TryGetValue("TriggerSource", out var trigger) ? trigger : null;
            if (completionStatus == "ready for review" && !string.Equals(triggerSource, "sdk-release", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Expected TriggerSource 'sdk-release' for status 'ready for review', actual '{triggerSource ?? "<missing>"}'. Draft runs must report 'draft'.");
            }

            var patch = new DevOpsPatch
            {
                new JsonPatchOperation { Operation = PatchOperation.Test, Path = "/rev", Value = revision },
                new JsonPatchOperation { Operation = PatchOperation.Test, Path = $"/fields/{ReleasePlanWorkItem.SpecCommitSHAField}", Value = parentPin },
                new JsonPatchOperation { Operation = PatchOperation.Test, Path = $"/fields/{pipelineField}", Value = latestPipelineUrl }
            };
            if (!string.IsNullOrWhiteSpace(sdkPrUrl))
            {
                patch.Add(new JsonPatchOperation { Operation = PatchOperation.Add, Path = $"/fields/Custom.SDKPullRequestFor{languageId}", Value = sdkPrUrl });
            }
            if (completionStatus != "No changes")
            {
                patch.Add(new JsonPatchOperation { Operation = PatchOperation.Add, Path = $"/fields/Custom.SDKPullRequestStatusFor{languageId}", Value = completionStatus });
            }
            patch.Add(new JsonPatchOperation { Operation = PatchOperation.Add, Path = $"/fields/Custom.GenerationStatusFor{languageId}", Value = "Completed" });

            ct.ThrowIfCancellationRequested();
            await workItemClient.UpdateWorkItemAsync(patch, workItemId, cancellationToken: ct);
            logger.LogInformation("Recorded SDK generation completion for build {BuildId}, work item {WorkItemId}, language {Language}.", buildId, workItemId, languageId);
            return true;
        }

        private static void ValidateSdkGenerationSnapshot(Build build, ReleasePlanWorkItem plan, int workItemId, int buildId, string language)
        {
            if (build?.Id != buildId)
            {
                throw new InvalidOperationException($"Expected build ID '{buildId}', actual '{build?.Id.ToString() ?? "<missing>"}'.");
            }
            if (plan?.WorkItemId != workItemId)
            {
                throw new InvalidOperationException($"Expected release plan work item ID '{workItemId}', actual '{plan?.WorkItemId.ToString() ?? "<missing>"}'.");
            }

            var definitionId = GetPipelineDefinitionId(SdkGenerationTargetHelper.GetLanguageId(language));
            if (definitionId <= 0)
            {
                throw new InvalidOperationException($"Expected a supported SDK generation language (.NET, JavaScript, Python, Java, or Go), actual '{language}'.");
            }
            if (build.Definition?.Id != definitionId)
            {
                throw new InvalidOperationException($"Expected {language} pipeline definition ID '{definitionId}', actual '{build.Definition?.Id.ToString() ?? "<missing>"}'.");
            }

            var mismatch = SdkGenerationTargetHelper.GetMismatch(build, plan, language);
            if (mismatch != null)
            {
                throw new InvalidOperationException(mismatch);
            }
        }

        private static void ValidateSdkGenerationPullRequest(string sdkPrUrl, string languageId)
        {
            var repository = languageId switch
            {
                "Dotnet" => "azure-sdk-for-net",
                "JavaScript" => "azure-sdk-for-js",
                "Python" => "azure-sdk-for-python",
                "Java" => "azure-sdk-for-java",
                "Go" => "azure-sdk-for-go",
                _ => throw new ArgumentException($"Unsupported SDK language '{languageId}'.", nameof(languageId))
            };
            var expected = $"https://github.com/Azure/{repository}/pull/<positive-number>";
            ParsedSdkPullRequest parsed;
            try
            {
                parsed = ParseSDKPullRequestUrl(sdkPrUrl);
            }
            catch (OverflowException ex)
            {
                throw new ArgumentException($"Expected SDK pull request URL '{expected}', actual '{sdkPrUrl}'.", nameof(sdkPrUrl), ex);
            }
            if (!parsed.IsValid || !string.Equals(parsed.RepoName, repository, StringComparison.Ordinal) ||
                !string.Equals(parsed.FullUrl, sdkPrUrl, StringComparison.Ordinal) ||
                !Uri.TryCreate(sdkPrUrl, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) || uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException($"Expected SDK pull request URL '{expected}', actual '{sdkPrUrl}'.", nameof(sdkPrUrl));
            }
        }
    }
}