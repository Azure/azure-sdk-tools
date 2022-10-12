using CommandLine;

namespace Azure.Sdk.Tools.TestProxy.CommandParserOptions
{
    /// <summary>
    /// DefaultOptions is the base for all CommandParser Verbs. The only options that should go in here are ones common to everything.
    /// </summary>
    class DefaultOptions
    {
        [Option('l', "storage-location", Default = null, HelpText = "The path to the target local git repo. If not provided as an argument, Environment variable TEST_PROXY_FOLDER will be consumed. Lacking both, the current working directory will be utilized.")]
        public string StorageLocation { get; set; }

        [Option('p', "storage-plugin", Default = "GitStore", HelpText = "The plugin for the selected storage, default is Git storage is GitStore. (Currently the only option)")]
        public string StoragePlugin { get; set; }
    }
}
