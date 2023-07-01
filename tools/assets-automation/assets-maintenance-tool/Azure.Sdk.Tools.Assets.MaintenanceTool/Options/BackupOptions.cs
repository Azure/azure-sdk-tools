using System.CommandLine;
using System.CommandLine.Binding;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Options;

public class BackupOptions : BaseOptions { }

public class BackupOptionsBinder : BaseOptionsBinder
{
    public BackupOptionsBinder(Option<string> configLocationOption) : base(configLocationOption) { }
}

