using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Model
{
    /// <summary>
    /// This class represents a single reference to the assets repo FROM a language repo.
    /// </summary>
    public class ScanResult
    {
        public ScanResult(string repo, string repoCommit, string assetsLocation, string tag, string tagRepo) {
            Repo = repo;
            Commit = repoCommit;
            AssetsLocation = assetsLocation;
            Tag = tag;
            TagRepo = tagRepo;
        }

        /// <summary>
        /// The containing repo from within which this result was found.
        /// </summary>
        public string Repo { get; set; }

        /// <summary>
        /// The SHA of the repo from which this result was generated.
        /// </summary>
        public string Commit { get; set; }

        /// <summary>
        /// The location of the assets.json within the repo from which this result was generated.
        /// </summary>
        public string AssetsLocation { get; set; }

        /// <summary>
        /// What tag in the assets repo is this reference pointed at?
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// Which git repo is being used to store the assets?
        /// </summary>
        public string TagRepo { get; set; }
    }
}
