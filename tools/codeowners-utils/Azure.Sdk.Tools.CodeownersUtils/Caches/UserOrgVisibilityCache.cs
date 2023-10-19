using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using Azure.Sdk.Tools.CodeownersUtils.Utils;

namespace Azure.Sdk.Tools.CodeownersUtils.Caches
{
    /// <summary>
    /// Holder for the user/org visibility data. The users are the all inclusive list of user under azure-sdk-write meaning
    /// that it's effectively a deduped list of users from azure-sdk-write team and its child teams. Owners are case
    /// insensitive but case preserving so this needs to be able to do case insensitive lookups.
    /// </summary>
    public class UserOrgVisibilityCache
    {
        private string UserOrgVisibilityBlobStorageURI { get; set; } = DefaultStorageConstants.UserOrgVisibilityBlobStorageURI;
        private Dictionary<string, bool> _userOrgDict = null;

        public Dictionary<string, bool> UserOrgVisibilityDict
        {
            get
            {
                if (_userOrgDict == null)
                {
                    _userOrgDict = GetUserOrgVisibilityData();
                }
                return _userOrgDict;
            }
            set
            {
                _userOrgDict = value;
            }
        }

        public UserOrgVisibilityCache(string userOrgVisibilityBlobStorageUri)
        {
            if (!string.IsNullOrWhiteSpace(userOrgVisibilityBlobStorageUri))
            {
                UserOrgVisibilityBlobStorageURI = userOrgVisibilityBlobStorageUri;
            }
        }

        private Dictionary<string, bool> GetUserOrgVisibilityData()
        {
            if (null == _userOrgDict)
            {
                string rawJson = FileHelpers.GetFileOrUrlContents(UserOrgVisibilityBlobStorageURI);
                var tempDict = JsonSerializer.Deserialize<Dictionary<string, bool>>(rawJson);
                // The StringComparer needs to be set in order to do an case insensitive lookup. GitHub's teams
                // and users are case insensitive but case preserving. This means that a user's login can be
                // SomeUser but, in a CODEOWNERS file, can be @someuser and it's the same user.
                _userOrgDict = new Dictionary<string, bool>(tempDict, StringComparer.InvariantCultureIgnoreCase);
            }
            return _userOrgDict;
        }
    }
}
