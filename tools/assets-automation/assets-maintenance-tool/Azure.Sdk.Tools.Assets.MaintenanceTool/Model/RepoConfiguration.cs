using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Model
{
    /// <summary>
    /// Configuration class describing options available while targeting a repository for scanning.
    /// </summary>
    public class RepoConfiguration
    {
        public RepoConfiguration(string repo)
        { 
            Repo = repo;
        }

        /// <summary>
        /// The full orgname/repo-id identifier to access a repo on github. EG: "azure/azure-sdk-for-net"
        /// </summary>
        public string Repo { get; set; }

        /// <summary>
        /// The time from which we will search for commits that contain assets.jsons.
        /// </summary>
        public DateTime ScanStartDate { get; set; } = DateTime.Parse("2022-12-01");

        /// <summary>
        /// The set of branches that we will examine. Defaults to just 'main'.
        /// </summary>
        public List<string> Branches { get; set; } = new List<string> { "main" };
    }
}
