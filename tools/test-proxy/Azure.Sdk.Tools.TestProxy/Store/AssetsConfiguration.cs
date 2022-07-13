using System.Text.Json.Serialization;

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
        public virtual string AssetsJsonRelativeLocation { get; set; }

        /// <summary>
        /// Contains the absolute path to the assets.json.
        /// </summary>
        [JsonIgnore]
        public virtual string AssetsJsonLocation { get; set; }

        /// <summary>
        /// Contains the absolute path to the root of the repo. Outside of a git repo, will return disk root.
        /// </summary>
        [JsonIgnore]
        public virtual string RepoRoot { get; set; }
    }
}
