using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Model
{
    public class CleanupResult : BackupResult
    {
        public CleanupResult(string repo, string repoCommit, string assetsLocation, string tag, string tagRepo, string backupUri) : base(repo, repoCommit, assetsLocation, tag, tagRepo, backupUri)
        {
        }
    }
}
