using Azure.Sdk.Tools.Assets.MaintenanceTool.Model;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Cleanup
{
    public class AssetsCleanupClient
    {
        public AssetsCleanupClient() { }

        public CleanupResultSet Cleanup(RunConfiguration config, BackupResultSet backupResult) {
            var result = new CleanupResultSet();
            return result;
        }
    }
}
