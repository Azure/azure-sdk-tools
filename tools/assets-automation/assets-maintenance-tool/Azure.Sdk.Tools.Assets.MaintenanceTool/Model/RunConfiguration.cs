using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Model
{
    public class RunConfiguration
    {
        public RunConfiguration() {
            Repos = new List<RepoConfiguration>();
        }

        public List<RepoConfiguration> Repos { get; set; }
    }
}
