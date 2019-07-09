// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.NetSdk.Build.Utilities
{
    using Microsoft.Build.Framework;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using MS.Az.Mgmt.CI.BuildTasks.Common.ExtensionMethods;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Utilities;
    using MS.Az.Mgmt.CI.BuildTasks.Models;
    using MS.Az.Mgmt.CI.Common.ExtensionMethods;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    public class ProjectSearchUtility : NetSdkUtilTask
    {
        #region CONST
        const string SDK_DIR_NAME = "sdk";
        const string LEGACY_SDK_DIR_NAME = @"sdks";
        const string SRC_DIR_NAME = @"src";
        #endregion

        #region fields
        string _scopeSeedDirPath;
        string _scopeToken;
        string _fqScopeDirPath;

        string _srcRootDir;
        string _sdkRootDir;
        string _legacy_SdkRootDir;
        #endregion

        #region properties

        #region flag properties
        bool topLevelDirsProcessed { get; set; }
        bool mgmt_Proj_Processed { get; set; }
        #endregion

        public string RootDirForSearch { get; set; }

        public List<string> ScopeDirPaths { get; set; }

        public string ScopeToken
        {
            get
            {
                return _scopeToken;
            }
            set
            {
                _scopeToken = value;
                _scopeSeedDirPath = string.Empty;
                topLevelDirsProcessed = false;
                mgmt_Proj_Processed = false;
            }
        }

        public string FQScopeDirPath
        {
            get
            {
                if(!string.IsNullOrWhiteSpace(_fqScopeDirPath))
                {
                    if(!Directory.Exists(_fqScopeDirPath))
                    {
                        _fqScopeDirPath = string.Empty;
                    }
                }
                else
                {
                    _fqScopeDirPath = string.Empty;
                }

                return _fqScopeDirPath;
            }

            set
            {
                _fqScopeDirPath = value;
            }
        }


        public SdkProjectCategory SearchProjectCategory { get; set; }

        public SdkProjectType SearchProjectType { get; set; }

        public bool UseLegacyDirs { get; set; }


        internal List<string> SearchDirPaths { get; set; }

        internal string SRCRootDir
        {
            get
            {
                if(string.IsNullOrWhiteSpace(_srcRootDir))
                {
                    _srcRootDir = Path.Combine(RootDirForSearch, SRC_DIR_NAME);
                    if(!Directory.Exists(_srcRootDir))
                    {
                        _srcRootDir = string.Empty;
                    }
                }

                return _srcRootDir;
            }

            set { _srcRootDir = value; }
        }


        internal string SDKRootDir
        {
            get
            {
                if(string.IsNullOrWhiteSpace(_sdkRootDir))
                {
                    _sdkRootDir = Path.Combine(RootDirForSearch, SDK_DIR_NAME);
                    Check.DirectoryExists(_sdkRootDir);
                }

                return _sdkRootDir;
            }

            set { _sdkRootDir = value; }
        }

        internal string Legacy_SDKRootDir
        {
            get
            {
                if(string.IsNullOrWhiteSpace(_legacy_SdkRootDir))
                {
                    _legacy_SdkRootDir = Path.Combine(SRCRootDir, LEGACY_SDK_DIR_NAME);
                    if(!Directory.Exists(_legacy_SdkRootDir))
                    {
                        _legacy_SdkRootDir = string.Empty;
                    }
                }

                return _legacy_SdkRootDir;
            }

            set { _legacy_SdkRootDir = value; }
        }

        string ScopeSeedDirPath
        {
            get
            {
                if(string.IsNullOrWhiteSpace(_scopeSeedDirPath))
                {
                    if(!string.IsNullOrWhiteSpace(FQScopeDirPath))
                    {
                        _scopeSeedDirPath = GetValidScopePath(FQScopeDirPath);                        
                    }
                    else if(!string.IsNullOrWhiteSpace(ScopeToken))
                    {
                        _scopeSeedDirPath = GetValidScopePath(ScopeToken);
                    }
                    else
                    {
                        _scopeSeedDirPath = SDKRootDir;
                    }
                }
                
                return _scopeSeedDirPath;
            }

            set
            {
                _scopeSeedDirPath = value;
            }
        }

        string cmdLineExclude { get; set; }

        string cmdLineInclude { get; set; }

        #region internal lists

        //List<string> AllSearchProjectList { get; set; }
        List<string> SDK_MgmtDirList { get; set; }
        List<string> SDK_dataPlaneDirList { get; set; }

        List<string> SDK_DirList { get; set; }

        List<string> MGMT_SdkProjList { get; set; }

        List<string> MGMT_TestProjList { get; set; }

        List<string> cmdLineExcludePathList { get; set; }
        List<string> cmdLineIncludePathList { get; set; }
        #endregion

        #endregion

        #region Constructor
        ProjectSearchUtility()
        {
            SDK_MgmtDirList = new List<string>();
            SDK_dataPlaneDirList = new List<string>();
            SDK_DirList = new List<string>();

            MGMT_SdkProjList = new List<string>();
            MGMT_TestProjList = new List<string>();

            cmdLineExcludePathList = new List<string>();
            cmdLineIncludePathList = new List<string>();

            ScopeDirPaths = new List<string>();

            SearchDirPaths = new List<string>();
            SRCRootDir = string.Empty;
            SDKRootDir = string.Empty;
            cmdLineExclude = string.Empty;
            cmdLineInclude = string.Empty;
            ScopeToken = string.Empty;
            FQScopeDirPath = string.Empty;

            //UseLegacyDirs = lega;
        }
                
        public ProjectSearchUtility(string rootDirPath)
            : this(rootDirPath, null, string.Empty, string.Empty, string.Empty, string.Empty, SdkProjectType.Sdk, SdkProjectCategory.MgmtPlane) { }
        
        public ProjectSearchUtility(string rootDirPath, List<string> scopeDirPaths, string scopeToken, string fullQualifiedScopeDirPath,
            string cmdLineExcludePathTokenString, string cmdLineIncludePathTokenString,
            SdkProjectType searchProjType, SdkProjectCategory searchProjCategory) : this()
        {
            // We shuold fail if the root directory is not a valid directory path
            Check.DirectoryExists(rootDirPath);
            RootDirForSearch = rootDirPath;
            cmdLineExclude = cmdLineExcludePathTokenString;
            cmdLineInclude = cmdLineIncludePathTokenString;

            if (scopeDirPaths.NotNullOrAny<string>())
            {
                ScopeDirPaths = scopeDirPaths;
            }

            if(!string.IsNullOrWhiteSpace(scopeToken))
            {
                ScopeToken = scopeToken;
                ScopeDirPaths.Add(scopeToken);
            }

            if(!string.IsNullOrWhiteSpace(fullQualifiedScopeDirPath))
            {
                ScopeDirPaths.Add(fullQualifiedScopeDirPath);
                FQScopeDirPath = fullQualifiedScopeDirPath;
            }

            InitProjectKind(searchProjType, searchProjCategory);
            ScopeDirPaths = DeDupeList(ScopeDirPaths);
            Init();
        }

        
        
        void Init()
        {
            MessageImportance msgImp = MessageImportance.Low;
            string validScope = string.Empty;

            if(SearchDirPaths.NotNullOrAny<string>())
            {
                UtilLogger.LogInfo(msgImp, SearchDirPaths, "Existing SearchDirPaths before clearing");
                SearchDirPaths.Clear();
            }

            // Finally you try to get valid scope from list of scopes provided
            foreach (string sdp in ScopeDirPaths)
            {
                validScope = GetScope(sdp);
                if (string.IsNullOrWhiteSpace(validScope))
                {
                    SearchDirPaths.Add(sdp);
                }
                else
                {
                    SearchDirPaths.Add(validScope);
                }
            }

            // Either we encountered bad scope or no scope was provided, this means we are in filter mode where Include/Exclude tokens were provided, we default to searching the entire repo
            if (SearchDirPaths.Count == 0)
            {   
                if (Directory.Exists(Legacy_SDKRootDir))
                {
                    if(UseLegacyDirs)
                    {
                        SearchDirPaths.Add(Legacy_SDKRootDir);
                    }
                }

                if (Directory.Exists(SDKRootDir))
                {
                    if(!UseLegacyDirs)
                    {
                        SearchDirPaths.Add(SDKRootDir);
                    }
                }

                UtilLogger.LogInfo(msgImp, SearchDirPaths, "Defaulting to searching at these locations");
            }

            if (!string.IsNullOrWhiteSpace(cmdLineExclude))
            {
                cmdLineExcludePathList = ParseIncludeExcludeTokens(cmdLineExclude);
            }

            if (!string.IsNullOrWhiteSpace(cmdLineInclude))
            {
                cmdLineIncludePathList = ParseIncludeExcludeTokens(cmdLineInclude);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cmdLineExcludePathTokenString"></param>
        /// <param name="cmdLineIncludePathTokenString"></param>
        void Init(string cmdLineExcludePathTokenString, string cmdLineIncludePathTokenString)
        {
            // First try to get the Scope Token
            string validScope = GetScope(ScopeToken);

            if(!string.IsNullOrWhiteSpace(validScope))
            {
                SearchDirPaths.Add(validScope);
            }

            // Then get the Fully Qualified scope directory path
            validScope = GetScope(FQScopeDirPath);
            if(!string.IsNullOrWhiteSpace(validScope))
            {
                SearchDirPaths.Add(validScope);
            }

            // Finally you try to get valid scope from list of scopes provided
            foreach(string s in ScopeDirPaths)
            {
                validScope = GetScope(s);
                if(string.IsNullOrWhiteSpace(validScope))
                {
                    SearchDirPaths.Add(s);
                }
            }

            // Either we encountered bad scope or no scope was provided, this means we are in filter mode where Include/Exclude tokens were provided, we default to searching the entire repo
            if(SearchDirPaths.Count == 0)
            {
                
                if (Directory.Exists(Legacy_SDKRootDir))
                {
                    SearchDirPaths.Add(Legacy_SDKRootDir);
                }
            }

            if (!string.IsNullOrWhiteSpace(cmdLineExcludePathTokenString))
            {
                cmdLineExcludePathList = ParseIncludeExcludeTokens(cmdLineExcludePathTokenString);
            }

            if (!string.IsNullOrWhiteSpace(cmdLineIncludePathTokenString))
            {
                cmdLineIncludePathList = ParseIncludeExcludeTokens(cmdLineIncludePathTokenString);
            }
        }

        /// <summary>
        /// Initialize project type and category to search for
        /// </summary>
        /// <param name="ProjType"></param>
        /// <param name="ProjCategory"></param>
        void InitProjectKind(SdkProjectType ProjType, SdkProjectCategory ProjCategory)
        {
            if (ProjType == SdkProjectType.NotSupported)
            {
                UtilLogger.LogError("No Support added for ProjectType:'{0}'", ProjType.ToString());
            }
            else
            {
                SearchProjectType = ProjType;
            }

            if (ProjCategory == SdkProjectCategory.DataPlane || ProjCategory == SdkProjectCategory.UnDetermined)
            {
                UtilLogger.LogError("No Support added for ProjectCategory:'{0}'", ProjCategory.ToString());
            }
            else
            {
                SearchProjectCategory = ProjCategory;
            }
        }

        string GetScope(string scope)
        {
            MessageImportance msgImp = MessageImportance.Low;
            if(!string.IsNullOrWhiteSpace(scope))
            {
                UtilLogger.LogInfo(msgImp, "Trying to find valid scope for token '{0}'", scope);
            }
            
            string validScope = string.Empty;

            // If the provided scope is a fully qualified directory path, we only do one check to see if the path is rooted within the repo root directory path
            // if yes we simply return that as a valid scope
            if (Path.IsPathRooted(scope))
            {
                if (Directory.Exists(scope))
                {
                    if (scope.StartsWith(RootDirForSearch))
                    {
                        validScope = scope;
                    }
                }
            }
            else
            {
                // FIRST ATTEMPT with (root)\src\SDKs dir
                if (!string.IsNullOrWhiteSpace(scope))
                {
                    if(UseLegacyDirs)
                    {
                        validScope = Path.Combine(SRCRootDir, scope);
                    }

                    // SECOND ATTEMPT with root\sdk
                    if (!Directory.Exists(validScope))
                    {
                        // this is a scenario where we detect scope from PR and their we include the full path that is available
                        // in the pr
                        if(scope.StartsWith("sdk", StringComparison.OrdinalIgnoreCase))
                        {
                            validScope = Path.Combine(RootDirForSearch, scope);
                        }
                        else
                        {
                            validScope = Path.Combine(SDKRootDir, scope);
                        }
                    }

                    // At this point we will set it to empty string, because we have tried both legacy sdk directory as well as new sdk directory
                    if(!Directory.Exists(validScope))
                    {
                        validScope = string.Empty;
                    }
                }
            }

            if(!string.IsNullOrWhiteSpace(validScope))
            {
                UtilLogger.LogInfo(msgImp, "ValidScope Found '{0}'", validScope);
            }
            
            return validScope;
        }

        #endregion

        #region public functions

        public List<string> FindProjects(string scope)
        {
            Check.NotEmptyNotNull(scope, "Scope in multipleScope scenario");

            if(Directory.Exists(scope))
            {
                FQScopeDirPath = scope;
            }
            else
            {
                ScopeToken = scope;
            }

            List<string> finalProjList = FindProjects();
            return finalProjList;
        }

        /// <summary>
        /// Find SDK as well as Test projects
        /// </summary>
        /// <returns></returns>
        public List<string> FindProjects()
        {
            List<string> finalProjList = new List<string>();
            InitSearch();
            finalProjList.AddRange(MGMT_SdkProjList);
            finalProjList.AddRange(MGMT_TestProjList);

            finalProjList = DeDupeList(finalProjList);

            return finalProjList;
        }

        public List<string> Find_Mgmt_SDKProjects()
        {
            InitSearch();
            return MGMT_SdkProjList;
        }

        public List<string> Find_Mgmt_TestProjects()
        {
            InitSearch();
            return MGMT_TestProjList;
        }

        public List<string> GetRPDirs()
        {
            List<string> rpDirList = new List<string>();
            foreach (string dirPath in SearchDirPaths)
            {
                var rpDirs = Directory.EnumerateDirectories(dirPath, "*", SearchOption.TopDirectoryOnly);

                if(rpDirs.Any<string>())
                {
                    rpDirList.AddRange(rpDirs);
                }
            }

            return rpDirList;
        }

        /// <summary>
        /// THis functions provides top level scopes that user can provides
        /// </summary>
        /// <returns></returns>
        public List<string> FindTopLevelScopes(bool returnPartialScopePaths = false)
        {
            List<string> topLevelScopeDirs = new List<string>();
            //CategorizeDirectories();
            List<string> dirList = CatDirs();

            var topLevelDirs = from d in dirList select Path.GetDirectoryName(d);

            if (returnPartialScopePaths)
            {
                if(topLevelDirs.NotNullOrAny<string>())
                {
                    var relPaths = from d in topLevelDirs select d.Replace(RootDirForSearch, "");
                    if (relPaths.NotNullOrAny<string>())
                    {
                        topLevelScopeDirs.AddRange(relPaths);
                    }
                }
            }
            else
            {
                if (topLevelDirs.Any<string>())
                {
                    topLevelScopeDirs = topLevelDirs.ToList<string>();
                }
            }

            topLevelScopeDirs = DeDupeList(topLevelScopeDirs);

            UtilLogger.LogInfo(MessageImportance.Low, topLevelScopeDirs, "Top Level Scopes");
            return topLevelScopeDirs; 
        }

        #region Scoped Search
        //public List<string> FindScopedTestProjects()
        //{
        //    InitSearch();


        //}
        #endregion

        /// <summary>
        /// TODO: Provide a way to possible scopes user can provide when user provided token
        /// e.g. if user provides KeyVault, provide all possible scopes available for KeyVault token
        /// </summary>
        /// <param name="scopeToken"></param>
        /// <returns></returns>
        void FindScopes(string scopeToken)
        {
            Check.NotEmptyNotNull(scopeToken);

            string validScopePath = Path.Combine(SDKRootDir, scopeToken);
            if(Directory.Exists(validScopePath))
            {
                UtilLogger.LogInfo(MessageImportance.Low, "Provided Scope is '{0}'", validScopePath);
            }
            else
            {
                Check.DirectoryExists(validScopePath);
            }
        }

        #endregion

        #region private functions
        void InitSearch()
        {
            Init();
            SDK_DirList = CatDirs();
            CatProjs();
        }

        /// <summary>
        /// If scope token is provided, construct a valid fully qualified scope path, if not default to root
        /// If Fully qualified scope path is provided, verify if it's a valid directory path, if not default it to root
        /// </summary>
        /// <param name="scopePath"></param>
        /// <returns></returns>
        string GetValidScopePath(string scopePath)
        {
            string validScopePath = string.Empty;
            UtilLogger.LogInfo(MessageImportance.Low, "Attempting to determine valid scope for token '{0}'", scopePath);

            if (!string.IsNullOrWhiteSpace(scopePath))
            {
                if(scopePath.StartsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    scopePath = scopePath.TrimStart(Path.DirectorySeparatorChar);
                }

                if (scopePath.StartsWith(Path.AltDirectorySeparatorChar.ToString()))
                {
                    scopePath = scopePath.TrimStart(Path.AltDirectorySeparatorChar);
                }

                //First check if it's fully qualified directory path
                if (Directory.Exists(scopePath))
                {
                    //Check if the scope path is part of the root path
                    if (scopePath.Contains(RootDirForSearch, StringComparison.OrdinalIgnoreCase))
                    {
                        validScopePath = scopePath;
                    }
                }
                else
                {
                    //validScopePath = Path.Combine(ScopeRootDir, scopePath);
                    UtilLogger.LogInfo(MessageImportance.Low, "Constructed Scope path is: '{0}'", validScopePath);

                    if (Directory.Exists(validScopePath))
                    {
                        UtilLogger.LogInfo(MessageImportance.Low, "Valid Scope '{0}' is valid", validScopePath);
                    }
                    else
                    {
                        validScopePath = SDKRootDir;
                        UtilLogger.LogInfo(MessageImportance.Low, "Provided Scope Token '{0}' is invalid. Default scope '{1}' will be used", scopePath, validScopePath);
                    }
                }
            }

            if(string.IsNullOrWhiteSpace(validScopePath))
            {
                validScopePath = SDKRootDir;
                UtilLogger.LogInfo(MessageImportance.Low, "Provided Scope token '{0}' is invalid. Default scope '{1}' will be used", scopePath, validScopePath);
            }

            return validScopePath;
        }

        /// <summary>
        /// First we try to find the root of the repo using hte provided dir path
        /// If we cannot find the root, we then use the current Assembly location and try to find the root dir
        /// </summary>
        /// <param name="providedRootDirPath"></param>
        /// <returns></returns>
        string FindRootDirPath(string providedRootDirPath)
        {
            FileSystemUtility fileSysUtil = new FileSystemUtility();

            //This check is especially needed for runnig unit tests.
            //TODO: move tests to an actual repository, then this check will not be needed
            string rootDirPath = fileSysUtil.TraverseUptoRootWithDirToken("src", providedRootDirPath);

            if(string.IsNullOrWhiteSpace(rootDirPath))
            {
                rootDirPath = fileSysUtil.TraverseUptoRootWithDirToken(".git", providedRootDirPath);
            }

            if(string.IsNullOrWhiteSpace(rootDirPath))
            {
                rootDirPath = fileSysUtil.TraverseUptoRootWithDirToken(".git", this.GetType().GetTypeInfo().Assembly.Location);
            }

            Check.DirectoryExists(rootDirPath);
            return rootDirPath;
        }


        void CatProjs()
        {
            IEnumerable<string> mgmtProjects = null;
            IEnumerable<string> testProjects = null;
            IEnumerable<string> commonProjects = null;
            IEnumerable<string> dataProjects = null;

            MGMT_SdkProjList.Clear();
            MGMT_TestProjList.Clear();

            foreach (string sdkDir in SDK_DirList)
            {
                UtilLogger.LogInfo(MessageImportance.Low, "Searching Projects in:'{0}'", sdkDir);
                var allprojs = Directory.EnumerateFiles(sdkDir, "*.csproj", SearchOption.AllDirectories);

                mgmtProjects = FilterCollection(allprojs, SdkProjectCategory.MgmtPlane);
                commonProjects = FilterCollection(allprojs, SdkProjectCategory.SdkCommon_Mgmt);
                testProjects = FilterCollection(allprojs, SdkProjectCategory.Test);
                dataProjects = FilterCollection(allprojs, SdkProjectCategory.DataPlane);

                //NOTE: quick way to get all data plane is to find everything else except the SDK collection
                //var f1 = mgmtProjects?.Union<string>(commonProjects);
                //dataProjects = allprojs.Except<string>(f1, StringComparer.OrdinalIgnoreCase);

                // Combine all non-test projects
                if (mgmtProjects.NotNullOrAny<string>())
                {
                    MGMT_SdkProjList.AddRange(mgmtProjects);
                }

                if (commonProjects.NotNullOrAny<string>())
                {
                    MGMT_SdkProjList.AddRange(commonProjects);
                }

                //if (dataProjects.NotNullOrAny<string>())
                //{
                //    MGMT_SdkProjList.AddRange(dataProjects);
                //}

                if (testProjects.NotNullOrAny<string>())
                {
                    MGMT_TestProjList.AddRange(testProjects);
                }
            }

            MGMT_SdkProjList = ExcludeIncludeProjects(MGMT_SdkProjList);
            MGMT_TestProjList = ExcludeIncludeProjects(MGMT_TestProjList);

            //TODO: can we do better than this check
            //This is performant than trying to adding check during search, we search test/sdk multiple times/ways, hence the number of checks will be more
            if (SearchProjectType == SdkProjectType.Sdk)
            {
                //If user selected to search for SDK projects, we clear all searched test projects
                MGMT_TestProjList.Clear();
            }

            if (SearchProjectType == SdkProjectType.Test)
            {
                //If user selected to search for test projects, we clear all searched sdk projects
                MGMT_SdkProjList.Clear();
            }
        }

        List<string> CatDirs()
        {
            List<string> masterList = new List<string>();
            //List<string> dpD = new List<string>();
            List<string> mpD = new List<string>();
            List<string> mpT = new List<string>();

            foreach (string sdp in SearchDirPaths)
            {
                IEnumerable<string> svcTopDirs = null;

                try
                {
                    svcTopDirs = Directory.EnumerateDirectories(sdp, "*", SearchOption.TopDirectoryOnly);

                    foreach (string serviceDir in svcTopDirs)
                    {
                        // If ever data plane needs to be part of this, enable the below
                        //dpD = FindDataPlaneDirs(serviceDir);

                        mpD = FindManagementDirs(serviceDir);
                        mpT = FindManagementTestDirs(serviceDir);

                        if ((!mpT.Any<string>()) && (!mpD.Any<string>()) /*&& (!dpD.Any<string>())*/ )
                        {
                            masterList.Add(sdp);
                        }

                        if (mpD.Any<string>())
                        {
                            masterList.AddRange(mpD);
                        }

                        if (mpT.Any<string>())
                        {
                            masterList.AddRange(mpT);
                        }

                        //if(dpD.Any<string>())
                        //{
                        //    SDK_DirList.AddRange(dpD);
                        //    //SDK_dataPlaneDirList.AddRange(dpD);
                        //}
                    }
                }
                catch(Exception ex)
                {
                    // you end up in this situation where users make mistakes in providing scope
                    // we simply do passthru
                    UtilLogger.LogInfo(ex.Message);
                    UtilLogger.LogWarning("'{0}' is not a valid scope", sdp);
                }
            }

            UtilLogger.LogInfo(MessageImportance.Low, "Count of SDK_dataPlaneDirList before dedupe '{0}'", masterList.Count.ToString());
            masterList = DeDupeList(masterList);
            UtilLogger.LogInfo(MessageImportance.Low, "Count of SDK_dataPlaneDirList before dedupe '{0}'", masterList.Count.ToString());

            return masterList;
        }

        List<string> FindDataPlaneDirs(string searchDirPath)
        {
            List<string> dpD = new List<string>();
            //#region legacy data-plane dirs
            ////Legacy data-plane dirs
            //var dpDirs = Directory.EnumerateDirectories(searchDirPath, "dataplane", SearchOption.TopDirectoryOnly);
            //if (dpDirs.Any<string>())
            //{
            //    dpD.AddRange(dpDirs);
            //}

            //var dppDirs = Directory.EnumerateDirectories(searchDirPath, "data-plane", SearchOption.TopDirectoryOnly);
            //if (dppDirs.Any<string>())
            //{
            //    dpD.AddRange(dppDirs);
            //}
            //#endregion

            // There is a case that in new dir structure, few dirs are starting with Azure.* name, so will have to add this if DP decides to leverage this
            var newDPDirs = Directory.EnumerateDirectories(searchDirPath, "Microsoft.Azure.*", SearchOption.TopDirectoryOnly);
            if(newDPDirs.Any<string>())
            {
                dpD.AddRange(newDPDirs);
            }

            return dpD;
        }

        List<string> FindManagementDirs(string searchDirPath)
        {
            List<string> mpD = new List<string>();
            var managementDir = Directory.EnumerateDirectories(searchDirPath, "management", SearchOption.TopDirectoryOnly);
            if (managementDir.Any<string>())
            {
                mpD.AddRange(managementDir);
            }

            var mgmtSearchedDirs = Directory.EnumerateDirectories(searchDirPath, "*Management*", SearchOption.TopDirectoryOnly);
            if (mgmtSearchedDirs.Any<string>())
            {
                mpD.AddRange(mgmtSearchedDirs);
            }

            return mpD;
        }

        List<string> FindManagementTestDirs(string searchDirPath)
        {
            List<string> mpD = new List<string>();
            var managementDir = Directory.EnumerateDirectories(searchDirPath, "*.Test*", SearchOption.TopDirectoryOnly);
            if (managementDir.Any<string>())
            {
                mpD.AddRange(managementDir);
            }

            return mpD;
        }

        /// <summary>
        /// This function will first get all projects that has *.Management.* in the directory names
        /// Then it will get all directories within a service directory and will eliminate *.Management.*
        /// That will leave all the directories with data plane directories
        /// 
        /// At the end the of this function it will be create 3 tests
        /// </summary>
        void CategorizeDirectories()
        {
            List<string> managementSdkDirs = new List<string>();
            List<string> dataPlaneSdkDirs = new List<string>();

            if(UseLegacyDirs)
            {
                CategorizeDirectories_Legacy();
            }
            else
            {
                var svcTopDirs = Directory.EnumerateDirectories(ScopeSeedDirPath, "*", SearchOption.TopDirectoryOnly);

                #region top level directories
                foreach (string serviceDir in svcTopDirs)
                {
                    var alldirs = Directory.EnumerateDirectories(serviceDir, "*", SearchOption.TopDirectoryOnly);

                    //Find mgmt directories
                    var mgmtSearchedDirs = Directory.EnumerateDirectories(serviceDir, "*Management*", SearchOption.AllDirectories);
                    if (mgmtSearchedDirs.Any<string>())
                    {
                        SDK_MgmtDirList.AddRange(mgmtSearchedDirs);
                    }

                    var restOfDirs = Directory.EnumerateDirectories(serviceDir, "Microsoft.Azure.*", SearchOption.TopDirectoryOnly);

                    //Find dataPlane directores
                    var nonMgmtDirs = restOfDirs.Except<string>(mgmtSearchedDirs, new ObjectComparer<string>((lhs, rhs) => lhs.Equals(rhs, StringComparison.OrdinalIgnoreCase)));
                    if (nonMgmtDirs.Any<string>())
                    {
                        SDK_dataPlaneDirList.AddRange(nonMgmtDirs);
                    }
                }
                #endregion
            }

            //Flag that top level directories are processed
            topLevelDirsProcessed = true;
        }

        void CategorizeDirectories_Legacy()
        {
            List<string> dpD = new List<string>();

            var svcTopDirs = Directory.EnumerateDirectories(ScopeSeedDirPath, "*", SearchOption.TopDirectoryOnly);
            UtilLogger.LogInfo(MessageImportance.Low, "ScopeSeedDirPath is:'{0}'", ScopeSeedDirPath);

            if (ScopeSeedDirPath.Equals(SDKRootDir))
            {   
                foreach (string serviceDir in svcTopDirs)
                {
                    dpD.Clear();

                    var alldirs = Directory.EnumerateDirectories(serviceDir, "*", SearchOption.TopDirectoryOnly);

                    var dpDirs = Directory.EnumerateDirectories(serviceDir, "dataplane", SearchOption.TopDirectoryOnly);
                    if(dpDirs.Any<string>())
                    {
                        dpD.AddRange(dpDirs);
                    }

                    var dppDirs = Directory.EnumerateDirectories(serviceDir, "data-plane", SearchOption.TopDirectoryOnly);
                    if (dppDirs.Any<string>())
                    {
                        dpD.AddRange(dppDirs);
                    }

                    var mDirs = alldirs.Except<string>(dpD, new ObjectComparer<string>((lhs, rhs) => lhs.Equals(rhs, StringComparison.OrdinalIgnoreCase)));

                    if(mDirs.Any<string>())
                    {
                        SDK_MgmtDirList.AddRange(mDirs);
                    }

                    var managementDir = Directory.EnumerateDirectories(serviceDir, "management", SearchOption.TopDirectoryOnly);
                    if(managementDir.Any<string>())
                    {
                        SDK_MgmtDirList.AddRange(managementDir);
                    }

                    if(dpD.Any<string>())
                    {
                        SDK_dataPlaneDirList.AddRange(dpD);
                    }
                }
            }
            else
            {
                SDK_MgmtDirList.Add(ScopeSeedDirPath);
            }

            UtilLogger.LogInfo(MessageImportance.Low, "Count of SDK_MgmtDirList before dedupe '{0}'", SDK_MgmtDirList.Count.ToString());
            SDK_MgmtDirList = DeDupeList(SDK_MgmtDirList);
            UtilLogger.LogInfo(MessageImportance.Low, "Count of SDK_MgmtDirList before dedupe '{0}'", SDK_MgmtDirList.Count.ToString());

            UtilLogger.LogInfo(MessageImportance.Low, "Count of SDK_dataPlaneDirList before dedupe '{0}'", SDK_dataPlaneDirList.Count.ToString());
            SDK_dataPlaneDirList = DeDupeList(SDK_dataPlaneDirList);
            UtilLogger.LogInfo(MessageImportance.Low, "Count of SDK_dataPlaneDirList before dedupe '{0}'", SDK_dataPlaneDirList.Count.ToString());
        }

        void CategorizeProjects()
        {
            IEnumerable<string> mgmtProjects = null;
            IEnumerable<string> testProjects = null;
            IEnumerable<string> commonProjects = null;
            IEnumerable<string> dataProjects = null;

            foreach (string mgmtDir in SDK_MgmtDirList)
            {
                UtilLogger.LogInfo(MessageImportance.Low, "Searching Projects in:'{0}'", mgmtDir);
                var allprojs = Directory.EnumerateFiles(mgmtDir, "*.csproj", SearchOption.AllDirectories);

                mgmtProjects = FilterCollection(allprojs, SdkProjectCategory.MgmtPlane);
                commonProjects = FilterCollection(allprojs, SdkProjectCategory.SdkCommon_Mgmt);
                testProjects = FilterCollection(allprojs, SdkProjectCategory.Test);
                dataProjects = FilterCollection(allprojs, SdkProjectCategory.DataPlane);

                //NOTE: quick way to get all data plane is to find everything else except the SDK collection
                //var f1 = mgmtProjects?.Union<string>(commonProjects);
                //dataProjects = allprojs.Except<string>(f1, StringComparer.OrdinalIgnoreCase);

                // Combine all non-test projects
                if (mgmtProjects.NotNullOrAny<string>())
                {
                    MGMT_SdkProjList.AddRange(mgmtProjects);
                }

                if (commonProjects.NotNullOrAny<string>())
                {
                    MGMT_SdkProjList.AddRange(commonProjects);
                }

                if(dataProjects.NotNullOrAny<string>())
                {
                    MGMT_SdkProjList.AddRange(dataProjects);
                }

                if(testProjects.NotNullOrAny<string>())
                {
                    MGMT_TestProjList.AddRange(testProjects);
                }
            }

            MGMT_SdkProjList = ExcludeIncludeProjects(MGMT_SdkProjList);
            MGMT_TestProjList = ExcludeIncludeProjects(MGMT_TestProjList);

            //TODO: can we do better than this check
            //This is performant than trying to adding check during search, we search test/sdk multiple times/ways, hence the number of checks will be more
            if (SearchProjectType == SdkProjectType.Sdk)
            {
                //If user selected to search for SDK projects, we clear all searched test projects
                MGMT_TestProjList.Clear();
            }

            if (SearchProjectType == SdkProjectType.Test)
            {
                //If user selected to search for test projects, we clear all searched sdk projects
                MGMT_SdkProjList.Clear();
            }

            //Flag that management projects are searched and processed
            mgmt_Proj_Processed = true;
        }

        /// <summary>
        /// Iterates all directories and finds
        /// Management Projects
        /// Tests projects in the same directory
        /// </summary>
        void CategorizeMgmtProjects()
        {
            foreach (string mgmtDir in SDK_MgmtDirList)
            {
                var allprojs = Directory.EnumerateFiles(mgmtDir, "*.csproj", SearchOption.AllDirectories);
             
                //first non test projects
                var msdkProj = Directory.EnumerateFiles(mgmtDir, "*Management.*.csproj", SearchOption.AllDirectories);

                // test projects
                var testProj = Directory.EnumerateFiles(mgmtDir, "*Test*.csproj", SearchOption.AllDirectories);
                if(testProj.Any<string>())
                {
                    MGMT_TestProjList.AddRange(testProj);
                }

                //Searching any test projects within sdk projects
                var testInSdkProj = msdkProj.Where<string>((item) => item.Contains("Test", StringComparison.OrdinalIgnoreCase));
                if(testInSdkProj.Any<string>())
                {
                    MGMT_TestProjList.AddRange(testInSdkProj);

                    var foo = msdkProj.Except(testInSdkProj, new ObjectComparer<string>((lhs, rhs) => lhs.Equals(rhs, StringComparison.OrdinalIgnoreCase)));

                    if(foo.Any<string>())
                    {

                    }
                }

                //Search test projects in the same directory, if the project name does not contain "test", they are non-test projects
                if (msdkProj.Any<string>())
                {
                    foreach(string p in msdkProj)
                    {
                        if(p.Contains("Test", StringComparison.OrdinalIgnoreCase))
                        {
                            MGMT_TestProjList.Add(p);
                        }
                        else
                        {
                            MGMT_SdkProjList.Add(p);
                        }
                    }
                }

                //We do a second search by filtering out non-test projects from all projects, that will be left with test projects
                if (allprojs.Any<string>())
                {
                    var testProjs = allprojs.Except<string>(msdkProj, new ObjectComparer<string>((lhs, rhs) => lhs.Equals(rhs, StringComparison.OrdinalIgnoreCase)));

                    if (testProjs.Any<string>())
                    {
                        MGMT_TestProjList.AddRange(testProjs);
                    }
                }
            }

            MGMT_SdkProjList = ExcludeIncludeProjects(MGMT_SdkProjList);
            MGMT_TestProjList = ExcludeIncludeProjects(MGMT_TestProjList);

            //TODO: Can we do better than this check
            //This is performant than trying to adding check during search, we search test/sdk multiple times/ways, hence the number of checks will be more
            if (SearchProjectType == SdkProjectType.Sdk)
            {   
                //If user selected to search for SDK projects, we clear all searched test projects
                MGMT_TestProjList.Clear();
            }

            if(SearchProjectType == SdkProjectType.Test)
            {
                //If user selected to search for test projects, we clear all searched sdk projects
                MGMT_SdkProjList.Clear();
            }

            //Flag that management projects are searched and processed
            mgmt_Proj_Processed = true;
        }

        List<string> FilterCollection(IEnumerable<string> collection, SdkProjectCategory projectCategory)
        {
            List<string> finalList = new List<string>();
            IEnumerable<string> filteredCollection = null;
            switch (projectCategory)
            {
                case SdkProjectCategory.MgmtPlane:
                    {
                        filteredCollection = FilterCollection(collection, "management", "sdkcommon");
                        filteredCollection = FilterCollection(collection, "management", "mgmtcommon");
                        filteredCollection = FilterOutTestProjects(filteredCollection);
                        break;
                    }

                case SdkProjectCategory.SdkCommon_Mgmt:
                    {
                        filteredCollection = FilterCollection(collection, "sdkcommon", "management");
                        filteredCollection = FilterCollection(collection, "mgmtcommon", "management");
                        filteredCollection = FilterOutTestProjects(filteredCollection);
                        break;
                    }
                //TODO: Consolidate Test ProjectType/Category into 1 enum (this is simply being used to categorize, ProjectType should be used at command line)
                case SdkProjectCategory.Test:
                    {
                        filteredCollection = FilterCollectionForTestProjects(collection);
                        break;
                    }

                case SdkProjectCategory.DataPlane:
                    {
                        var f1 = collection
                            .Where<string>((item) => !item.Contains("management", StringComparison.OrdinalIgnoreCase))
                            .Where<string>((item) => !item.Contains("mgmtcommon", StringComparison.OrdinalIgnoreCase))
                            .Where<string>((item) => !item.Contains("sdkcommon", StringComparison.OrdinalIgnoreCase));

                        //if (f1.NotNullOrAny<string>())
                        //{
                        //    f1 = f1.Where<string>((item) => item.Contains("sdkcommon", StringComparison.OrdinalIgnoreCase));
                        //}
                        //else
                        //{
                        //    f1 = collection.Where<string>((item) => !item.Contains("sdkcommon", StringComparison.OrdinalIgnoreCase));
                        //}

                        if(filteredCollection.NotNullOrAny<string>())
                        {
                            f1 = FilterOutTestProjects(f1);
                        }

                        filteredCollection = f1;
                        break;
                    }

                default:
                    {
                        UtilLogger.LogException<ApplicationException>(string.Format("'{0}' is not currently supported category", projectCategory.ToString()));
                        break;
                    }
            }

            if (filteredCollection.NotNullOrAny<string>())
            {
                finalList.AddRange(filteredCollection);
            }

            return finalList;
        }

        /// <summary>
        /// Find test projects
        /// Project files (.csproj) that ends with test.csproj or tests.csproj (case insensitive search)
        /// </summary>
        /// <param name="collection"></param>
        /// <returns></returns>
        List<string> FilterCollectionForTestProjects(IEnumerable<string> collection)
        {
            List<string> finalList = new List<string>();
            foreach (string filePath in collection)
            {
                if (
                    (filePath.EndsWith("test.csproj", StringComparison.OrdinalIgnoreCase))
                    ||
                    (filePath.EndsWith("tests.csproj", StringComparison.OrdinalIgnoreCase))
                    )
                {
                    if(filePath.Contains("mgmtcommon"))
                    {
                        finalList.Add(filePath);
                    }
                    else if(filePath.Contains("management"))
                    {
                        finalList.Add(filePath);
                    }
                }
            }



            //var mgmtTestProj = finalList.Where<string>((item) => item.Contains("management", StringComparison.OrdinalIgnoreCase));

            //if(mgmtTestProj.NotNullOrAny<string>())
            //{
            //    finalList = mgmtTestProj.ToList<string>();
            //}
            //else
            //{
            //    finalList.Clear();
            //}

            return finalList;
        }

        /// <summary>
        /// Filter out test projects
        /// </summary>
        /// <param name="collection"></param>
        /// <returns></returns>
        List<string> FilterOutTestProjects(IEnumerable<string> collection)
        {
            List<string> finalList = new List<string>();

            foreach (string filePath in collection)
            {
                if (
                    (!filePath.EndsWith("test.csproj", StringComparison.OrdinalIgnoreCase))
                    &&
                    (!filePath.EndsWith("tests.csproj", StringComparison.OrdinalIgnoreCase))
                    )
                {
                    finalList.Add(filePath);
                }
            }

            return finalList;
        }

        List<string> FilterCollection(IEnumerable<string> collection, string containsToken, string doesNotContainToken = "", bool returnOriginalCollectionIfNoMatchesFound = false)
        {
            List<string> finalList = new List<string>();
            IEnumerable<string> containsTokenFiltered = null;
            IEnumerable<string> doesNotContainsTokenFiltered = null;

            if (returnOriginalCollectionIfNoMatchesFound)
            {
                finalList.AddRange(collection);
            }

            // Filter on the token you want
            if (collection.NotNullOrAny<string>())
            {
                containsTokenFiltered = collection.Where<string>((item) => item.Contains(containsToken, StringComparison.OrdinalIgnoreCase));
            }

            // Add it to that final list
            if (containsTokenFiltered.NotNullOrAny<string>())
            {
                finalList.Clear();
                finalList.AddRange(containsTokenFiltered);

                // now filter out what you don't want
                if (!string.IsNullOrWhiteSpace(doesNotContainToken))
                {
                    doesNotContainsTokenFiltered = containsTokenFiltered.Where<string>((item) => !item.Contains(doesNotContainToken, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (doesNotContainsTokenFiltered.NotNullOrAny<string>())
            {
                finalList.Clear();
                finalList.AddRange(doesNotContainsTokenFiltered);
            }

            return finalList;
        }
        
        /// <summary>
        /// This function will filter on cmdline provided include/exclude path list
        /// Exclude will be given priority over include
        /// First we will exclude all the projects that are not needed
        /// Then we will include only the projects that are listed in the include path token list
        /// </summary>
        /// <param name="masterList">list of project path on which include/exclue filter will be applied</param>
        /// <returns>List of project path after applying filters</returns>
        List<string> ExcludeIncludeProjects(List<string> masterList)
        {
            UtilLogger.LogInfo(MessageImportance.Low, "Begin Filtering of projects.....");

            // Makes it easy to debug and compare at runtime
            List<string> finalList = new List<string>();
            List<string> includeList = new List<string>();

            if (masterList.Any<string>())
            {
                includeList.AddRange(masterList);

                UtilLogger.LogInfo(MessageImportance.Low, "Final List count before filtering on include path tokens:'{0}'", finalList.Count.ToString());
                if (cmdLineIncludePathList.Any<string>())
                {
                    UtilLogger.LogInfo(MessageImportance.Low, cmdLineIncludePathList, "User Include Path token list count '{0}'", cmdLineIncludePathList.Count.ToString());
                    var includeFilteredList = masterList.Intersect<string>(cmdLineIncludePathList, new ObjectComparer<string>((rhs, lhs) => lhs.Contains(rhs, StringComparison.OrdinalIgnoreCase)));

                    // We give priority to ONLY include projects that match include list, in case we do not have anything that matches provided include list, we discard all the projects that were found (because they do not match include list)
                    // The purpose of include list is to ONLY include what is specified
                    if (includeFilteredList.Any<string>())
                    {
                        includeList.Clear();
                        includeList.AddRange(includeFilteredList);
                        UtilLogger.LogInfo(MessageImportance.Low, "After filtering on 'UserIncludePathList', Final list count is:'{0}'", finalList.Count.ToString());
                    }
                    else
                    {
                        includeList.Clear();
                    }
                }
                else
                {
                    UtilLogger.LogInfo(MessageImportance.Low, "User Include Path Token list empty");
                    finalList.Clear();
                    finalList.AddRange(includeList);
                }

                if (cmdLineExcludePathList.Any<string>())
                {
                    UtilLogger.LogInfo(MessageImportance.Low, cmdLineExcludePathList, "User Exclude path token list count:'{0}'", cmdLineExcludePathList.Count.ToString());
                    if (includeList.Any<string>())
                    {
                        UtilLogger.LogInfo(MessageImportance.Low, "Final List count before filtering on exclusion path tokens:'{0}'", finalList.Count.ToString());

                        var finalMgmtList = includeList.Except<string>(cmdLineExcludePathList, new ObjectComparer<string>((rhs, lhs) => lhs.Contains(rhs, StringComparison.OrdinalIgnoreCase)));

                        // If we get anything in finalMgmtList that means, the filter was a success
                        if (finalMgmtList.Any<string>())
                        {
                            finalList.Clear();
                            finalList.AddRange(finalMgmtList);
                            UtilLogger.LogInfo(MessageImportance.Low, "After filtering on 'UserExcludePathList', Final list count is:'{0}'", finalList.Count.ToString());
                        }
                        else
                        {
                            // This means due to Except filter, we were able to filter out remaining list, so we need to clear the final list.
                            // Meaning things matched from cmdLineExcludePathList
                            finalList.Clear();
                        }
                    }
                }
                else
                {
                    UtilLogger.LogInfo(MessageImportance.Low, "User Exclude Path Token list empty");
                }
            }
            else
            {
                UtilLogger.LogWarning("Master list empty. Exiting filter");
            }

            UtilLogger.LogInfo(MessageImportance.Low, "End Filtering of projects. Final list count:'{0}'", finalList.Count.ToString());

            if(finalList.Any<string>())
            {
                finalList = DeDupeList(finalList);
            }

            return finalList;
        }

        List<string> DeDupeList(List<string> masterList)
        {
            List<string> ddList = new List<string>();
            UtilLogger.LogInfo(MessageImportance.Low, "List Count prior to dedupe '{0}'", masterList.Count.ToString());
            if (masterList.NotNullOrAny<string>())
            {
                var dd = masterList.Distinct<string>(new ObjectComparer<string>((lhs, rhs) => lhs.Equals(rhs, StringComparison.OrdinalIgnoreCase)));
                if (dd.NotNullOrAny<string>())
                {
                    ddList = dd.ToList<string>();
                }
            }

            UtilLogger.LogInfo(MessageImportance.Low, "List Count after to dedupe '{0}'", ddList.Count.ToString());
            return ddList;
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

            tokenList = DeDupeList(tokenList);
            return tokenList;
        }
        #endregion
    }
}