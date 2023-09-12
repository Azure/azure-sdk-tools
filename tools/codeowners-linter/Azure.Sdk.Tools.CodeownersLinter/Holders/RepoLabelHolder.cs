using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersLinter.Constants;
using Azure.Sdk.Tools.CodeOwnersParser;

namespace Azure.Sdk.Tools.CodeownersLinter.Holders
{
    public class RepoLabelHolder
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

        public RepoLabelHolder(string repoLabelBlobStorageURI)
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
