// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.


namespace MS.Az.Mgmt.CI.BuildTasks.BuildTasks.PreBuild
{
    using Microsoft.Build.Framework;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using MS.Az.Mgmt.CI.BuildTasks.Common.ExtensionMethods;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Utilities;
    using MS.Az.Mgmt.CI.Common.ExtensionMethods;
    using MS.Az.Mgmt.CI.Common.Models;
    using MS.Az.Mgmt.CI.Common.Services;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Based on the values passed during PR validation
    /// 
    /// Repo Id: Azure/azure-sdk-for-net
    /// Commit Id: cbb60cf07c655e38ffa3ffa66cf7779b8e5d08c5
    /// Pull Request No: 6453
    /// Pull Request Id: 284151530
    /// Head Branch: envs
    /// Base fork: Azure/azure-sdk-for-net
    /// Base Branch: master
    /// 
    /// echo Repo Id: $(Build.Repository.ID)
    /// echo Commit Id: $(Build.SourceVersion)
    /// echo Pull Request No: $(System.PullRequest.PullRequestNumber)
    /// echo Pull Request Id: $(System.PullRequest.PullRequestId)
    /// echo Head Branch: $(System.PullRequest.SourceBranch)
    /// echo Base fork: $(Build.Repository.Name)
    /// echo Base Branch: $(System.PullRequest.TargetBranch)
    /// </summary>
    public class DetectRPScopeTask : NetSdkBuildTask
    {
        #region const
        const string tkn = @"OTlkZGI2ZTFjYjQwYzdhODljYzRlZjJmODgwYmQzYjZmMTI4MTMwZg==";
        #endregion

        #region fields
        GitHubService _ghSvc;
        Int64 _gh_prNumber;
        #endregion

        #region Properties
        #region task input properties
        Int64 GH_PRNumber
        {
            get
            {
                if(string.IsNullOrWhiteSpace(GitHubPRNumber))
                {
                    _gh_prNumber = 0;
                }
                else
                {
                    if(Int64.TryParse(GitHubPRNumber, out Int64 parsedGHPRNum))
                    {
                        _gh_prNumber = parsedGHPRNum;
                    }
                    else
                    {
                        _gh_prNumber = 0;
                    }
                }

                return _gh_prNumber;
            }

            set
            {
                _gh_prNumber = value;
            }
        }

        public string GitHubPRNumber { get; set; }

        //public Int64 GH_RepositoryId { get; set; }

        /// <summary>
        /// Repository Url that is being used in browser
        /// Not the SSH uri, neither clone uri
        /// This can also contain relative Url
        /// </summary>
        public string GitHubRepositoryHtmlUrl { get; set; }

        #endregion

        #region task output properties
        [Output]
        public string[] ScopesFromPR { get; set; }

        [Output]
        public string PRScopeString { get; set; }
        #endregion

        public override string NetSdkTaskName => "DetectRPScopeTask";

        GitHubService GHSvc
        {
            get
            {
                if(_ghSvc == null)
                {
                    // string accTkn = KVSvc.GetSecret(CommonConstants.AzureAuth.KVInfo.Secrets.GH_AdxSdkNetAcccesToken);

                    // hard coding this, the downside is, the read limit can be reached early if this token is misused.
                    // this token does not allow to do any writes, so we should be ok.
                  _ghSvc = new GitHubService(TaskLogger, Encoding.ASCII.GetString(Convert.FromBase64String(Common.CommonConstants.AzureAuth.KVInfo.Secrets.GH_AccTkn)));
                }

                return _ghSvc;
            }
        }
        #endregion

        #region Constructor
        public DetectRPScopeTask()
        {
            GitHubPRNumber = string.Empty;
            GH_PRNumber = 0;
            GitHubRepositoryHtmlUrl = string.Empty;
            PRScopeString = string.Empty;
            ScopesFromPR = new string[] { };
        }

        public DetectRPScopeTask(string repoHtmlUrl, string prNumberString) : this()
        {
            GitHubRepositoryHtmlUrl = repoHtmlUrl.Trim();
            GitHubPRNumber = prNumberString;
        }

        void Init()
        {
            if (string.IsNullOrWhiteSpace(GitHubRepositoryHtmlUrl))
            {
                //throw new ArgumentException("Provide either Repository Id or Repositry Url");
                //TaskLogger.LogWarning("Repository Html Url not provided");
            }

            if (GH_PRNumber < 0)
            {
                //throw new ArgumentException("Provide non-zero, non-negative PR Number");
            }
        }
        #endregion

        #region Public Functions
        public override bool Execute()
        {
            base.Execute();
            Init();

            List<string> validScopes = new List<string>();

            if (GH_PRNumber > 0)
            {
                validScopes = GetRPScopes();
                if (validScopes.NotNullOrAny<string>())
                {
                    ScopesFromPR = validScopes.ToArray<string>();
                    PRScopeString = string.Join(";", ScopesFromPR);
                }
            }
            else
            {
                // This helps in pass thru for scenarios where this task is being invoked without
                // any valid PR info
                ScopesFromPR = validScopes.ToArray<string>();
            }

            return TaskLogger.TaskSucceededWithNoErrorsLogged;
        }

        #endregion

        #region private functions




