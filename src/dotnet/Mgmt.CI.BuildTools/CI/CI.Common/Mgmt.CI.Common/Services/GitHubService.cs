// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.Common.Services
{
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using MS.Az.Mgmt.CI.BuildTasks.Common.ExtensionMethods;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Logger;
    using MS.Az.Mgmt.CI.Common.ExtensionMethods;
    using MS.Az.Mgmt.CI.Common.Models;
    using Octokit;
    using Octokit.Internal;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;

    /// <summary>
    /// Access to Github api model
    /// </summary>
    public class GitHubService : NetSdkUtilTask
    {
        #region const

        #endregion

        #region fields
        GitHubClient _octokitClient;
        Credentials _githubCredentials;
        InMemoryCredentialStore _credentialStore;
        ProductHeaderValue _myProductInfo;
        PrSvc _pr;
        #endregion

        #region Properties

        //public bool IsRepoAuthorized
        //{
        //    get
        //    {
        //        bool isAuthorized = false;
        //        try
        //        {
        //            if (OctoClient != null)
        //            {
        //                if (OctoClient.User != null)
        //                {
        //                    isAuthorized = true;
        //                }
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            UtilLogger.LogInfo("And error occured while initialzing Octokit client", ex.ToString());
        //        }

        //        return isAuthorized;
        //    }
        //}

        public PrSvc PR
        {
            get
            {
                if (_pr == null)
                {
                    _pr = new PrSvc(OctoClient, UtilLogger);
                }

                return _pr;
            }
        }

        ProductHeaderValue MyProductInfo
        {
            get
            {
                if (_myProductInfo == null)
                {
                    Type thisType = this.GetType();
                    _myProductInfo = new ProductHeaderValue(thisType.FullName, thisType.Assembly.GetName().Version.ToString());
                }
                return _myProductInfo;
            }
        }

        Credentials GitHubCredentials
        {
            get
            {
                if (_githubCredentials == null)
                {
                    //TODO: Find the apiKey For the user that has access to both repo (public/private) in the new flow
                    //_githubCredentials = new Credentials(KVSvc.GetSecret(CommonConstants.AzureAuth.KVInfo.Secrets.GH_AdxSdkNetAcccesToken));
                    _githubCredentials = new Credentials(GHAccessToken);
                }

                return _githubCredentials;
            }
        }

        InMemoryCredentialStore CredentialStore
        {
            get
            {
                if (_credentialStore == null)
                {
                    _credentialStore = new InMemoryCredentialStore(GitHubCredentials);
                }
                return _credentialStore;
            }
        }

        public GitHubClient OctoClient
        {
            get
            {
                if (_octokitClient == null)
                {
                    _octokitClient = new GitHubClient(MyProductInfo);
                    _octokitClient.Credentials = GitHubCredentials;
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                }

                return _octokitClient;
            }
        }

        string GHAccessToken { get; set; }
        #endregion

        #region Constructor
        public GitHubService(NetSdkBuildTaskLogger utilLog, string ghAccessToken) : base(utilLog)
        {
            GHAccessToken = ghAccessToken;
        }
        #endregion

        #region Public Functions

        /// <summary>
        /// Get Repository Information for any repo under Azure organization
        /// </summary>
        /// <param name="repoName"></param>
        /// <returns></returns>
        public Repository GetRepository(string repoName)
        {
            Repository r = OctoClient.Repository.Get("Azure", repoName).GetAwaiter().GetResult();
            return r;
        }

        /// <summary>
        /// Get list of files for a particular commit
        /// </summary>
        /// <param name="repoDirPath"></param>
        /// <param name="gitHubForkName"></param>
        /// <param name="gitHubRepoName"></param>
        /// <param name="refCommit"></param>
        /// <returns></returns>
        public IEnumerable<String> GetDownloadUrlForFilesUnderDirectory(String repoDirPath, String gitHubForkName, String gitHubRepoName, String refCommit)
        {
            try
            {
                return OctoClient.Repository.Content.GetAllContentsByRef(gitHubForkName, gitHubRepoName, repoDirPath, refCommit).Result.Select(item => item.DownloadUrl);
            }
            catch (Exception ex)
            {
                UtilLogger.LogException(ex);
                throw;
            }
        }

        public bool IsRepoAuthorized(string repoUrl)
        {
            bool isAuthorized = false;
            try
            {
                OctoClient.Repository.Get("Azure", repoUrl).GetAwaiter().GetResult();
                isAuthorized = true;
            }
            catch (Exception ex)
            {
                UtilLogger.LogInfo("An error occured while initialzing Octokit client", ex.ToString());
            }

            return isAuthorized;
        }

        #endregion

        #region private functions

        #endregion
    }

    /// <summary>
    /// All functionality related to Pull Requests
    /// </summary>
    public class PrSvc
    {
        #region const
        const int DEFAULT_TIMEOUT_IN_MINUTES = 1;
        #endregion

        #region Properties
        Octokit.GitHubClient OC { get; set; }

        NetSdkBuildTaskLogger Logger { get; set; }

        #endregion

        #region Constructor
        public PrSvc(Octokit.GitHubClient ghc, NetSdkBuildTaskLogger log)
        {
            Logger = log;
            OC = ghc;
        }
        #endregion

        #region Public Functions

        #region Get

        /// <summary>
        /// Get PullRequest info
        /// </summary>
        /// <param name="supportedRepo"></param>
        /// <param name="prNumber"></param>
        /// <returns></returns>
        public PullRequest GetPullRequest(SupportedGitHubRepos supportedRepo, long prNumber)
        {
            string repoName = supportedRepo.GetRepoName();
            Repository r = GetRepository(repoName);
            PullRequest pr = OC.PullRequest.Get(r.Id, (int)prNumber).GetAwaiter().GetResult();
            return pr;
        }

        /// <summary>
        /// Get PullRequest info
        /// </summary>
        /// <param name="repositoryName"></param>
        /// <param name="prNumber"></param>
        /// <returns></returns>
        public PullRequest GetPullRequest(string repositoryName, long prNumber)
        {
            Repository r = GetRepository(repositoryName);
            PullRequest pr = OC.PullRequest.Get(r.Id, (int)prNumber).GetAwaiter().GetResult();
            return pr;
        }

        /// <summary>
        /// Get top 10 pull request sorted in descending order
        /// </summary>
        /// <param name="supportedRepo"></param>
        /// <param name="topCount"></param>
        /// <returns></returns>
        public List<PullRequest> GetTopPullRequests(SupportedGitHubRepos supportedRepo, int topCount = 10)
        {
            if (topCount > 10) Logger.LogException<ApplicationException>("Get more than 10 Pull Requsts is not supported");

            List<PullRequest> prInfoList = new List<PullRequest>();

            Repository r = GetRepository(supportedRepo);

            ApiOptions apiOpt = new ApiOptions();
            apiOpt.PageCount = 1;
            apiOpt.PageSize = topCount;
            PullRequestRequest prr = new PullRequestRequest();
            prr.State = ItemStateFilter.Open;
            prr.SortDirection = SortDirection.Descending;

            IReadOnlyList<PullRequest> prList = OC.PullRequest.GetAllForRepository(r.Id, prr, apiOpt).GetAwaiter().GetResult();

            if (prList.NotNullOrAny<PullRequest>())
            {
                prInfoList = prList.ToList<PullRequest>();
            }

            return prInfoList;
        }

        /// <summary>
        /// Get list of top 10 pr numbers
        /// </summary>
        /// <param name="supportedRepo"></param>
        /// <param name="topCount"></param>
        /// <returns></returns>
        public List<long> GetTopPrNumberList(SupportedGitHubRepos supportedRepo, int topCount = 10)
        {
            List<long> prNumberList = new List<long>();
            var prs = GetTopPullRequests(supportedRepo, topCount);

            if (prs.Any<PullRequest>())
            {
                prNumberList = prs.Select<PullRequest, long>((item) => item.Number).ToList<long>();
            }

            return prNumberList;
        }

        /// <summary>
        /// Get last commit in a particular PR
        /// </summary>
        /// <param name="supportedRepo"></param>
        /// <param name="prNumber"></param>
        /// <returns></returns>
        public string GetLastCommitForPr(SupportedGitHubRepos supportedRepo, long prNumber)
        {
            string repoName = supportedRepo.GetRepoName();
            Repository r = GetRepository(repoName);
            SortedList<DateTime, string> sortedCommitHistory = GetPrWithCommitList(r.Id, prNumber);
            string lastcommit = string.Empty;

            if (sortedCommitHistory.Any<KeyValuePair<DateTime, string>>())
            {
                lastcommit = sortedCommitHistory.Values[sortedCommitHistory.Count - 1];
            }

            return lastcommit;
        }

        /// <summary>
        /// Get sorted list of commit ids in a particular PR number
        /// </summary>
        /// <param name="repoId"></param>
        /// <param name="prNumber"></param>
        /// <returns></returns>
        SortedList<DateTime, string> GetPrWithCommitList(long repoId, long prNumber)
        {
            SortedList<DateTime, string> sortedCommitList = new SortedList<DateTime, string>();
            IReadOnlyList<PullRequestCommit> prCommitList = OC.Repository.PullRequest.Commits(repoId, (int)prNumber).GetAwaiter().GetResult();

            foreach (PullRequestCommit c in prCommitList)
            {
                sortedCommitList.Add(c.Commit.Committer.Date.DateTime, c.Sha);
            }

            return sortedCommitList;
        }

        #endregion

        #region Get PR Files
        /// <summary>
        /// Get list of files in a particular PR number
        /// </summary>
        /// <param name="supportedRepo"></param>
        /// <param name="prNumber"></param>
        /// <returns></returns>
        public IEnumerable<string> GetPullRequestFileList(SupportedGitHubRepos supportedRepo, long prNumber)
        {
            string repoName = supportedRepo.GetRepoName();
            return GetPullRequestFileList(repoName, prNumber);
        }

        /// <summary>
        /// Get List of files of in a particular PR Number for given repo
        /// </summary>
        /// <param name="repoName"></param>
        /// <param name="prNumber"></param>
        /// <returns></returns>
        public IEnumerable<string> GetPullRequestFileList(string repoName, long prNumber)
        {
            Repository repo = GetRepository(repoName);
            return GetPullRequestFileList(repo.Id, prNumber);
        }

        /// <summary>
        /// Get PR file list
        /// </summary>
        /// <param name="repoId"></param>
        /// <param name="prNumber"></param>
        /// <returns></returns>
        public IEnumerable<string> GetPullRequestFileList(long repoId, long prNumber)
        {
            List<string> filePathList = new List<string>();
            try
            {
                IReadOnlyList<PullRequestFile> prFiles = OC.PullRequest.Files(repoId, (int)prNumber).GetAwaiter().GetResult();
                //IEnumerable<string> filePathList = prFiles.Select<PullRequestFile, string>((item) => item.FileName);
                if(prFiles.NotNullOrAny<PullRequestFile>())
                {
                    filePathList = prFiles.Select<PullRequestFile, string>((item) => item.FileName).ToList<string>();
                }
            }
            catch(Exception ex)
            {
                Logger.LogInfo(ex.ToString());
            }

            return filePathList;
        }

        #endregion

        #region Get Content
        /// <summary>
        /// Get commit content
        /// </summary>
        /// <param name="repoDirPath"></param>
        /// <param name="gitHubForkName"></param>
        /// <param name="gitHubRepoName"></param>
        /// <param name="refCommit"></param>
        /// <returns></returns>
        public IEnumerable<RepositoryContent> GetContent(String repoDirPath, String gitHubForkName, String gitHubRepoName, String refCommit)
        {
            return OC.Repository.Content.GetAllContentsByRef(gitHubForkName, gitHubRepoName, repoDirPath, refCommit).GetAwaiter().GetResult();
        }
        #endregion

        #endregion

        #region private functions
        Repository GetRepository(string repoName)
        {
            Repository r = OC.Repository.Get("Azure", repoName).GetAwaiter().GetResult();
            return r;
        }

        Repository GetRepository(SupportedGitHubRepos supportedRepo)
        {
            string repoName = supportedRepo.GetRepoName();
            return GetRepository(repoName);
        }

        #region Merge/Close PR

        bool MergePrInRepo(String ghFork, String ghRepo, int ghPRNumber)
        {
            var prInfo = OC.PullRequest.Get(ghFork, ghRepo, ghPRNumber).GetAwaiter().GetResult();
            if (prInfo.Merged)
            {
                Logger.LogWarning(String.Format("PR '{0}' has already been merged. Skipping merging task.", prInfo.HtmlUrl));
                return true;
            }
            if (prInfo.MergeableState != MergeableState.Clean)
            {
                Logger.LogError(String.Format("Failed to merge PR {0}.", prInfo.HtmlUrl));
                return false;
            }
            OC.PullRequest.Merge(ghFork, ghRepo, ghPRNumber, new MergePullRequest() { MergeMethod = PullRequestMergeMethod.Squash, CommitTitle = prInfo.Title }).Wait();
            return true;
        }

        void ClosePrInRepo(String ghFork, String ghRepo, int ghPRNumber)
        {
            var prInfo = OC.PullRequest.Get(ghFork, ghRepo, ghPRNumber).GetAwaiter().GetResult();
            if (prInfo.State == ItemState.Closed)
            {
                Logger.LogWarning(String.Format("PR {0} has already been closed. Skipping closing task.", prInfo.HtmlUrl));
                return;
            }
            OC.PullRequest.Update(ghFork, ghRepo, ghPRNumber, new PullRequestUpdate() { State = ItemState.Closed }).Wait();
        }

        void PostOrUpdateCommentOnPR(String ghFork, String ghRepo, int ghPrNumber, String comment, String userName, int? commentId = null)
        {
            try
            {
                if (commentId == null)
                {
                    OC.Issue.Comment.Create(ghFork, ghRepo, ghPrNumber, comment).Wait(TimeSpan.FromMinutes(DEFAULT_TIMEOUT_IN_MINUTES));
                }
                else
                {
                    OC.Issue.Comment.Update(ghFork, ghRepo, (int)commentId, comment).Wait(TimeSpan.FromMinutes(DEFAULT_TIMEOUT_IN_MINUTES));
                }

            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                throw;
            }
        }

        IEnumerable<IssueComment> GetAllCommentsByAuthor(String ghFork, String ghRepo, int ghPrNumber, String author)
        {
            try
            {
                var comments = OC.Issue.Comment.GetAllForIssue(ghFork, ghRepo, ghPrNumber).Result;
                return comments.Where(cmt => cmt.User.Login == author);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                throw;
            }
        }

        void UpdatePrState(SupportedGitHubRepos supportedRepo, long prNumber, ItemState prCurrentState, ItemState prNewState)
        {
            if (prCurrentState.Equals(prNewState))
            {
                Logger.LogInfo("Pr#'{0}', requested state '{1}' and current state '{2}' are same. Exiting.....", prNumber.ToString(), prNewState.ToString(), prCurrentState.ToString());
            }

            Repository r = GetRepository(supportedRepo);
            PullRequest pr = GetPullRequest(r.Name, prNumber);
            Logger.LogInfo("Pr#'{0}', requested state to be set:'{1}', current state is:'{2}'", prNumber.ToString(), prNewState.ToString(), pr.State.ToString());

            if (pr.State == prCurrentState)
            {
                PullRequestUpdate pru = new PullRequestUpdate();
                pru.State = prNewState;
                pr = OC.PullRequest.Update(r.Id, (int)prNumber, pru).GetAwaiter().GetResult();

                Logger.LogInfo("Pr#'{0}', requested state to be set:'{1}', current state is:'{2}'", prNumber.ToString(), prNewState.ToString(), pr.State.ToString());
            }

            if (!pr.State.Equals(prNewState))
            {
                Logger.LogError("Unable to set PR state to:'{0}'", prNewState.ToString());
            }
        }
        #endregion

        #endregion
    }
}
