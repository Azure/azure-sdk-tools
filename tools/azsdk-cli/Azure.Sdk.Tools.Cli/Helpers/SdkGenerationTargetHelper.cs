// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.TeamFoundation.Build.WebApi;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public static class SdkGenerationTargetHelper
    {
        /// <summary>
        /// Compares a run's immutable inputs with the current release target, without performing I/O.
        /// Pipeline definition ownership is checked by DevOpsService using its existing language mapping.
        /// </summary>
        public static string? GetMismatch(Build build, ReleasePlanWorkItem plan, string language)
        {
            if (build == null || build.Id <= 0)
            {
                return Mismatch("Build.Id", "a positive build ID", build?.Id.ToString(CultureInfo.InvariantCulture));
            }
            if (plan == null || plan.WorkItemId <= 0)
            {
                return Mismatch("WorkItemId", "a positive release plan work item ID", plan?.WorkItemId.ToString(CultureInfo.InvariantCulture));
            }

            var languageId = GetLanguageId(language);
            if (!DevOpsService.IsSDKGenerationSupported(languageId))
            {
                return Mismatch("Language", ".NET, JavaScript, Python, Java, or Go", language);
            }
            if (plan.ApiReleaseType == ApiReleaseType.PrivatePreview)
            {
                return Mismatch("ApiReleaseType", "a non-private-preview release plan", plan.ReleasePlanType);
            }

            var sdkInfo = plan.SDKInfo?.FirstOrDefault(info =>
                string.Equals(GetLanguageId(info.Language), languageId, StringComparison.OrdinalIgnoreCase));
            if (sdkInfo == null)
            {
                return Mismatch("SDKInfo.Language", languageId, null);
            }
            if (string.Equals(sdkInfo.ReleaseStatus, "Released", StringComparison.OrdinalIgnoreCase))
            {
                return Mismatch($"{languageId}.ReleaseStatus", "an unreleased SDK", sdkInfo.ReleaseStatus);
            }
            if (!ReleasePlanSpecHelper.IsValidCommitSha(plan.SpecCommitSHA))
            {
                return Mismatch("SpecCommitSHA", "a stored 40-character commit SHA (no target update in progress)", plan.SpecCommitSHA);
            }
            if (string.IsNullOrWhiteSpace(plan.SpecAPIVersion) ||
                string.Equals(plan.SpecAPIVersion, "none", StringComparison.OrdinalIgnoreCase))
            {
                return Mismatch("SpecAPIVersion", "an explicit stored API version", plan.SpecAPIVersion);
            }
            if (!string.Equals(plan.SDKReleaseType, "beta", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(plan.SDKReleaseType, "stable", StringComparison.OrdinalIgnoreCase))
            {
                return Mismatch("SDKReleaseType", "beta or stable", plan.SDKReleaseType);
            }

            var projectPath = plan.APISpecProjectPath?.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                return Mismatch("APISpecProjectPath", "a stored TypeSpec project path", plan.APISpecProjectPath);
            }
            // SourceBranch and template parameters are not substitutes for Build.SourceVersion.
            if (!ReleasePlanSpecHelper.IsValidCommitSha(build.SourceVersion) ||
                !string.Equals(build.SourceVersion, plan.SpecCommitSHA, StringComparison.OrdinalIgnoreCase))
            {
                return Mismatch("Build.SourceVersion", plan.SpecCommitSHA, build.SourceVersion);
            }

            var expectedParameters = new Dictionary<string, string>
            {
                ["ApiVersion"] = plan.SpecAPIVersion,
                ["SdkReleaseType"] = plan.SDKReleaseType,
                ["ConfigPath"] = $"{projectPath}/tspconfig.yaml",
                ["ConfigType"] = "TypeSpec",
                ["ReleasePlanWorkItemId"] = plan.WorkItemId.ToString(CultureInfo.InvariantCulture)
            };
            foreach (var (name, expected) in expectedParameters)
            {
                var actual = build.TemplateParameters != null && build.TemplateParameters.TryGetValue(name, out var value)
                    ? value
                    : null;
                // Git paths are case-sensitive; language, version, SHA, and release labels are not.
                var comparison = name == "ConfigPath" ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                if (!string.Equals(expected, actual, comparison))
                {
                    return Mismatch($"TemplateParameters.{name}", expected, actual);
                }
            }

            // Require the exact URL written when this run was queued. A previous run at the same SHA is stale.
            var expectedPipelineUrl = DevOpsService.GetPipelineUrl(build.Id);
            if (!string.Equals(sdkInfo.GenerationPipelineUrl, expectedPipelineUrl, StringComparison.Ordinal))
            {
                return Mismatch($"{languageId}.GenerationPipelineUrl", expectedPipelineUrl, sdkInfo.GenerationPipelineUrl);
            }

            return null;
        }

        internal static string GetLanguageId(string language)
        {
            // Pipeline callers use net/js; work items use Dotnet/JavaScript; CLI callers also use .NET/csharp.
            var name = string.Equals(language, "net", StringComparison.OrdinalIgnoreCase) ? ".NET" : language ?? string.Empty;
            return DevOpsService.MapLanguageToId(DevOpsService.MapLanguageIdToName(name));
        }

        private static string Mismatch(string field, string expected, string? actual)
        {
            return $"SDK generation snapshot mismatch for {field}: expected '{expected}', actual '{(string.IsNullOrWhiteSpace(actual) ? "<missing>" : actual)}'.";
        }
    }
}