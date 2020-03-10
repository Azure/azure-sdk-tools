using CommandLine.Attributes;
using CommandLine.Attributes.Advanced;

namespace CreateRuleFabricBot.CommandLine
{
    public class CommandLineArgs
    {
        [ActionArgument]
        public ActionToTake Action { get; set; }

        [CommonArgument]
        [RequiredArgument(0, "org", "The org the repo is in")]
        public string Owner { get; set; }

        [CommonArgument]
        [RequiredArgument(1, "repo", "The name of the repo")]
        public string Repo { get; set; }

        [ArgumentGroup(nameof(ActionToTake.create))]
        [ArgumentGroup(nameof(ActionToTake.update))]
        [RequiredArgument(2, "servicesFile", "The file containing the service table. Structure for the table Labels: Column1, Handles: Column3")]
        public string ServicesFile { get; set; }

        [ArgumentGroup(nameof(ActionToTake.delete))]
        [RequiredArgument(2, "task", "The task id to delete.")]
        public string TaskId { get; set; }

        [CommonArgument]
        [OptionalArgument(null, "token", "The cookie token for authentication")]
        public string CookieToken { get; set; }

        [CommonArgument]
        [OptionalArgument(true, "prompt", "Don't prompt for validation")]
        public bool Prompt { get; set; }
    }
}
