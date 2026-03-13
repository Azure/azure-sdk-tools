namespace Azure.Sdk.Tools.Cli.Commands
{
    public static class SharedCommandGroups
    {

        public static readonly CommandGroup AzurePipelines = new(
            Verb: "ci",
            Description: "Get pipeline/checks information for pull requests and azure pipelines",
            Aliases: ["azp", "pipeline"]
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
            Verb: "samples",
            Description: "Sample operations for SDK packages"
        );

        public static readonly CommandGroup PackageTest = new(
            Verb: "test",
            Description: "Test operations for SDK packages"
        );

        public static readonly CommandGroup ReleasePlan = new(
           Verb: "release-plan",
           Description: "Manage release plans in Azure DevOps"
       );

        public static readonly CommandGroup TypeSpec = new(
            Verb: "tsp",
            Description: "Set up and work with TypeSpec projects",
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

        public static readonly CommandGroup Verify = new(
            Verb: "verify",
            Description: "Verify project environments",
            Options: []
        );

        public static readonly CommandGroup Setup = new(
            Verb: "setup",
            Description: "Environment setup verification and installation"
        );

        public static readonly CommandGroup APIView = new(
            Verb: "apiview",
            Description: "Interact with APIView services and functionality",
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
