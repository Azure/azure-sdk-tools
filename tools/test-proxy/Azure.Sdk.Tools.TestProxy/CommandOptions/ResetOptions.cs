using System.CommandLine;
using System.CommandLine.Binding;

namespace Azure.Sdk.Tools.TestProxy.CommandOptions
{
    /// <summary>
    /// Any unique options to the reset command will reside here.
    /// </summary>
    public class ResetOptions : CLICommandOptions
    {
        public bool ConfirmReset { get; set; }
    }

    public class ResetOptionsBinder : BinderBase<ResetOptions>
    {
        private readonly Option<string> _storageLocationOption;
        private readonly Option<string> _storagePluginOption;
        private readonly Option<string> _assetsJsonPathOption;
        private readonly Option<bool> _confirmResetOption;

        public ResetOptionsBinder(Option<string> storageLocationOption, Option<string> storagePluginOption, Option<string> assetsJsonPathOption, Option<bool> confirmResetOption)
        {
            _storageLocationOption = storageLocationOption;
            _storagePluginOption = storagePluginOption;
            _assetsJsonPathOption = assetsJsonPathOption;
            _confirmResetOption = confirmResetOption;
        }

        protected override ResetOptions GetBoundValue(BindingContext bindingContext) =>
            new ResetOptions
            {
                StorageLocation = bindingContext.ParseResult.GetValueForOption(_storageLocationOption),
                StoragePlugin = bindingContext.ParseResult.GetValueForOption(_storagePluginOption),
                AssetsJsonPath = bindingContext.ParseResult.GetValueForOption(_assetsJsonPathOption),
                ConfirmReset = bindingContext.ParseResult.GetValueForOption(_confirmResetOption)
            };
    }
}
