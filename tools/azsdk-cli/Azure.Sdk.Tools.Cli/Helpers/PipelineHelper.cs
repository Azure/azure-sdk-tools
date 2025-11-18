// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers {
    public static class PipelineHelper
    {
        /// <summary>
        /// Determines if the current environment is a CI pipeline.
        /// </summary>
        public static bool IsRunningInPipeline()
        {
            return Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" ||
                Environment.GetEnvironmentVariable("SYSTEM_TEAMPROJECTID") != null;
        }
    }
}
