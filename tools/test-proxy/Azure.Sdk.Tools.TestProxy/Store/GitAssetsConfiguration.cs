using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Azure.Sdk.Tools.TestProxy.Common;

namespace Azure.Sdk.Tools.TestProxy.Store
{
    /// <summary>
    /// This class is used to represent any assets.json configuration. An assets.json configuration contains all the necessary configuration needed to restore an asset to the local storage directory of the test-proxy.
    /// </summary>
    public class GitAssetsConfiguration : AssetsConfiguration
    {
        /// <summary>
        /// Populated during ParseConfiguration. This is the actual name of the file containing the parsed AssetsConfiguration. Normally "assets.json", but can be customized.
        /// </summary>
        public string AssetsFileName { get; set; }

        /// <summary>
        /// The targeted assets repo. EG: "Azure/azure-sdk-for-net".
        /// </summary>
        public string AssetsRepo { get; set; }

        /// <summary>
        /// The targeted Tag within the AssetsRepo.
        /// </summary>
        public string Tag { get; set;  }

        /// <summary>
        /// Within the assets repo, is there a prefix that should be inserted prior to writing out files?
        /// </summary>
        public string AssetsRepoPrefixPath { get; set; }

        /// <summary>
        /// Tags are generated and pushed with each changeset. This prefix will inform the name of the tag to be recognizable as soemthing other than a guid.
        /// </summary>
        public string TagPrefix { get; set; }

        /// <summary>
        /// The location of the assets repo for this config.
        /// </summary>
        public string AssetsRepoLocation
        { 
            get { return ResolveAssetRepoLocation(); }
        }

        /// <summary>
        /// Used to resolve the location of the "assets" store location. This is the folder CONTAINING other cloned repos. No git data will be restored or staged directly within this folder.
        /// </summary>
        /// <returns></returns>
        public string ResolveAssetsStoreLocation()
        {
            var location = Environment.GetEnvironmentVariable("PROXY_ASSETS_FOLDER") ?? Path.Join(RepoRoot, ".assets");
            if (!Directory.Exists(location))
            {
                Directory.CreateDirectory(location);
            }

            return location;
        }

        /// <summary>
        /// Resolves the location of the actual folder containing a cloned repository WITHIN the asset store. Git data will be stored directly within this directory.
        /// </summary>
        /// <returns></returns>
        public string ResolveAssetRepoLocation()
        {
            var assetsStore = ResolveAssetsStoreLocation();

            // Combine the AssetsRepo and AssetsJsonRelativeLocation and grab the hash of that.
            // AssetsRepo will be something like Azure/azure-sdk-assets-integration and
            // AssetsJsonRelativeLocation will be something like sdk/<service>/assets.json
            string assetsRepoPlusJsonRelPathLoc = AssetsRepo + AssetsJsonRelativeLocation;

            string shortHashString = ShortHashGenerator.GenerateShortHash(assetsRepoPlusJsonRelPathLoc);

            var location = Path.Join(assetsStore, shortHashString);
            if (!Directory.Exists(location))
            {
                Directory.CreateDirectory(location);
            }

            return location;
        }

        /// <summary>
        /// Checks for whether or not the assets repo is initialized.
        /// </summary>
        /// <param name="autoCreate"></param>
        /// <returns></returns>
        public bool IsAssetsRepoInitialized(bool autoCreate = true)
        {
            var location = Path.Join(ResolveAssetRepoLocation(), ".git");

            return Directory.Exists(location);
        }
    }
}
