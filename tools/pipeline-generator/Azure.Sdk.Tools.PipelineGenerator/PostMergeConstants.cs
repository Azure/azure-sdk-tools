namespace PipelineGenerator
{
    /// <summary>
    /// Fixed Azure DevOps targets for post-merge preparation. These are intentionally not configurable.
    /// Copies outside C# must match: eng/pipelines/pipeline-generation-post-merge.yml (stage condition and
    /// environment) and eng/scripts/Initialize-PostMergeSdk.ps1 (trusted context check).
    /// </summary>
    public static class PostMergeConstants
    {
        public const string Organization = "https://dev.azure.com/azure-sdk";
        public const string Project = "internal";
        public const string ApiBaseUrl = Organization + "/" + Project + "/_apis/";

        // Azure Pipelines sets SYSTEM_COLLECTIONURI with a trailing slash.
        public const string CollectionUri = Organization + "/";

        public const string ToolsRepository = "Azure/azure-sdk-tools";
        public const string ToolsBranch = "refs/heads/main";

        public const string LockEnvironment = "post-merge-pipeline-preparation";

        // Azure DevOps check type ID for "Exclusive lock".
        public const string ExclusiveLockCheckTypeId = "2ef31ad6-baa0-403a-8b45-2cbc9b4e5563";

        // Build property key that correlates a queued release run with its preparation request.
        public const string PropertyName = "postMergePreparation";
    }
}
