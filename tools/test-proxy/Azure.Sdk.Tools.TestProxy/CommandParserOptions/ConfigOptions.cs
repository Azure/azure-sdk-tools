using CommandLine.Text;
using CommandLine;

namespace Azure.Sdk.Tools.TestProxy.CommandParserOptions
{
    /// <summary>
    /// Any unique options to the push command will reside here.
    /// </summary>
    [Verb("config", HelpText = "Interact with an assets.json.")]
    class ConfigOptions : CLICommandOptions
    {
    }
}
