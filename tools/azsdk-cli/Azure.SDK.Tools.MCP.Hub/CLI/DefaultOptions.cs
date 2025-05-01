using System.CommandLine;
using System.CommandLine.Binding;

namespace Azure.SDK.Tools.MCP.Hub.CLI
{
    // todo: populate the option tree for the server run defaults
    // todo: figure out direct tool invocation if not running server
    public class DefaultOptions
    {
        public string WorkingDirectory { get; set; }
    }

    public class DefaultOptsBinder : BinderBase<DefaultOptions>
    {
        private readonly Option<string> _workingDirectoryOption;

        public DefaultOptsBinder(Option<string> workingDirectoryOption, Option<string> storagePluginOption)
        {
            _workingDirectoryOption = workingDirectoryOption;
        }

        protected override DefaultOptions GetBoundValue(BindingContext bindingContext) =>
            new DefaultOptions
            {
                WorkingDirectory = bindingContext.ParseResult.GetValueForOption(this._workingDirectoryOption),
            };
    }
}
