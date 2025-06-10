using Azure.Sdk.Tools.Cli.Contract;
using System.CommandLine;
using System.CommandLine.Parsing;


namespace Azure.Sdk.Tools.Cli.Commands
{
    /// <summary>
    /// These should be referenced in the CommandHierarchy array for each command if necessary.
    /// Every access returns new instances so you never reuse the same Option or List when building up the command tree.
    /// </summary>
    public static class SharedCommandGroups
    {
        public static Option<string> CheckThisOption
        {
            get => new Option<string>(
                        aliases: new[] { "--meep", "-m" },
                        description: "This should be settable anywhere in the assigned command",
                        getDefaultValue: () => "oops"
                   )
            {
                IsRequired = false
            };
        }

        public static CommandGroup AzurePipelines
        {
            get => new CommandGroup(
                       Verb: "azp",
                       Description: "Azure Pipelines Tool",
                       Options: new List<Option> { CheckThisOption }
                   );
        }

        public static CommandGroup EngSys
        {
            get => new CommandGroup(
                       Verb: "eng",
                       Description: "Internal azsdk engineering system commands",
                       Options: new List<Option>()
                   );
        }

        public static CommandGroup Cleanup
        {
            get => new CommandGroup(
                       Verb: "cleanup",
                       Description: "Cleanup commands",
                       Options: new List<Option>()
                   );
        }
    }
}
