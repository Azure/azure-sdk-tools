using System.CommandLine;

namespace Azure.Sdk.Tools.Cli.Commands
{
    public static class SharedCommandGroups
    {

        public static readonly CommandGroup AzurePipelines = new(
            Verb: "azp",
            Description: "Azure Pipelines commands",
            Aliases: ["pipeline"]
        );

        public static readonly CommandGroup EngSys = new(
            Verb: "eng",
            Description: "Internal azsdk engineering system commands"
        );

        public static readonly CommandGroup Cleanup = new(
            Verb: "cleanup",
            Description: "Cleanup commands"
        );

        public static readonly CommandGroup Config = new(
            Verb: "config",
            Description: "SDK service configuration commands"
        );

        public static readonly CommandGroup Log = new(
            Verb: "log",
            Description: "Log processing commands"
        );

        public static readonly CommandGroup Package = new(
            Verb: "pkg",
            Description: "Package operations",
            Aliases: ["package"]
        );

        public static readonly CommandGroup PackageReadme = new(
            Verb: "readme",
            Description: "README operations for SDK packages"
        );

        public static readonly CommandGroup PackageSample = new(
            Verb: "sample",
            Description: "Sample operations for SDK packages"
        );

        public static readonly CommandGroup PackageTest = new(
            Verb: "test",
            Description: "Test operations for SDK packages"
        );

        public static readonly CommandGroup TypeSpec = new(
            Verb: "tsp",
            Description: "Commands for setting up or working with TypeSpec projects",
            Aliases: ["typespec"]
        );

        public static readonly CommandGroup TypeSpecProject = new(
            Verb: "project",
            Description: "TypeSpec project utilities"
        );

        public static readonly CommandGroup TypeSpecClient = new(
            Verb: "client",
            Description: "TypeSpec client update helpers"
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
