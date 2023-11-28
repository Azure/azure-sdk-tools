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
    /// Holder for the repo label dictionary. Lazy init, it doesn't pull the data from the URI until first use.
    /// The GetRepoLabelData is interesting because the repo/label data is a dictionary of hashsets but, both
    /// the dictionary and all of the hashsets need to be case insensitive because GitHub is case insensitive
    /// but case preserving when it comes to labels. This means that MyLabel, mylabel and mYlAbEl are effectively
    /// the same to GitHub and, let's face it, the CODEOWNERS files are all over the place with exact vs non-exact
    /// casing.
    /// </summary>
    public class RepoLabelCache
    {
        private string RepoLabelBlobStorageURI { get; set; } = DefaultStorageConstants.RepoLabelBlobStorageURI;
        private Dictionary<string, HashSet<string>> _repoLabelDict = null;

        public Dictionary<string, HashSet<string>> RepoLabelDict
        {
            get
            {
                if (_repoLabelDict == null)
                {
                    _repoLabelDict = GetRepoLabelData();
                }
                return _repoLabelDict;
            }
            set
            {
                _repoLabelDict = value;
            }
        }

        public RepoLabelCache(string repoLabelBlobStorageURI)
        {
            if (!string.IsNullOrWhiteSpace(repoLabelBlobStorageURI))
            {
                RepoLabelBlobStorageURI = repoLabelBlobStorageURI;
            }
        }

        private Dictionary<string, HashSet<string>> GetRepoLabelData()
        {
            if (null == _repoLabelDict)
            {
                string rawJson = FileHelpers.GetFileOrUrlContents(RepoLabelBlobStorageURI);
                 var tempDict = JsonSerializer.Deserialize<Dictionary<string, HashSet<string>>>(rawJson);
                // The StringComparer needs to be set in order to do an case insensitive lookup for both
                // the repository (the dictionary key) and for the HashSet of labels.
                _repoLabelDict = new Dictionary<string, HashSet<string>>(StringComparer.InvariantCultureIgnoreCase);
                foreach (KeyValuePair<string, HashSet<string>> entry in tempDict)
                {
                    _repoLabelDict.Add(entry.Key, new HashSet<string>(entry.Value, StringComparer.InvariantCultureIgnoreCase));
                }
            }
            return _repoLabelDict;
        }
    }
}
