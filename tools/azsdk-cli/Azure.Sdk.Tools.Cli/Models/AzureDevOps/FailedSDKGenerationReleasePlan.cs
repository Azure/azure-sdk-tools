// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models.AzureDevOps
{
    /// <summary>
    /// Represents a release plan that had one or more SDK pull request statuses updated to
    /// "Failed to generate SDK." within the last 24 hours, along with the list of languages
    /// (SDK display names) for which the generation failed.
    /// </summary>
    public class FailedSDKGenerationReleasePlan
    {
        public required ReleasePlanWorkItem ReleasePlan { get; set; }

        /// <summary>
        /// SDK display names (e.g. ".NET", "Python") whose pull request status was updated to
        /// "Failed to generate SDK." in the last 24 hours.
        /// </summary>
        public List<string> FailedLanguages { get; set; } = [];
    }
}
