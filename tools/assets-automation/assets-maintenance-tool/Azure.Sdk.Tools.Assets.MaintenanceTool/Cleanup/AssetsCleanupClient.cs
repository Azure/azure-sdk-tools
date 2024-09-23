using Azure.Sdk.Tools.Assets.MaintenanceTool.Model;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Cleanup;

public class AssetsCleanupClient
{
    public AssetsCleanupClient() { }

    public AssetsResultSet Cleanup(RunConfiguration config, AssetsResultSet backupResult)
    {
        return new AssetsResultSet(new List<AssetsResult>()); ;
    }
}
