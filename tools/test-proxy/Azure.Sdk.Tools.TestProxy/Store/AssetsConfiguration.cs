using System.Text.Json.Serialization;
using Azure.Sdk.tools.TestProxy.Common;

namespace Azure.Sdk.Tools.TestProxy.Store
{
    /// <summary>
    /// This class is used to represent any configuration. An assets.json configuration contains all the necessary configuration needed to restore an asset to the local
    /// storage directory of the test-proxy.
    /// </summary>
    public class AssetsConfiguration
    {
        /// <summary>
        /// Contains the relative path from the containing repo root to the location of this assets json location.
        /// Useful for sparse checkout operations.
        /// </summary>
        [JsonIgnore]
        public virtual NormalizedString AssetsJsonRelativeLocation { get; set; }

        /// <summary>
        /// Contains the absolute path to the assets.json.
        /// </summary>
        [JsonIgnore]
        public virtual NormalizedString AssetsJsonLocation { get; set; }

        /// <summary>
        /// Contains the absolute path to the root of the repo. Outside of a git repo, will return disk root.
        /// </summary>
        [JsonIgnore]
        public virtual NormalizedString RepoRoot { get; set; }
    }
}
