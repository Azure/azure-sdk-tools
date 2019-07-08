// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.BuildTasks
{
    using Microsoft.Build.Framework;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using MS.Az.Mgmt.CI.BuildTasks.Common.ExtensionMethods;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Utilities;
    using MS.Az.Mgmt.CI.BuildTasks.Models;
    using MS.Az.Mgmt.CI.BuildTasks.Tasks.PreBuild;
    using MS.Az.Mgmt.CI.Common.ExtensionMethods;
    using MS.Az.NetSdk.Build.Utilities;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// This task will search projects based on criteria provided
    /// Depending on the provided properties, this task will exclude/include projects from build or test execution
    /// 
    /// This task does not return any output and hence there is no easy way to detect if the task was executed successfully
    /// </summary>
    public class SkipBuildOrTestExecutionTask : NetSdkBuildTask
    {
        #region const
        const string PROPNAME_SKIP_TEST_EXECUTION = "ExcludeFromTest";
        const string PROPNAME_SKIP_BUILD = "ExcludeFromBuild";
        const string REPO_ROOT_TOKEN_DIR = ".git";
        #endregion

        #region fields
        string _repositoryRootDirPath;
        #endregion

        #region Properties
        #region Task Input Properties
        public string BuildScope { get; set; }

        public string BuildScopes { get; set; }

        public string ProjectType { get; set; }

        public string ProjectCategory { get; set; }

        public bool SkipFromTestExecution { get; set; }
        public bool SkipFromBuild { get; set; }
        #endregion

        #region Task output
        /// <summary>
        /// List of project file paths that are either
        /// </summary>
        public ITaskItem ProjectFilePaths { get; set; }
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

        public override string NetSdkTaskName => "SkipBuildOrTestExecutionTask";

        #endregion

        #region Constructor
        public SkipBuildOrTestExecutionTask()
        {
            BuildScope = string.Empty;
            BuildScopes = string.Empty;
            ProjectType = "Test";
            ProjectCategory = "MgmtPlane";
        }

        public SkipBuildOrTestExecutionTask(string rootDirPath) : this()
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
                ScopedProjects = GetProjectsToBeSkiped();
                UpdateProjects(ScopedProjects);
                ScopedProjects.Clear();
            }

            return TaskLogger.TaskSucceededWithNoErrorsLogged;
        }

        protected override void WhatIfAction()
        {
            List<string> ScopedProjects = GetProjectsToBeSkiped();

            if (ScopedProjects.NotNullOrAny<string>())
            {
                TaskLogger.LogInfo("Following Projects will be affected, with following properties, SkipFromTestExecution:'{0}', SkipFromBuild:'{1}'",
                    SkipFromTestExecution.ToString(), SkipFromBuild.ToString());

                TaskLogger.LogInfo(ScopedProjects);
            }
            else
            {
                //TODO: Print all input properties that were provided
                TaskLogger.LogInfo("No projects matched for provided criteria");
            }
        }
        #endregion

        #region private functions

        List<string> GetProjectsToBeSkiped()
        {
            string notSupportedErrorFormat = @"Unable to execute skipping tests on following directory '{0}'";

            List<string> ScopedProjects = new List<string>();

            // We will not skip broad build scope (e.g. sdk), the idea is to not to skip all the tests in a broader build scope.
            if (BuildScope.Equals("sdk", StringComparison.OrdinalIgnoreCase))
            {
                TaskLogger.LogException<NotSupportedException>(notSupportedErrorFormat, BuildScope);
            }
            else if (BuildScopes.Equals("sdk", StringComparison.OrdinalIgnoreCase))
            {
                TaskLogger.LogException<NotSupportedException>(notSupportedErrorFormat, BuildScope);
            }
            else
            {
                string sdkRootDirPath = Path.Combine(RepositoryRootDirPath, "sdk");

                CategorizeSDKProjectsTask catProj = new CategorizeSDKProjectsTask(RepositoryRootDirPath, BuildScope, null, ProjectType, ProjectCategory);
                catProj.BuildScopes = BuildScopes;
                catProj.Execute();

                if (catProj.MultipleScopes.Contains("sdk"))
                {
                    TaskLogger.LogException<NotSupportedException>(notSupportedErrorFormat, sdkRootDirPath);
                }
                else
                {
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
                }
            }

            return ScopedProjects;
        }

        void UpdateProjects(List<string> projectList)
        {
            foreach (string projectPath in projectList)
            {
                TaskLogger.LogInfo("Updating '{0}'", projectPath);
                MsbuildProject msbp = new MsbuildProject(projectPath);

                // We will only set SkipBuild for sdk projects, we will never set skipTestexecution for sdk projects
                if (msbp.IsProjectSdkType)
                {
                    UpdatePropertyValue(msbp, PROPNAME_SKIP_BUILD, SkipFromBuild);
                }

                if (msbp.IsProjectTestType)
                {
                    UpdatePropertyValue(msbp, PROPNAME_SKIP_BUILD, SkipFromBuild);
                    UpdatePropertyValue(msbp, PROPNAME_SKIP_TEST_EXECUTION, SkipFromTestExecution);
                }
            }
        }

        void UpdatePropertyValue(MsbuildProject proj, string propertyName, bool newPropValue)
        {
            string existingVal = proj.GetPropertyValue(propertyName);

            if (string.IsNullOrWhiteSpace(existingVal) && newPropValue == false)
            {
                TaskLogger.LogInfo(MessageImportance.Low, "'{0}' property is not currently set and the new value is '{1}'. No changes will be made", propertyName, newPropValue.ToString());
            }
            else if (existingVal.Equals(newPropValue.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                TaskLogger.LogInfo(MessageImportance.Low, "'{0}' current value is '{1}'. New value requested is '{2}'", propertyName, existingVal, newPropValue.ToString());
            }
            else if (!existingVal.Equals(newPropValue.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                TaskLogger.LogInfo(MessageImportance.Low, "{0} current value:'{1}', new value:'{2}'", propertyName, existingVal, newPropValue.ToString());
                proj.AddUpdateProperty(propertyName, newPropValue.ToString());
            }
        }

        #endregion
    }
}
