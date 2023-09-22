using System.CommandLine;
using System.CommandLine.Binding;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Options;

public class BaseOptions
{
    public string ConfigLocation { get; set; } = string.Empty;
}

public class BaseOptionsBinder : BinderBase<BaseOptions>
{
    private readonly Option<string> _configLocationOption;

    public BaseOptionsBinder(Option<string> configLocationOption)
    {
        _configLocationOption = configLocationOption;
    }

    protected override BaseOptions GetBoundValue(BindingContext bindingContext)
    {
        var result = bindingContext.ParseResult.GetValueForOption(_configLocationOption);

        if (result != null)
        {
            return new BaseOptions
            {
                ConfigLocation = result
            };
        }
        else
        {
            return new BaseOptions
            {
                ConfigLocation = string.Empty
            };
        }
    }
}
