// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.BuildTasks
{
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Utilities;
    using MS.Az.Mgmt.CI.BuildTasks.Models;
    using MS.Az.Mgmt.CI.BuildTasks.Tasks.PreBuild;
    using MS.Az.Mgmt.CI.Common.ExtensionMethods;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class SkipTestExecutionTask : NetSdkBuildTask
    {
        #region const
        const string PROPNAME_SKIP_TEST_EXECUTION = "SkipTestExecution";
        const string PROPNAME_SKIP_BUILD = "SkipBuild";
        const string REPO_ROOT_TOKEN_DIR = ".git";
        #endregion

        #region fields
        string _repositoryRootDirPath;
        #endregion

        #region Properties
        #region Task Input Properties
        public string BuildScope { get; set; }

        public List<string> BuildScopes { get; set; }

        public string ProjectType { get; set; }

        public string ProjectCategory { get; set; }

        public bool ExcludeFromTestExecution { get; set; }
        public bool ExcludeFromBuild { get; set; }
        #endregion

        string RepositoryRootDirPath
        {
            get
            {
                if (string.IsNullOrEmpty(_repositoryRootDirPath))
                {
                    FileSystemUtility fileSysUtil = new FileSystemUtility();
                    _repositoryRootDirPath = fileSysUtil.TraverseUptoRootWithDirToken(REPO_ROOT_TOKEN_DIR);
                    Check.DirectoryExists(_repositoryRootDirPath);
                }

                return _repositoryRootDirPath;
            }

            set
            {
                _repositoryRootDirPath = value;
            }
        }


        public override string NetSdkTaskName => "SkipTestExecution";

        #endregion

        #region Constructor
        public SkipTestExecutionTask() { }

        public SkipTestExecutionTask(string rootDirPath):this()
        {
            RepositoryRootDirPath = rootDirPath;
        }
        #endregion

        #region Public Functions
        public override bool Execute()
        {
            base.Execute();
            if (WhatIf)
            {
                WhatIfAction();
            }
            else
            {
                List<string> ScopedProjects = new List<string>();

                // We will not skip broad build scope (e.g. sdk), the idea is to not to skip all the tests in a broader build scope.
                if (string.IsNullOrWhiteSpace(BuildScope))
                {
                    TaskLogger.LogWarning("BuildScope is required to skip tests.");
                }
                else if (BuildScope.Equals("sdk", StringComparison.OrdinalIgnoreCase))
                {
                    TaskLogger.LogWarning("'{0}' BuildScope is not supported", BuildScope);
                }
                else
                {
                    CategorizeSDKProjectsTask catProj = new CategorizeSDKProjectsTask(RepositoryRootDirPath, BuildScope, BuildScopes, ProjectType, ProjectCategory);
                    catProj.Execute();

                    var sdkProj = catProj.SDK_Projects.Select<SDKMSBTaskItem, string>((item) => item.ItemSpec);
                    var testProj = catProj.Test_Projects.Select<SDKMSBTaskItem, string>((item) => item.ItemSpec);

                    if (sdkProj.NotNullOrAny<string>())
                    {
                        ScopedProjects.AddRange(sdkProj.ToList<string>());
                    }

                    if (testProj.NotNullOrAny<string>())
                    {
                        ScopedProjects.AddRange(testProj.ToList<string>());
                    }

                    UpdateProjects(ScopedProjects);
                    ScopedProjects.Clear();
                }
            }

            return TaskLogger.TaskSucceededWithNoErrorsLogged;
        }
        #endregion

        #region private functions
        void UpdateProjects(List<string> projectList)
        {
            foreach (string projectPath in projectList)
            {
                MsbuildProject msbp = new MsbuildProject(projectPath);

                if (msbp.IsProjectTestType)
                {
                    if (ExcludeFromTestExecution == true)
                    {
                        msbp.AddUpdateProperty(PROPNAME_SKIP_TEST_EXECUTION, "true");
                    }
                    else
                    {
                        msbp.AddUpdateProperty(PROPNAME_SKIP_TEST_EXECUTION, "false");
                    }
                }

                if (ExcludeFromBuild == true)
                {
                    msbp.AddUpdateProperty(PROPNAME_SKIP_BUILD, "true");
                }
                else
                {
                    msbp.AddUpdateProperty(PROPNAME_SKIP_BUILD, "false");
                }
            }
        }

        CategorizeSDKProjectsTask GetCategorizeProjectTask(string BuildScope, string FQBuildScopeDirPath, string projType, string projCategory)
        {

            if (string.IsNullOrEmpty(BuildScope) && string.IsNullOrEmpty(FQBuildScopeDirPath))
            {
                TaskLogger.LogError("Both BuildScope and FullyQualifiedBuildScopeDirPath properties cannot be null/empty. Need at least one property to be set in order for the task to execute.");
            }

            if(!string.IsNullOrWhiteSpace(BuildScope))
            {
                
            }


            if(!string.IsNullOrWhiteSpace(FQBuildScopeDirPath))
            {

            }


            return null;
        }

        #endregion

    }
}
