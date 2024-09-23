using System.CommandLine;
using System.CommandLine.Binding;

namespace Azure.Sdk.Tools.TestProxy.CommandOptions
{
    /// <summary>
    /// Any unique options to the push command will reside here.
    /// </summary>
    public class PushOptions : CLICommandOptions
    {
        public bool BreakGlass { get; set; }
    }

    public class PushOptionsBinder : BinderBase<PushOptions>
    {
        private readonly Option<string> _storageLocationOption;
        private readonly Option<string> _storagePluginOption;
        private readonly Option<string> _assetsJsonPathOption;
        private readonly Option<bool> _breakGlassOption;

        public PushOptionsBinder(Option<string> storageLocationOption, Option<string> storagePluginOption, Option<string> assetsJsonPathOption, Option<bool> breakGlassOption)
        {
            _storageLocationOption = storageLocationOption;
            _storagePluginOption = storagePluginOption;
            _assetsJsonPathOption = assetsJsonPathOption;
            _breakGlassOption = breakGlassOption;
        }

        protected override PushOptions GetBoundValue(BindingContext bindingContext) =>
            new PushOptions
            {
                StorageLocation = bindingContext.ParseResult.GetValueForOption(_storageLocationOption),
                StoragePlugin = bindingContext.ParseResult.GetValueForOption(_storagePluginOption),
                AssetsJsonPath = bindingContext.ParseResult.GetValueForOption(_assetsJsonPathOption),
                BreakGlass = bindingContext.ParseResult.GetValueForOption(_breakGlassOption),
            };
    }
}
