using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CodeownersUtils.Constants
{
    /// <summary>
    /// Default constants for the storage URIs if they're not passed in.
    /// </summary>
    public class DefaultStorageConstants
    {
        public const string RepoLabelBlobStorageURI = "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/repository-labels-blob";
        public const string TeamUserBlobUri = "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/azure-sdk-write-teams-blob";
        public const string UserOrgVisibilityBlobStorageURI = "https://azuresdkartifacts.blob.core.windows.net/azure-sdk-write-teams/user-org-visibility-blob";
    }
}
