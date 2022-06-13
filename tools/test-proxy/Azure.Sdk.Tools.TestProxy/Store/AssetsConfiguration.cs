using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.TestProxy.Store
{
    /// <summary>
    /// This class is used to represent any assets.json configuration. An assets.json configuration contains all the necessary configuration needed to restore an asset to the local storage directory of the test-proxy.
    /// </summary>
    public class AssetsConfiguration
    {
        /// <summary>
        /// One of two base properties of an AssetsConfiguration. This property contains the relative path from the containing repo root to the location of this assets json location.
        /// Useful for sparse checkout operations.
        /// </summary>
        [JsonIgnore]
        public virtual string AssetsJsonRelativeLocation { get; set; }

        /// <summary>
        /// One of two base properties of an AssetsConfiguration. This property contains the absolute path to the assets.json.
        /// </summary>
        [JsonIgnore]
        public virtual string AssetsJsonLocation { get; set; }
    }
}
