using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Binding;

namespace Azure.Sdk.Tools.TestProxy.CommandOptions
{
    public class StartOptions : DefaultOptions
    {
        public bool Insecure { get; set; }
        public bool Dump { get; set; }
        public bool UniversalOutput { get; set; }

        public int AutoShutdownTime { get; set; }

        public bool StandardProxyMode { get; set; }

        // On the command line, use -- and everything after that becomes arguments to Host.CreateDefaultBuilder
        // For example Test-Proxy start -i -d -- --urls https://localhost:8002 would set AdditionaArgs to a list containing
        // --urls and https://localhost:8002 as individual entries. This is converted to a string[] before being
        // passed to Host.CreateDefaultBuilder
        public IEnumerable<string> AdditionalArgs { get; set; }
    }
    public class StartOptionsBinder : BinderBase<StartOptions>
    {
        private readonly Option<string> _storageLocationOption;
        private readonly Option<string> _storagePluginOption;
        private readonly Option<bool> _insecureOption;
        private readonly Option<bool> _dumpOption;
        private readonly Option<bool> _univeralOutputOption;
        private readonly Option<int> _autoShutdownTime;
        private readonly Option<bool> _standardProxyMode;
        private readonly Argument<string[]> _additionalArgs;
        

        public StartOptionsBinder(Option<string> storageLocationOption, Option<string> storagePluginOption, Option<bool> insecureOption, Option<bool> dumpOption, Option<bool> universalOutput, Option<int> autoShutdownTime, Option<bool> standardProxyMode, Argument<string[]> additionalArgs)
        {
            _storageLocationOption = storageLocationOption;
            _storagePluginOption = storagePluginOption;
            _insecureOption = insecureOption;
            _dumpOption = dumpOption;
            _univeralOutputOption = universalOutput;
            _autoShutdownTime = autoShutdownTime;
            _standardProxyMode = standardProxyMode;
            _additionalArgs = additionalArgs;
        }

        protected override StartOptions GetBoundValue(BindingContext bindingContext) =>
            new StartOptions
            {
                StorageLocation = bindingContext.ParseResult.GetValueForOption(_storageLocationOption),
                StoragePlugin = bindingContext.ParseResult.GetValueForOption(_storagePluginOption),
                Insecure = bindingContext.ParseResult.GetValueForOption(_insecureOption),
                Dump = bindingContext.ParseResult.GetValueForOption(_dumpOption),
                UniversalOutput = bindingContext.ParseResult.GetValueForOption(_univeralOutputOption),
                AutoShutdownTime = bindingContext.ParseResult.GetValueForOption(_autoShutdownTime),
                StandardProxyMode = bindingContext.ParseResult.GetValueForOption(_standardProxyMode),
                AdditionalArgs = bindingContext.ParseResult.GetValueForArgument(_additionalArgs)
            };
    }
}
