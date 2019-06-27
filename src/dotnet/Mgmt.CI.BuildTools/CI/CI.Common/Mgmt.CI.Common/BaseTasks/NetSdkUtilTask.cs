// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Common.Base
{
    using Microsoft.Build.Utilities;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Logger;
    using MS.Az.Mgmt.CI.Common.Logger;
    using MS.Az.Mgmt.CI.Common.Utilities;
    using System;

    public class NetSdkUtilTask : INetSdkTask, IDisposable
    {
        #region fields
        static NetSdkBuildTaskLogger _utilLogger;
        #endregion

        #region Properties
        TaskLoggingHelper LogHelper { get; set; }

        protected NetSdkBuildTaskLogger UtilLogger
        {
            get
            {
                if (_utilLogger == null)
                {
                    _utilLogger = GetLogger();

                    if (_utilLogger == null)
                    {
                        _utilLogger = new BuildConsoleLogger();
                        GlobalStateInfo.SetGlobalObject<NetSdkBuildTaskLogger>(_utilLogger);
                    }
                }

                return _utilLogger;
            }

            private set
            {
                _utilLogger = value;
            }
        }
        #endregion

        #region Constructor
        public NetSdkUtilTask()
        {

        }
        protected NetSdkUtilTask(NetSdkBuildTaskLogger utilLog) : this()
        {
            UtilLogger = utilLog;
        }
        
        NetSdkUtilTask(Task rootTask) : this()
        {
            LogHelper = rootTask.Log;
        }

        #endregion

        #region implemented interface
        public virtual string NetSdkTaskName => "NetSdkUtilTask";

        protected virtual bool IsDisposed { get; set; }

        public virtual void Dispose()
        {
            IsDisposed = true;
        }
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
