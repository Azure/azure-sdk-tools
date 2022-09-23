using CommandLine;
using System.Collections.Generic;

namespace Azure.Sdk.Tools.TestProxy.CommandParserOptions
{
    [Verb("start", isDefault: true, HelpText = "Start the TestProxy.")]
    class StartOptions : DefaultOptions
    {
        [Option('i', "insecure", Default = false, HelpText = "Flag; Allow insecure upstream SSL certs.")]
        public bool Insecure { get; set; }

        [Option('d', "dump", Default = false, HelpText = "Flag; Output configuration values when starting the Test-Proxy.")]
        public bool Dump { get; set; }

        // On the command line, use -- and everything after that becomes arguments to Host.CreateDefaultBuilder
        // For example Test-Proxy -i -d -- --urls https://localhost:8002 would set AdditionaArgs to a list containing
        // --urls and https://localhost:8002 as individual entries. This is converted to a string[] before being
        // passed to Host.CreateDefaultBuilder
        [CommandLine.Value(0)]
        public IEnumerable<string> AdditionalArgs { get; set; }

    }

}
