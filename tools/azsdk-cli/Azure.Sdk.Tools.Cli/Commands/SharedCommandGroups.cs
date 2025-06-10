using Azure.Sdk.Tools.Cli.Contract;
using System.CommandLine;
using System.CommandLine.Parsing;


namespace Azure.Sdk.Tools.Cli.Commands
{
    public static class SharedCommandGroups
    {

        public static readonly CommandGroup AzurePipelines = new CommandGroup(
            Verb: "azp",
            Description: "Azure Pipelines Tool",
            Options: new List<Option>()
        );

        public static readonly CommandGroup EngSys = new CommandGroup(
            Verb: "eng",
            Description: "Internal azsdk engineering system commands",
            Options: new List<Option>()
        );

        public static readonly CommandGroup Cleanup = new CommandGroup(
            Verb: "cleanup",
            Description: "Cleanup commands",
            Options: new List<Option>()
        );
    }
}
