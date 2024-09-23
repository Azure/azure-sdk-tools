using System.CommandLine;
using System.CommandLine.Binding;

namespace Azure.Sdk.Tools.TestProxy.CommandOptions
{
    /// <summary>
    /// Any unique options to the push command will reside here.
    /// </summary>
    public class ConfigOptions : CLICommandOptions
    {
    }

    public class ConfigOptionsBinder : BinderBase<ConfigOptions>
    {
        private readonly Option<string> _storageLocationOption;
        private readonly Option<string> _storagePluginOption;
        private readonly Option<string> _assetsJsonPathOption;

        public ConfigOptionsBinder(Option<string> storageLocationOption, Option<string> storagePluginOption, Option<string> assetsJsonPathOption)
        {
            _storageLocationOption = storageLocationOption;
            _storagePluginOption = storagePluginOption;
            _assetsJsonPathOption = assetsJsonPathOption;
        }

        protected override ConfigOptions GetBoundValue(BindingContext bindingContext) =>
            new ConfigOptions
            {
                StorageLocation = bindingContext.ParseResult.GetValueForOption(_storageLocationOption),
                StoragePlugin = bindingContext.ParseResult.GetValueForOption(_storagePluginOption),
                AssetsJsonPath = bindingContext.ParseResult.GetValueForOption(_assetsJsonPathOption)
            };
    }
}
