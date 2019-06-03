// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Common.Base
{
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Logger;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Services;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Tasks;
    using MS.Az.Mgmt.CI.Common.Logger;
    using MS.Az.Mgmt.CI.Common.Utilities;

    public abstract class NetSdkBuildTask : Task
    {
        #region Fields
        NetSdkBuildTaskLogger _taskLogger;
        DebugTask _debugTask;
        //bool _isRunningUnderTest;
        bool _isBuildEngineInitialized;
        KeyVaultService _kvSvc;
        #endregion

        #region base task properties
        #region public properties

        /// <summary>
        /// Key Vault Service
        /// </summary>
        protected KeyVaultService KVSvc
        {
            get
            {
                if (_kvSvc == null)
                {
                    _kvSvc = new KeyVaultService(TaskLogger);
                }

                return _kvSvc;
            }
        }



        /// <summary>
        /// 
        /// </summary>
        public bool DebugMode { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool IsDisposed { get; protected set; }

        /// <summary>
        /// Minimal way to identify task that is being executed
        /// </summary>
        public abstract string NetSdkTaskName { get; }

        #endregion

        #region private/internal properties
        /// <summary>
        /// TODO:
        /// If set to true, it will simply emulate what will happen when used with applicable target
        /// </summary>
        public bool WhatIf { get; set; }

        /// <summary>
        /// Logger
        /// </summary>
        protected virtual NetSdkBuildTaskLogger TaskLogger
        {
            get
            {
                if (_taskLogger == null)
                {
                    _taskLogger = GetLogger();

                    if (_taskLogger == null)
                    {
                        if (IsBuildEngineInitialized)
                        {
                            _taskLogger = new NetSdkBuildTaskLogger(this, MessageImportance.Normal);
                        }
                        else
                        {
                            _taskLogger = new BuildConsoleLogger();
                        }

                        GlobalStateInfo.SetGlobalObject<NetSdkBuildTaskLogger>(_taskLogger);
                    }
                }

                return _taskLogger;
            }
        }

        bool IsBuildEngineInitialized
        {
            get
            {
                if (this?.BuildEngine != null)
                {
                    _isBuildEngineInitialized = true;
                }

                return _isBuildEngineInitialized;
            }
        }

        #endregion
        #endregion

        #region Constrcutor
        public NetSdkBuildTask() : base() { }
        #endregion

        #region Functions
        public override bool Execute()
        {
            if (DebugMode)
            {
                TaskLogger.LogInfo(MessageImportance.Low, "DebugMode Detected");
                if (_debugTask == null)
                {
                    _debugTask = new DebugTask();
                }

                _debugTask.ExecWithInfo(this.NetSdkTaskName);
            }

            return TaskLogger.TaskSucceededWithNoErrorsLogged;
        }

        /// <summary>
        /// Each task will be implementing what should happen on a possibility of executing the task
        /// For e.g. a cleaning task can log/print what artifacts will be cleaned when executed the task
        /// This allows a task to emulate what will happen when task is executed without actually performing the task
        /// </summary>
        protected virtual void WhatIfAction() { }

        #endregion

        #region private functions
        NetSdkBuildTaskLogger GetLogger()
        {
            NetSdkBuildTaskLogger logger = GlobalStateInfo.GetGlobalObject<NetSdkBuildTaskLogger>();
            if (logger == null)
            {
                BuildConsoleLogger consoleLogger = GlobalStateInfo.GetGlobalObject<BuildConsoleLogger>();
                if (consoleLogger != null)
                    logger = consoleLogger;
            }

            return logger;
        }

        #endregion
    }
}