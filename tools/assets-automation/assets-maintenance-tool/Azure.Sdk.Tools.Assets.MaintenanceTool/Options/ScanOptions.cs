using System.CommandLine;
using System.CommandLine.Binding;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Options;

public class ScanOptions : BaseOptions { }

public class ScanOptionsBinder : BaseOptionsBinder
{
    public ScanOptionsBinder(Option<string> configLocationOption) : base(configLocationOption){ }
}
