using Azure.Sdk.Tools.Cli.Contract;
using System.CommandLine;
using System.CommandLine.Parsing;


namespace Azure.Sdk.Tools.Cli.Commands
{
    public static class SharedCommandGroups
    {

        public static readonly CommandGroup AzurePipelines = new(
            Verb: "azp",
            Description: "Azure Pipelines Tool",
            Options: []
        );

        public static readonly CommandGroup EngSys = new(
            Verb: "eng",
            Description: "Internal azsdk engineering system commands",
            Options: []
        );

        public static readonly CommandGroup Cleanup = new(
            Verb: "cleanup",
            Description: "Cleanup commands",
            Options: []
        );

        public static readonly CommandGroup Log = new(
            Verb: "log",
            Description: "Log processing commands",
            Options: []
        );
    }
}
