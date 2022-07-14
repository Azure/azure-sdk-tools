using System;
using System.IO;

namespace Azure.Sdk.Tools.TestProxy.Store
{
    /// <summary>
    /// This class is used to represent any assets.json configuration. An assets.json configuration contains all the necessary configuration needed to restore an asset to the local storage directory of the test-proxy.
    /// </summary>
    public class GitAssetsConfiguration : AssetsConfiguration
    {
        public string AssetsFileName { get; set; }

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

        /// <summary>
        /// The location of the assets repo for this config.
        /// </summary>
        public string AssetsRepoLocation { get
            {
                return ResolveAssetRepoLocation(true);
            }
        }

        public string ResolveAssetsStoreLocation(bool autoCreate = true)
        {
            var location = Environment.GetEnvironmentVariable("PROXY_ASSETS_FOLDER") ?? Path.Join(RepoRoot, ".assets");
            if (!Directory.Exists(location) && autoCreate)
            {
                Directory.CreateDirectory(location);
            }

            return location;
        }

        public string ResolveAssetRepoLocation(bool autoCreate = true)
        {
            var assetsStore = ResolveAssetsStoreLocation(autoCreate: autoCreate);
            var location = Path.Join(assetsStore, AssetsRepo.Replace("/", "-"));
            if (!Directory.Exists(location) && autoCreate)
            {
                Directory.CreateDirectory(location);
            }

            return location;
        }

        public bool IsAssetsRepoInitialized(bool autoCreate = true)
        {
            var location = Path.Join(ResolveAssetRepoLocation(autoCreate: autoCreate), ".git");

            return Directory.Exists(location);
        }
    }
}
