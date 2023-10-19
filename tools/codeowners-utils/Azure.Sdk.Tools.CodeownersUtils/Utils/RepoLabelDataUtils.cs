using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using Azure.Sdk.Tools.CodeownersUtils;
using Azure.Sdk.Tools.CodeownersUtils.Caches;

namespace Azure.Sdk.Tools.CodeownersUtils.Utils
{
    /// <summary>
    /// The RepoLabelData contains the repository label cache as well as methods used for repository label verification.
    /// </summary>
    public class RepoLabelDataUtils
    {
        private string _repository = null;
        private RepoLabelCache _repoLabelCache = null;
        public RepoLabelDataUtils()
        {
        }

        public RepoLabelDataUtils(string repoLabelBlobStorageUri,
                                  string repository)
        {
            _repository = repository;
            _repoLabelCache = new RepoLabelCache(repoLabelBlobStorageUri);
        }

        // This constructor is for testing purposes only.
        public RepoLabelDataUtils(RepoLabelCache repoLabelCache,
                                  string repository)
        {
            _repository = repository;
            _repoLabelCache = repoLabelCache;
        }

        public bool LabelInRepo(string label)
        {
            return _repoLabelCache.RepoLabelDict[_repository].Contains(label);
        }

        /// <summary>
        /// Check to verify that repository label data exists. If it doesn't, that means this
        /// is running in a repostiory it shouldn't be.
        /// </summary>
        /// <returns>True if label data exists for the repository.</returns>
        public bool RepoLabelDataExists()
        {
            return _repoLabelCache.RepoLabelDict.ContainsKey(_repository);
        }
    }
}
