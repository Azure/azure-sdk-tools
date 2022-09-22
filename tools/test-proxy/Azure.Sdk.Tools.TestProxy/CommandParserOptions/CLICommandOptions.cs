using CommandLine;

namespace Azure.Sdk.Tools.TestProxy.CommandParserOptions
{
    /// <summary>
    /// CLICommandOptions contain options common to all CLI Commands (Push, Reset, Restore)
    /// </summary>
    class CLICommandOptions : DefaultOptions
    {
        [Option('a', "assets-json-path", Required = true, HelpText = "Required for Push/Reset/Restore. This should be a path to a valid assets.json within a language repository.")]
        public string AssetsJsonPath { get; set; }
    }
}
