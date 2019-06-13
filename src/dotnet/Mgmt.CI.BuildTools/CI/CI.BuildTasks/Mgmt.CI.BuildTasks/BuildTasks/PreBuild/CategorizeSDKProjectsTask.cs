// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Tasks.PreBuild
{
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Utilities;
    using MS.Az.Mgmt.CI.BuildTasks.Models;
    using MS.Az.NetSdk.Build.Models;
    using MS.Az.NetSdk.Build.Utilities;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using MS.Az.Mgmt.CI.BuildTasks.Common.ExtensionMethods;
    using MS.Az.Mgmt.CI.Common.ExtensionMethods;

    /// <summary>
    /// Vocabulary:
    /// Scope: Scope is used to flag a project set of projects (e.g. sdk\Compute) will search all project for compute
    /// 
    /// This task is primarily responsible for searching projects and categorizing it based on various parameters.
    /// This task will select projects to build, test that are platform specific as well as scope specific.
    /// Swagger to SDK also relies on these set of tools to selectively build, test and create nuget pacakges based on scope for specific swagger specs
    /// 
    /// Currently supported scenarios
    /// 1) Search projects when no scope is provided
    /// 2) Search projects when scope is provided (partial scope)
    /// 3) Search projects when fully qualified scope dir path is provided
    /// 4) Search projects when ProjectType is specified
    /// 5) Search projects when ProjectCategory is specified (not extensively tested/checked for data plane project types)
    /// 6) Search projects when Include/Exclude tokens are specified at command line
    /// 7) Search project honoring exclusion/inclusion settings specified in project files (swagger to SDK scenarios)
    /// 
    /// At end of this task, it returns the following outputs
    /// 1) List of SDK Projects that will be built
    /// 2) List of Test Projects that will be built
    /// 3) List of Test Projects whose tests will be executed
    /// 4) List of Projects which are currently not supported for consistency sake (e.g. if an RP wants to target SDK project to a target framework that is currently not supported acros the entire repository)
    /// 5) List of Package references that will be passed on to other tragets (e.g. cleaning targets to clean packages from nuget caches)
    /// </summary>
    public class CategorizeSDKProjectsTask : NetSdkBuildTask
    {
        #region const
        const string ALL_SCOPE = "all";
        const string MGMT_SCOPE = "mgmt";
        const string REPO_ROOT_TOKEN_DIR = ".git";

        //const string default_excludeTokens = @"Batch\Support;D:\adxRepo\netSdk\master\src\SDKCommon\Test\SampleProjectPublish\SampleSDKTestPublish.csproj";
        const string default_excludeTokens = @"Batch\Microsoft.Azure.Batch;Batch\Microsoft.Azure.Batch.FilesConventions;Batch\Microsoft.Azure.Batch.FileStaging;mgmtCommon\Test\SampleProjectPublish\";
        #endregion

        #region fields
        //string _scope;
        string _repositoryRootDirPath;
        string _cmdLineExcludeScope;

        //string _projType;
        //string _projCat;
        //SdkProjectType _projectType;
        //SdkProjectCategory _projectCategory;
        #endregion

        #region Properties

        #region Task Input Properties

        public bool UseLegacyDirStructure { get; set; }

        /// <summary>
        /// Scope is a hint as to which directories to build
        /// If no scope is specified, it's assumed the entire repo needs to be built
        /// </summary>
        public string BuildScope { get; set; }

        /// <summary>
        /// Semicolon seperated scopes, this can be partial or fully qualified scopes
        /// </summary>
        public string BuildScopes { get; set; }

        /// <summary>
        /// Provide list of scopes for the task
        /// This can be either fully qualified scopes or relative scopes
        /// </summary>
        List<string> MultipleScopes { get; set; }

        /// <summary>
        /// Fully qualified Scope Path
        /// This is especially required for Swagger to SDK scenarios
        /// </summary>
        public string FullyQualifiedBuildScopeDirPath { get; set; }

        /// <summary>
        /// Resource Providers to include for applicable targets (clean, build etc)
        /// Setting this will make sure only tokens will be part of the project path
        /// e.g. if you set "Compute", it will make sure only file paths that contain "compute" will be included
        /// </summary>
        public string CmdLineIncludeScope { get; set; }

        /// <summary>
        /// Resource providers to exclude for applicable targets
        /// Setting this will given first priority over "cmdLineIncludeScope"
        /// </summary>
        public string CmdLineExcludeScope
        {
            get
            {
                if(string.IsNullOrWhiteSpace(_cmdLineExcludeScope))
                {
                    _cmdLineExcludeScope = default_excludeTokens;
                }
                else
                {
                    if (!_cmdLineExcludeScope.Contains(default_excludeTokens, StringComparison.OrdinalIgnoreCase))
                    {
                        _cmdLineExcludeScope = string.Format("{0};{1}", _cmdLineExcludeScope, default_excludeTokens);
                    }
                }

                return _cmdLineExcludeScope;
            }

            set { _cmdLineExcludeScope = value; }
        }

        /// <summary>
        /// Project type specified at command line corresponds to ProjectType enum
        /// </summary>
        public string ProjectType { get; set; }

        /// <summary>
        /// Project category specified at command line corresponds to ProjectCategory enum
        /// </summary>
        public string ProjectCategory { get; set; }
        
        #endregion

        #region Output Properties
        [Output]
        public SDKMSBTaskItem[] SDK_Projects { get; set; }

        [Output]
        public SDKMSBTaskItem[] Test_Projects { get; set; }

        [Output]
        public SDKMSBTaskItem[] UnSupportedProjects { get; set; }

        [Output]
        public SDKMSBTaskItem[] Test_ToBe_Run { get; set; }

        /// <summary>
        /// No need to output this list, this will be only printed on console and for test purpose
        /// </summary>
        public SDKMSBTaskItem[] PlatformSpecificSkippedProjects { get; set; }


        [Output]
        public string[] SdkPkgReferenceList { get; set; }
        #endregion


        #region internal task props
        public override string NetSdkTaskName => "CategorizeSDKProjectsTask";

        string RepositoryRootDirPath
        {
            get
            {
                if (string.IsNullOrEmpty(_repositoryRootDirPath))
                {
                    //if(!Directory.Exists(_repositoryRootDirPath))
                    //{
                        FileSystemUtility fileSysUtil = new FileSystemUtility();
                        _repositoryRootDirPath = fileSysUtil.TraverseUptoRootWithDirToken(REPO_ROOT_TOKEN_DIR);
                        Check.DirectoryExists(_repositoryRootDirPath);
                    //}
                }

                return _repositoryRootDirPath;
            }

            set
            {
                _repositoryRootDirPath = value;
            }
        }

        string BaseLineSdkTargetFx { get; set; }

        string BaseLineTestTargetFx { get; set; }

        internal SdkProjectType ProjType { get; set; }

        internal SdkProjectCategory ProjCat { get; set; }

        #endregion
        #endregion

        #region Constructor
        public CategorizeSDKProjectsTask()
        {
            MultipleScopes = new List<string>();
        }

        public CategorizeSDKProjectsTask(string rootDirPath) : this(rootDirPath, string.Empty, string.Empty, string.Empty) { }

        public CategorizeSDKProjectsTask(string rootDirPath, string buildScope, string PType, string PCategory)
            : this(rootDirPath, buildScope, null, PType, PCategory)
        {
            RepositoryRootDirPath = rootDirPath;
            BuildScope = buildScope;
            ProjectType = PType;
            ProjectCategory = PCategory;
        }

        public CategorizeSDKProjectsTask(string rootDirPath, string buildScope, List<string> multipleScopes, 
            string PType, string PCategory) : this()
        {
            RepositoryRootDirPath = rootDirPath;            
            BuildScope = buildScope;
            ProjectType = PType;
            ProjectCategory = PCategory;

            if(multipleScopes != null)
            {
                MultipleScopes = multipleScopes;
            }
        }

        #endregion

        #region Public Functions
        public override bool Execute()
        {
            base.Execute();
            ParseCmdLineProperties();

            if(WhatIf)
            {
                WhatIfAction();
            }
            else
            {
                Categorize();
            }

            return TaskLogger.TaskSucceededWithNoErrorsLogged;
        }

        protected override void WhatIfAction()
        {
            TaskLogger.LogInfo(MessageImportance.High, "Projects will be categorized for Scope '{0}'", BuildScope.ToString());
            TaskLogger.LogInfo(MessageImportance.High, "Project Type '{0}' will be used ", ProjType.ToString());
            TaskLogger.LogInfo(MessageImportance.High, "Project Category '{0}' will be used ", ProjCat.ToString());
            TaskLogger.LogInfo(MessageImportance.High, "Tokens to be included '{0}'", CmdLineIncludeScope);
            TaskLogger.LogInfo(MessageImportance.High, "Tokens to be excluded '{0}'", CmdLineExcludeScope);

            TaskLogger.LogInfo(MessageImportance.High, "Repository Root Dir Path '{0}'", RepositoryRootDirPath);

            ProjectSearchUtility psu = new ProjectSearchUtility(RepositoryRootDirPath, MultipleScopes, BuildScope, FullyQualifiedBuildScopeDirPath, CmdLineExcludeScope, CmdLineIncludeScope, ProjType, ProjCat);
            psu.UseLegacyDirs = UseLegacyDirStructure;
            TaskLogger.LogInfo(MessageImportance.High, "Use Legacy Directory Strucuture is set to '{0}'", psu.UseLegacyDirs.ToString());
            TaskLogger.LogInfo(MessageImportance.High, "SDK Root Dir Path '{0}'", psu.SDKRootDir);
            TaskLogger.LogInfo(MessageImportance.High, psu.SearchDirPaths, "Search Dir Path(s)");
        }

        #endregion

        #region private functions
        /// <summary>
        /// 
        /// </summary>
        void Categorize()
        {
            TaskLogger.LogInfo("Categorizing Projects.....");
            List<SdkProjectMetadata> sdkProjList = new List<SdkProjectMetadata>();
            List<SdkProjectMetadata> testProjList = new List<SdkProjectMetadata>();
            List<SdkProjectMetadata> unsupportedProjList = new List<SdkProjectMetadata>();
            List<SdkProjectMetadata> testToBeRunProjList = new List<SdkProjectMetadata>();
            List<SdkProjectMetadata> platformSpecificSkippedProjList = new List<SdkProjectMetadata>();

            List<string> searchedProjects = new List<string>();

            ProjectSearchUtility psu = null;
            Dictionary<string, SdkProjectMetadata> allProj = null;

            psu = new ProjectSearchUtility(RepositoryRootDirPath, MultipleScopes, BuildScope, FullyQualifiedBuildScopeDirPath, CmdLineExcludeScope, CmdLineIncludeScope, ProjType, ProjCat);
            psu.UseLegacyDirs = UseLegacyDirStructure;

            searchedProjects = psu.FindProjects();
            allProj = LoadProjectData(searchedProjects);

            foreach (KeyValuePair<string, SdkProjectMetadata> kv in allProj)
            {
                SdkProjectMetadata pmd = kv.Value;

                switch(pmd.ProjectType)
                {
                    #region SDK
                    case SdkProjectType.Sdk:
                        {
                            if(!pmd.Fx.IsTargetFxMatch)
                            {
                                if (!pmd.Fx.IsApplicableForCurrentPlatform)
                                {
                                    platformSpecificSkippedProjList.Add(pmd);
                                }
                                else
                                {
                                    unsupportedProjList.Add(pmd);
                                }
                            }
                            else if(!pmd.Fx.IsApplicableForCurrentPlatform)
                            {
                                platformSpecificSkippedProjList.Add(pmd);
                            }
                            else
                            {
                                if (!pmd.ExcludeFromBuild)
                                {
                                    sdkProjList.Add(pmd);
                                }
                            }
                            
                            break;
                        }
                    #endregion

                    #region Test
                    case SdkProjectType.Test:
                        {
                            // WE HAVE INTENTIONALLY SKIPPED CHECKING THIS PROPERTY, BASICALLY WE WILL NOT BE VERIFYING BASELINE TARGETFX FOR TEST PROJECTS
                            // IF WE EVER DECIDE TO DO IT, SIMPLY ENABLE THE BELOW CODE

                            //    if (!pmd.Fx.IsTargetFxMatch)
                            //    {
                            //        if (!pmd.Fx.IsApplicableForCurrentPlatform)
                            //        {
                            //            platformSpecificSkippedProjList.Add(pmd);
                            //        }
                            //        else
                            //        {
                            //            unsupportedProjList.Add(pmd);
                            //        }
                            //    }
                            
                            if (!pmd.Fx.IsApplicableForCurrentPlatform)
                            {
                                platformSpecificSkippedProjList.Add(pmd);
                            }
                            else
                            {
                                if (!pmd.ExcludeFromBuild)
                                {
                                    testProjList.Add(pmd);
                                }

                                if (!pmd.ExcludeFromTest)
                                {
                                    testToBeRunProjList.Add(pmd);
                                }
                            }
                            break;
                        }
                        #endregion
                }
            }

            SDK_Projects = sdkProjList.Select<SdkProjectMetadata, SDKMSBTaskItem>((item) => new SDKMSBTaskItem(item)).ToArray<SDKMSBTaskItem>();
            Test_Projects = testProjList.Select<SdkProjectMetadata, SDKMSBTaskItem>((item) => new SDKMSBTaskItem(item)).ToArray<SDKMSBTaskItem>();
            UnSupportedProjects = unsupportedProjList.Select<SdkProjectMetadata, SDKMSBTaskItem>((item) => new SDKMSBTaskItem(item)).ToArray<SDKMSBTaskItem>();
            Test_ToBe_Run = testToBeRunProjList.Select<SdkProjectMetadata, SDKMSBTaskItem>((item) => new SDKMSBTaskItem(item)).ToArray<SDKMSBTaskItem>();
            PlatformSpecificSkippedProjects = platformSpecificSkippedProjList.Select<SdkProjectMetadata, SDKMSBTaskItem>((item) => new SDKMSBTaskItem(item)).ToArray<SDKMSBTaskItem>();
            SdkPkgReferenceList = GetNormalizedPkgRefList();

            TaskLogger.LogInfo("SDK Project(s) found:'{0}'", SDK_Projects.Count().ToString());
            TaskLogger.LogInfo(MessageImportance.Low, SDK_Projects, "File Paths for SDK Projects");

            TaskLogger.LogInfo("Test Project(s) found:'{0}'", Test_Projects.Count().ToString());
            TaskLogger.LogInfo(MessageImportance.Low, Test_Projects, "File Paths for Test Projects");

            TaskLogger.LogInfo("Test Project(s) whose tests will be executed are:'{0}'", Test_ToBe_Run.Count().ToString());
            TaskLogger.LogInfo(MessageImportance.Low, Test_ToBe_Run, "File Paths for Test Projects whose tests will be executed");

            if (UnSupportedProjects.NotNullOrAny<SDKMSBTaskItem>())
            {
                TaskLogger.LogInfo("Project(s) whose target framework is not currently supported:'{0}'", UnSupportedProjects.Count().ToString());
                TaskLogger.LogInfo(MessageImportance.Low, UnSupportedProjects, "File Paths for Unsupported Projects");
            }

            //if (Test_ToBe_Run.NotNullOrAny<SDKMSBTaskItem>())
            //{
            //    TaskLogger.LogInfo("Test Project(s) whose tests will be executed are:'{0}'", Test_ToBe_Run.Count().ToString());
            //    TaskLogger.LogInfo(MessageImportance.Low, Test_ToBe_Run, "File Paths for Test Projects whose tests will be executed");
            //}

            if (PlatformSpecificSkippedProjects.NotNullOrAny<SDKMSBTaskItem>())
            {
                TaskLogger.LogInfo("Test Project(s) that will be skipped from building/executing tests are:'{0}'", PlatformSpecificSkippedProjects.Count().ToString());
                TaskLogger.LogInfo(MessageImportance.Low, PlatformSpecificSkippedProjects, "File Paths for Projects that will be skipped that are platform specific");
            }

            if (SdkPkgReferenceList != null)
            {
                TaskLogger.LogInfo("PackageReferences count:'{0}'", SdkPkgReferenceList.Count().ToString());
                TaskLogger.LogInfo(MessageImportance.Low, SdkPkgReferenceList, "Packages References");
            }
        }

        /// <summary>
        /// Loads project into metadata model
        /// </summary>
        /// <param name="projectPathList"></param>
        /// <returns></returns>
        Dictionary<string, SdkProjectMetadata> LoadProjectData(List<string> projectPathList)
        {
            Dictionary<string, SdkProjectMetadata> d_msbp = new Dictionary<string, SdkProjectMetadata>();
            DateTime startTime = DateTime.Now;
            foreach (string projPath in projectPathList)
            {
                SdkProjectMetadata msbp = new SdkProjectMetadata(projPath, BaseLineSdkTargetFx, BaseLineTestTargetFx);
                if (!d_msbp.ContainsKey(projPath))
                {
                    d_msbp.Add(projPath, msbp);
                }
            }
            DateTime endTime = DateTime.Now;
            TaskLogger.LogInfo(MessageImportance.Low, "Total time for loading '{0}' projects:'{1}'", d_msbp.Count.ToString(), (endTime - startTime).TotalSeconds.ToString());

            return d_msbp;
        }

        void InitBaselineTargetFx()
        {
            string azSdkFilePath = string.Empty;
            string azTestFilePath = string.Empty;
            

            var azSdkRef = Directory.EnumerateFiles(RepositoryRootDirPath, "AzSdk.reference.props", SearchOption.AllDirectories);
            if(azSdkRef.Any<string>())
            {
                azSdkFilePath = azSdkRef.FirstOrDefault<string>();

                MsbuildProject msbSdk = new MsbuildProject(azSdkFilePath);
                BaseLineSdkTargetFx = msbSdk.GetPropertyValue("SdkTargetFx");
            }

            var azTestRef = Directory.EnumerateFiles(RepositoryRootDirPath, "AzSdk.test.reference.props", SearchOption.AllDirectories);
            if (azTestRef.Any<string>())
            {
                azTestFilePath = azTestRef.FirstOrDefault<string>();
                MsbuildProject msbTest = new MsbuildProject(azTestFilePath);
                BaseLineTestTargetFx = msbTest.GetPropertyValue("TargetFrameworks");
            }
        }

        void ParseCmdLineProperties()
        {
            InitBaselineTargetFx();

            if(string.IsNullOrWhiteSpace(ProjectType) && ProjType == SdkProjectType.UnDetermined)
            {
                ProjType = SdkProjectType.Sdk;
            }
            else
            {
                ProjType = ParseProjectKind<SdkProjectType>(ProjectType, SdkProjectType.Sdk);
            }

            if (string.IsNullOrWhiteSpace(ProjectCategory) && ProjCat == SdkProjectCategory.UnDetermined)
            {
                ProjCat = SdkProjectCategory.MgmtPlane;
            }
            else
            {
                ProjCat = ParseProjectKind<SdkProjectCategory>(ProjectCategory, SdkProjectCategory.MgmtPlane);
            }

            if(!string.IsNullOrWhiteSpace(BuildScope))
            {
                MultipleScopes.Add(BuildScope);
            }

            if(!string.IsNullOrWhiteSpace(BuildScopes))
            {
                if(BuildScopes.Contains(";"))
                {
                    var tokens = BuildScopes.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if(tokens.Any<string>())
                    {
                        MultipleScopes = tokens.ToList<string>();
                    }
                }
                else
                { 
                    MultipleScopes.Add(BuildScopes);
                }
            }

            if(!string.IsNullOrWhiteSpace(FullyQualifiedBuildScopeDirPath))
            {
                MultipleScopes.Add(FullyQualifiedBuildScopeDirPath);
            }
        }

        List<string> ParseIncludeExcludeTokens(string tokenString)
        {
            List<string> tokenList = new List<string>();
            char[] sepChars = new char[] { ';' };

            if (!string.IsNullOrEmpty(tokenString))
            {
                string[] tkns = tokenString.Split(sepChars, StringSplitOptions.RemoveEmptyEntries);

                if (tkns != null)
                {
                    tokenList = tkns.ToList<string>();
                }
            }

            return tokenList;
        }

        T ParseProjectKind<T>(string projKind, T defaultValue) where T: struct
        {
            T pk = defaultValue;

            if(!Enum.TryParse<T>(projKind, true, out pk))
            {
                TaskLogger.LogInfo(MessageImportance.Low, "Provided '{0}' value '{1}' is currently not supported", pk.GetType().Name.ToString(), projKind);
                TaskLogger.LogInfo(MessageImportance.Low, "Setting it to default '{0}'", defaultValue.ToString());
            }

            return pk;
        }

        string[] GetNormalizedPkgRefList()
        {
            List<SDKMSBTaskItem> allProjs = new List<SDKMSBTaskItem>();
            string[] pkgRefArray = new string[] { };
            allProjs.AddRange(SDK_Projects);
            allProjs.AddRange(Test_Projects);
            allProjs.AddRange(UnSupportedProjects);

            List<string> refList = new List<string>();
            foreach(SDKMSBTaskItem item in allProjs)
            {
                refList.AddRange(item.PackageRefList);
            }

            var deDuped = refList.Distinct<string>(new ObjectComparer<string>((lhs, rhs) => lhs.Equals(rhs, StringComparison.OrdinalIgnoreCase)));

            if(deDuped.NotNullOrAny<string>())
            {
                pkgRefArray = deDuped.ToArray<string>();
            }

            return pkgRefArray;
        }

        #endregion
    }
}