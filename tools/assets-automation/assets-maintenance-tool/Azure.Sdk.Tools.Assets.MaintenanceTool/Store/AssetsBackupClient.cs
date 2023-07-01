using Azure.Sdk.Tools.Assets.MaintenanceTool.Model;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Store;

/// <summary>
/// Used to write our backup entries.
/// </summary>
public class AssetsBackupClient
{
    public AssetsBackupClient()
    {

    }

    public AssetsResultSet Backup(AssetsResultSet results, RunConfiguration runConfiguration)
    {
        return new AssetsResultSet(new List<AssetsResult>());
    }

    public void Save(AssetsResultSet results, RunConfiguration runConfiguration) { }
}
