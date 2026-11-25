using System;

namespace PipelineGenerator
{
    /// <summary>
    /// Variable groups and service connections granted to post-merge prepared definitions.
    /// Must match the per-language configuration in eng/common/pipelines/templates/jobs/prepare-pipelines.yml;
    /// PostMergeResourceProfileTests enforces this.
    /// </summary>
    public record PostMergeResourceProfile(
        int[] InternalVariableGroups,
        int[] TestVariableGroups,
        string[] InternalServiceConnections,
        string[] TestServiceConnections)
    {
        private static readonly string[] TestConnections =
        {
            "Azure", "azure-sdk-tests", "azure-sdk-tests-preview", "azure-sdk-tests-public",
            "Azure SDK Test Resources - LiveTestSecrets"
        };

        private static readonly string[] InternalConnections =
        {
            "Azure", "Azure SDK Artifacts", "Azure SDK Engineering System", "opensource-api-connection",
            "AzureSDKEngKeyVault Secrets", "Azure SDK PME Managed Identity", "APIView prod deployment"
        };

        public static PostMergeResourceProfile For(string language) => language switch
        {
            "java" => new(new[] { 1, 58, 93 }, new[] { 64 }, InternalConnections, TestConnections),
            "js" => new(new[] { 24, 58, 93 }, new[] { 64 }, InternalConnections, TestConnections),
            "python" => new(new[] { 58, 93 }, new[] { 64 }, InternalConnections, TestConnections),
            "net" => new(new[] { 13, 58, 93 }, new[] { 64 },
                [.. InternalConnections, "Azure SDK Symbols Publishing", "Nuget.org"], TestConnections),
            "go" => new(new[] { 58, 93, 64 }, Array.Empty<int>(), InternalConnections, TestConnections),
            _ => throw new InvalidOperationException($"Unsupported resource profile '{language}'.")
        };
    }
}
