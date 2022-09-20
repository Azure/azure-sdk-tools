using CommandLine.Text;
using CommandLine;

namespace Azure.Sdk.Tools.TestProxy.CommandParserOptions
{
    /// <summary>
    /// Any unique options to the push command will reside here.
    /// </summary>
    [Verb("push", HelpText = "Push the assets, referenced by assets.json, into git.")]
    class PushOptions : CLICommandOptions
    {
    }
}
