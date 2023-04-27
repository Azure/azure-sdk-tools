using System.CommandLine;
using System.CommandLine.Binding;

namespace Azure.Sdk.Tools.TestProxy.CommandOptions
{
    /// <summary>
    /// Any unique options to the restore command will reside here.
    /// </summary>
    public class RestoreOptions : CLICommandOptions
    {
    }

    public class RestoreOptionsBinder : BinderBase<RestoreOptions>
    {
        private readonly Option<string> _storageLocationOption;
        private readonly Option<string> _storagePluginOption;
        private readonly Option<string> _assetsJsonPathOption;

        public RestoreOptionsBinder(Option<string> storageLocationOption, Option<string> storagePluginOption, Option<string> assetsJsonPathOption)
        {
            _storageLocationOption = storageLocationOption;
            _storagePluginOption = storagePluginOption;
            _assetsJsonPathOption = assetsJsonPathOption;
        }

        protected override RestoreOptions GetBoundValue(BindingContext bindingContext) =>
            new RestoreOptions
            {
                StorageLocation = bindingContext.ParseResult.GetValueForOption(_storageLocationOption),
                StoragePlugin = bindingContext.ParseResult.GetValueForOption(_storagePluginOption),
                AssetsJsonPath = bindingContext.ParseResult.GetValueForOption(_assetsJsonPathOption)
            };
    }
}
