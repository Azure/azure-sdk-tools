using CommandLine;

namespace Azure.Sdk.Tools.TestProxy.CommandParserOptions
{
    /// <summary>
    /// Any unique options to the restore command will reside here.
    /// </summary>
    [Verb("restore", HelpText = "Restore the assets, referenced by assets.json, from git.")]
    class RestoreOptions : CLICommandOptions
    {
    }
}
