namespace Azure.Sdk.Tools.TestProxy.Store
{
    /// <summary>
    /// This class is used to represent any assets.json configuration. An assets.json configuration contains all the necessary configuration needed to restore an asset to the local storage directory of the test-proxy.
    /// </summary>
    public class GitAssetsConfiguration : AssetsConfiguration
    {

        /// <summary>
        /// The targeted assets repo. EG: "Azure/azure-sdk-for-net".
        /// </summary>
        public string AssetsRepo { get; set; }

        /// <summary>
        /// The targeted SHA within the AssetsRepo.
        /// </summary>
        public string SHA { get; set;  }

        /// <summary>
        /// Within the assets repo, is there a prefix that should be inserted prior to writing out files?
        /// </summary>
        public string AssetsRepoPrefixPath { get; set; }

        /// <summary>
        /// The auto-commit branch.
        /// </summary>
        public string AssetsRepoBranch { get; set; }
    }
}
