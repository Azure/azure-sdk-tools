using System.CommandLine;
using System.CommandLine.Binding;

namespace Azure.Sdk.Tools.TestProxy.CommandOptions
{
    /// <summary>
    /// CLICommandOptions contain options common to all CLI Commands (Push, Reset, Restore)
    /// </summary>
    public class CLICommandOptions : DefaultOptions
    {
        public string AssetsJsonPath { get; set; }
    }

    public class CLICommandOptionsBinder : BinderBase<CLICommandOptions>
    {
        private readonly Option<string> _storageLocationOption;
        private readonly Option<string> _storagePluginOption;
        private readonly Option<string> _assetsJsonPathOption;

        public CLICommandOptionsBinder(Option<string> storageLocationOption, Option<string> storagePluginOption, Option<string> assetsJsonPathOption)
        {
            _storageLocationOption = storageLocationOption;
            _storagePluginOption = storagePluginOption;
            _assetsJsonPathOption = assetsJsonPathOption;
        }

        protected override CLICommandOptions GetBoundValue(BindingContext bindingContext) =>
            new CLICommandOptions
            {
                StorageLocation = bindingContext.ParseResult.GetValueForOption(_storageLocationOption),
                StoragePlugin = bindingContext.ParseResult.GetValueForOption(_storagePluginOption),
                AssetsJsonPath = bindingContext.ParseResult.GetValueForOption(_assetsJsonPathOption)
            };
    }
}
