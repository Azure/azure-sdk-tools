using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Model
{
    /// <summary>
    /// A given RunConfiguration contains multiple repo configurations.
    /// </summary>
    public class RunConfiguration
    {
        public RunConfiguration() {
            Repos = new List<RepoConfiguration>();
        }

        public List<RepoConfiguration> Repos { get; set; }
    }
}
