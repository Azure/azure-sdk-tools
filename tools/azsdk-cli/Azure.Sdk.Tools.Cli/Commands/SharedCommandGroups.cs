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

        public static readonly CommandGroup Generators = new(
            Verb: "generators",
            Description: "Commands that generate files",
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

        public static readonly CommandGroup Package = new(
            Verb: "package",
            Description: "Package management and validation commands",
            Options: []
        );

        public static readonly CommandGroup SourceCode = new(
            Verb: "source-code",
            Description: "Source code generation and build commands",
            Options: []
        );

        public static readonly CommandGroup TypeSpec = new(
            Verb: "tsp",
            Description: "Tools for setting up or working with TypeSpec projects",
            Options: []
        );

#if DEBUG
        public static readonly CommandGroup Example = new(
            Verb: "example",
            Description: "Example tool demonstrating framework features",
            Options: []
        );

        public static readonly CommandGroup Demo = new(
            Verb: "demo",
            Description: "Demo commands exercising services and helpers",
            Options: []
        );
#endif
    }
}