        /// <summary>
        /// Detect valid scope based on the change list in the PR
        /// Get affected files and find scope based on directory that contains .sln file
        /// </summary>
        /// <returns></returns>
        List<string> GetRPScopes()
        {
            TaskLogger.LogInfo("Trying to get Pr info for PrNumber:'{0}'", GH_PRNumber.ToString());
            FileSystemUtility fileSysUtil = new FileSystemUtility();
            IEnumerable<string> prFileList = null;
            List<string> finalScopePathList = new List<string>();
            List<string> intermediateList = new List<string>();

            if (!string.IsNullOrWhiteSpace(GitHubRepositoryHtmlUrl))
            {
                TaskLogger.LogInfo("Trying to get Pr info using PrNumber:'{0}', GitHubUrl:'{1}'", GH_PRNumber.ToString(), GitHubRepositoryHtmlUrl);

                string repoName = string.Empty;
                //Split the url
                string[] tokens = GitHubRepositoryHtmlUrl.Split(new char[] { Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

                //Get the last token which represents the repository name
                if (tokens != null)
                {
                    repoName = tokens[tokens.Length - 1];
                }

                prFileList = GHSvc.PR.GetPullRequestFileList(repoName, GH_PRNumber);
            }
            //else if (GH_RepositoryId > 0)
            //{
            //    TaskLogger.LogInfo("Trying to get Pr info using PrNumber:'{0}', GitHub Repo Id:'{1}'", GH_PRNumber.ToString(), GH_RepositoryId.ToString());
            //    prFileList = GHSvc.PR.GetPullRequestFileList(GH_RepositoryId, GH_PRNumber);
            //}

            TaskLogger.LogInfo(MessageImportance.Low, prFileList, "List of files from PR");
            Dictionary<string, string> RPDirs = FindScopeFromPullRequestFileList(prFileList);

            if (RPDirs.NotNullOrAny<KeyValuePair<string, string>>())
            {
                intermediateList = RPDirs.Select<KeyValuePair<string, string>, string>((item) => item.Key).ToList<string>();
            }

            if (DetectEnv.IsRunningUnderNonWindows)
            {
                foreach (string scopePath in intermediateList)
                {
                    string newPath = scopePath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    finalScopePathList.Add(newPath);
                }
            }
            else
            {
                finalScopePathList = intermediateList;
            }

            return finalScopePathList;
        }


        /// <summary>
        /// Get list of scopes from PR file list
        /// </summary>
        /// <returns></returns>
        Dictionary<string, string> FindScopeFromPullRequestFileList(IEnumerable<string> prFileList)
        {
            Dictionary<string, string> scopeDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<string> finalScopePathList = new List<string>();
            // Assumption:
            // GH API gives relative paths from root
            // So we assume that we will get relative paths that starts after root

            // Currently only supporting .NET SDK repos (both public and private repos)
            // Taking hard dependency on current directory structure, any change in directory strucutre will affect this functionality
            // Alternate way:
            // We can always make Rest calls to find directory location for .sln file, but this approach will hamper the funtionality due to restriction on Github API rate limit
            // On average .NET SDK PR contains 50 files (that will result in at least minimum 3 REST calls for every PR)
            // hence we are taking hard dependency on directory structure and making certain assumptions based on the repository

            //Assumption: we are interested in paths that start with sdk and we only need sdk/<rpName>/<pkgName>
            foreach (string prFilePath in prFileList)
            {
                if (prFilePath.StartsWith("sdk", StringComparison.OrdinalIgnoreCase) || prFilePath.StartsWith("src", StringComparison.OrdinalIgnoreCase))
                {
                    string[] tokens = prFilePath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                    if (tokens.NotNullOrAny<string>())
                    {
                        if (tokens.Length >= 3)
                        {
                            string relScopePath = string.Empty;
                            if (tokens[0].Equals("src", StringComparison.OrdinalIgnoreCase))
                            {
                                relScopePath = Path.Combine(tokens[1], tokens[2]);
                            }
                            else
                            {
                                relScopePath = Path.Combine(tokens[0], tokens[1], tokens[2]);
                            }

                            relScopePath = AdjustPlatformPaths(relScopePath);

                            if (!relScopePath.EndsWith("_metadata", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!scopeDictionary.ContainsKey(relScopePath))
                                {
                                    scopeDictionary.Add(relScopePath, prFilePath);
                                }
                            }
                        }
                    }
                }
                else if(prFilePath.StartsWith("eng", StringComparison.OrdinalIgnoreCase))
                {
                    // Anything outside of sdk should be treated as if a common props file was changed and so
                    // the entire mgmt sdks should be built and all tests should be ran
                    TaskLogger.LogWarning("Detected common files were changed. All mgmt sdks will be built.");
                    scopeDictionary.Clear();
                    break;
                }
            }

            return scopeDictionary;
        }

        string AdjustPlatformPaths(string givenPath)
        {
            string finalPath = givenPath;
            if (DetectEnv.IsRunningUnderWindowsOS)
            {
                finalPath = givenPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }
            else
            {
                finalPath = givenPath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            return finalPath;
        }

        /// <summary>
        /// Compares the Github Html url and returns supportedGithubRepo enum
        /// TODO: Handle url that ends with '/' or find a robust way to compare html url regardless or trailing '/' or not
        /// </summary>
        /// <param name="githubHtmlUrl"></param>
        /// <returns></returns>
        SupportedGitHubRepos FindRepoEnum(string githubHtmlUrl)
        {
            if (githubHtmlUrl.EndsWith("/"))
            {
                githubHtmlUrl = githubHtmlUrl.TrimEnd('/');
            }

            if (SupportedGitHubRepos.SdkForNet_PrivateRepo.GetDescriptionAttributeValue().Equals(githubHtmlUrl, StringComparison.OrdinalIgnoreCase))
            {
                return SupportedGitHubRepos.SdkForNet_PrivateRepo;
            }
            else if (SupportedGitHubRepos.SdkForNet_PrivateRepo.GetDescriptionAttributeValue().Equals(githubHtmlUrl, StringComparison.OrdinalIgnoreCase))
            {
                return SupportedGitHubRepos.SdkForNet_PublicRepo;
            }
            else
            {
                return SupportedGitHubRepos.UnSupported;
            }
        }
        #endregion
    }
}
