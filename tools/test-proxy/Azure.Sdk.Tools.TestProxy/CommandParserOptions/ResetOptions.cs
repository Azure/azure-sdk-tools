using CommandLine;

namespace Azure.Sdk.Tools.TestProxy.CommandParserOptions
{
    /// <summary>
    /// Any unique options to the reset command will reside here.
    /// </summary>
    [Verb("reset", HelpText = "Reset the assets, referenced by assets.json, from git to their original files referenced by the tag. Will prompt if there are pending changes unless indicated by -y/--yes.")]
    class ResetOptions : CLICommandOptions
    {

        [Option('y', "yes", Default = null, HelpText = "Skip prompt for reset when discarding pending changes.")]
        public string ConfirmReset { get; set; }
    }
}
