// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.


namespace MS.Az.Mgmt.CI.BuildTasks.UtilityTasks
{
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Logger;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Utilities;
    using System.IO;
    public class InstallPsModulesUtilTask : NetSdkUtilTask
    {
        #region const
        const string USERPROFILE_ENV = "userprofile";
        const string DOCUMENTDIRNAME = @"Documents";
        const string WINPSDIRNAME = @"WindowsPowerShell";
        const string MODULEDIRNAME = @"Modules";        
        #endregion

        #region fields
        string _userModulesDirPath;
        FileSystemUtility _fileSysUtil;
        #endregion

        #region Properties
        public string PsModulesSourceRootDirPath { get; set; }

        string UserModulesDirPath
        {
            get
            {
                if(string.IsNullOrWhiteSpace(_userModulesDirPath))
                {
                    string userProfileDir = System.Environment.GetEnvironmentVariable(USERPROFILE_ENV);
                    if(!string.IsNullOrWhiteSpace(userProfileDir))
                    {
                        _userModulesDirPath = Path.Combine(userProfileDir, DOCUMENTDIRNAME, WINPSDIRNAME, MODULEDIRNAME);
                        if(!Directory.Exists(_userModulesDirPath))
                        {
                            Directory.CreateDirectory(_userModulesDirPath);
                        }
                    }
                    else
                    {
                        _userModulesDirPath = string.Empty;
                    }
                }

                return _userModulesDirPath;
            }
        }

        FileSystemUtility FileSysUtil
        {
            get
            {
                if(_fileSysUtil == null)
                {
                    _fileSysUtil = new FileSystemUtility();
                }

                return _fileSysUtil;
            }
        }
        #endregion

        #region Constructor
        InstallPsModulesUtilTask()
        {
            Init();
        }

        public InstallPsModulesUtilTask(NetSdkBuildTaskLogger taskLogger ) : base(taskLogger)
        {
            Init();
        }

        void Init()
        {
            PsModulesSourceRootDirPath = string.Empty;
        }
        #endregion

        #region Public Functions
        public bool ExecuteUtil()
        {
            if(ParseInput())
            {
                FileSysUtil.DirectoryCopy(PsModulesSourceRootDirPath, UserModulesDirPath, copySubDirs: true);
            }
            
            return UtilLogger.TaskSucceededWithNoErrorsLogged;
        }
        #endregion

        #region private functions
        bool ParseInput()
        {
            bool srcDirExists = true;
            bool destinationDirExists = true;
            if (!Directory.Exists(PsModulesSourceRootDirPath))
            {
                srcDirExists = false;
                UtilLogger.LogWarning("PsModuleSourceDir '{0}' does not exist", PsModulesSourceRootDirPath);
            }

            if(!Directory.Exists(UserModulesDirPath))
            {
                if(DetectEnv.IsRunningUnderNonWindows)
                {
                    UtilLogger.LogWarning("Current not supported on Non-Windows platform");
                }
                else
                {
                    UtilLogger.LogWarning("Unable to detect user modules directory. Usually on windows platform this directory looks like {0}'", UserModulesDirPath);
                }

                destinationDirExists = false;
            }

            return (srcDirExists && destinationDirExists);
        }
        #endregion
    }
}
