using Azure.Sdk.Tools.Cli.Contract;

namespace Azure.Sdk.Tools.Cli.Commands
{
    /// <summary>
    /// These should be referenced in the CommandHierarchy array for each command if neceessary. We want the metadata for these to be shared.
    /// </summary>
    public static class CommandVerbDescriptions
    {
        public static readonly CommandVerbDescription EngSys = new("eng", "Internal azsdk engineering system commands");
        public static readonly CommandVerbDescription Cleanup = new("cleanup", "Cleanup commands");
        public static readonly CommandVerbDescription AzurePipelines = new("azp", "Azure Pipelines Tool");
    }
}
