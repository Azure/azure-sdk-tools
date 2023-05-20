using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Model
{
    /// <summary>
    /// A given RunConfiguration contains multiple repo configurations. This class provides such a container as well as
    /// mappings to pick up configurations from an incoming path.
    /// </summary>
    public class RunConfiguration
    {
        public RunConfiguration() {
            Repos = new List<RepoConfiguration>();
        }

        public RunConfiguration(string configPath)
        {

        }

        public List<RepoConfiguration> Repos { get; set; }
    }
}
