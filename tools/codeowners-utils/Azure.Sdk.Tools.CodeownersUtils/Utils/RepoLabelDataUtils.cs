using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using Azure.Sdk.Tools.CodeownersUtils;
using Azure.Sdk.Tools.CodeownersUtils.Holders;

namespace Azure.Sdk.Tools.CodeownersUtils.Utils
{
    /// <summary>
    /// The OwnerData contains the repository label data as well as methods used for repository label verification.
    /// </summary>
    public class RepoLabelDataUtils
    {
        private string _repository = null;
        private RepoLabelHolder _repoLabelHolder = null;
        public RepoLabelDataUtils()
        {
        }

        public RepoLabelDataUtils(string repoLabelBlobStorageUri,
                                  string repository)
        {
            _repository = repository;
            _repoLabelHolder = new RepoLabelHolder(repoLabelBlobStorageUri);
        }

        // This constructor is for testing purposes only.
        public RepoLabelDataUtils(RepoLabelHolder repoLabelHolder,
                                  string repository)
        {
            _repository = repository;
            _repoLabelHolder = repoLabelHolder;
        }

        public bool LabelInRepo(string label)
        {
            return _repoLabelHolder.RepoLabelDict[_repository].Contains(label);
        }

        /// <summary>
        /// Check to verify that repository label data exists. If it doesn't, that means this
        /// is running in a repostiory it shouldn't be.
        /// </summary>
        /// <returns>True if label data exists for the repository.</returns>
        public bool RepoLabelDataExists()
        {
            return _repoLabelHolder.RepoLabelDict.ContainsKey(_repository);
        }
    }
}
