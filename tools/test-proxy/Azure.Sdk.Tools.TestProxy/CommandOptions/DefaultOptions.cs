using System.CommandLine;
using System.CommandLine.Binding;

namespace Azure.Sdk.Tools.TestProxy.CommandOptions
{
    public class DefaultOptions
    {
        public string StorageLocation { get; set; }

        public string StoragePlugin { get; set; }
    }

    public class DefaultOptsBinder : BinderBase<DefaultOptions>
    {
        private readonly Option<string> _storageLocationOption;
        private readonly Option<string> _storagePluginOption;

        public DefaultOptsBinder(Option<string> storageLocationOption, Option<string> storagePluginOption)
        {
            _storageLocationOption = storageLocationOption;
            _storagePluginOption = storagePluginOption;
        }

        protected override DefaultOptions GetBoundValue(BindingContext bindingContext) =>
            new DefaultOptions
            {
                StorageLocation = bindingContext.ParseResult.GetValueForOption(_storageLocationOption),
                StoragePlugin = bindingContext.ParseResult.GetValueForOption(_storagePluginOption)
            };
    }
}
