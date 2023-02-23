using CommandLine;

namespace Azure.Sdk.Tools.TestProxy.CommandParserOptions.ConfigVerbs
{
    /// <summary>
    /// This option allows combines with arg skipping in Main() to allow access to simple sub-verb under command "config"
    /// </summary>
    [Verb("locate", HelpText = "Show the location of the restored code for a given assets.json.")]
    class ShowOptions : CLICommandOptions
    {

    }
}
