// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.


namespace MS.Az.Mgmt.CI.BuildTasks.BuildTasks.PreBuild
{
    using Microsoft.Build.Framework;
    using MS.Az.Mgmt.CI.BuildTasks.Common;
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
    using System.Threading.Tasks;

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

        #endregion

        #region fields
        GitHubService _ghSvc;
        #endregion

        #region Properties
        #region task input properties
        public Int64 GH_PRNumber { get; set; }

        public Int64 GH_RepositoryId { get; set; }

        /// <summary>
        /// Repository Url that is being used in browser
        /// Not the SSH uri, neither clone uri
        /// This can also contain relative Url
        /// </summary>
        public string GH_RepositoryHtmlUrl { get; set; }

        #endregion

        #region task output properties
        [Output]
        public string[] ScopesFromPR { get; set; }
        #endregion

        public override string NetSdkTaskName => "DetectRPScopeTask";

        GitHubService GHSvc
        {
            get
            {
                if(_ghSvc == null)
                {
                    string accTkn = KVSvc.GetSecret(CommonConstants.AzureAuth.KVInfo.Secrets.GH_AdxSdkNetAcccesToken);
                    _ghSvc = new GitHubService(TaskLogger, accTkn);
                }

                return _ghSvc;
            }
        }

        //long RepoId { get; set; }

        //long PrNumber { get; set; }
        #endregion

        #region Constructor
        public DetectRPScopeTask()
        {
            GH_PRNumber = 0;
            GH_RepositoryHtmlUrl = string.Empty;
            GH_RepositoryId = 0;
        }

        public DetectRPScopeTask(string repoHtmlUrl, Int64 prNumber) : this()
        {
            GH_RepositoryHtmlUrl = repoHtmlUrl.Trim();
            GH_PRNumber = prNumber;
        }
        public DetectRPScopeTask(Int64 repoId, Int64 prNumber) : this()
        {
            GH_RepositoryId = repoId;
            GH_PRNumber = prNumber;
        }

        void Init()
        {
            //string exceptionStringFormat = "Only numeric datatype is supported. Provided value has to be non-negative and non-zero '{0}'";


            if(GH_RepositoryId <= 0)
            {
                if (string.IsNullOrWhiteSpace(GH_RepositoryHtmlUrl))
                {
                    //throw new ArgumentException("Provide either Repository Id or Repositry Url");
                    TaskLogger.LogWarning("Repository Html Url not provided");
                }
            }

            if(GH_PRNumber < 0)
            {
                throw new ArgumentException("Provide non-zero, non-negative PR Number");
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

            if (!string.IsNullOrWhiteSpace(GH_RepositoryHtmlUrl))
            {
                TaskLogger.LogInfo("Trying to get Pr info using PrNumber:'{0}', GitHubUrl:'{1}'", GH_PRNumber.ToString(), GH_RepositoryHtmlUrl);

                string repoName = string.Empty;
                //Split the url
                string[] tokens = GH_RepositoryHtmlUrl.Split(new char[] { Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

                //Get the last token which represents the repository name
                if (tokens != null)
                {
                    repoName = tokens[tokens.Length - 1];
                }

                prFileList = GHSvc.PR.GetPullRequestFileList(repoName, GH_PRNumber);
            }
            else if (GH_RepositoryId > 0)
            {
                TaskLogger.LogInfo("Trying to get Pr info using PrNumber:'{0}', GitHub Repo Id:'{1}'", GH_PRNumber.ToString(), GH_RepositoryId.ToString());
                prFileList = GHSvc.PR.GetPullRequestFileList(GH_RepositoryId, GH_PRNumber);
            }

            TaskLogger.LogInfo(MessageImportance.Low, prFileList, "List of files from PR");
            Dictionary<string, string> RPDirs = FindScopeFromPullRequestFileList(prFileList);

            //Dictionary<string, string> RPDirs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (RPDirs.NotNullOrAny<KeyValuePair<string, string>>())
            {
                finalScopePathList = RPDirs.Select<KeyValuePair<string, string>, string>((item) => item.Key).ToList<string>();
            }

            
            //foreach(string filePath in prFileList)
            //{
            //    string slnDirPath = fileSysUtil.TraverUptoRootWithFileExtension(filePath);

            //    if (Directory.Exists(slnDirPath))
            //    {
            //        if(!RPDirs.ContainsKey(slnDirPath))
            //        {
            //            RPDirs.Add(slnDirPath, slnDirPath);
            //        }
            //    }
            //    else
            //    {
            //        TaskLogger.LogWarning("RPScope Detection: '{0}' does not exists", slnDirPath);
            //    }
            //}

            //TaskLogger.LogInfo("Number of RPs detected", RPDirs);

            //return RPDirs.Select<KeyValuePair<string, string>, string>((item) => item.Key).ToList<string>();

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
            }

            //if (scopeDictionary.NotNullOrAny<KeyValuePair<string, string>>())
            //{
            //    finalScopePathList = scopeDictionary.Select<KeyValuePair<string, string>, string>((item) => item.Key).ToList<string>();
            //}

            return scopeDictionary;

            //else
            //{
            //    TaskLogger.LogError("Provided repo '{0}' is not currently supported", repo.ToString());
            //}
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
