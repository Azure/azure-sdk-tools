#define TRACE
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Common.Logger
{
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;

    public class NetSdkBuildTaskLogger : INetSdkTaskLogger
    {
        #region Fields
        bool _isBuildEngineInitialized;
        TaskLoggingHelper _underlyingLogHelper;
        bool _errorsLogged;
        bool _isDebuggerAttached;

        bool _isInitialized;

        object syncObject;
        #endregion

        #region Properties

        public bool IsDebuggerAttached
        {
            get
            {
                if(_isInitialized == false)
                {
                    _isInitialized = true;
                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        _isDebuggerAttached = true;
                    }
                }

                return _isDebuggerAttached;
            }
        }

        public MessageImportance DefaultMessageImportance { get; set; }

        private Task UnderlyingTask { get; set; }

        private bool HasLoggedErrors_Internal { get; set; }

        string TraceLogFilePath { get; }

        #region Interface implemented
        public bool TaskSucceededWithNoErrorsLogged
        {
            get
            {
                if (IsBuildEngineInitialized)
                {
                    _errorsLogged = UnderlyingLogHelper.HasLoggedErrors;
                }
                else
                {
                    _errorsLogged = HasLoggedErrors_Internal;
                }

                return !_errorsLogged;
            }
        }
        #endregion

        internal bool IsBuildEngineInitialized
        {
            get
            {
                if (UnderlyingTask?.BuildEngine != null)
                {
                    _isBuildEngineInitialized = true;
                }

                return _isBuildEngineInitialized;
            }
        }

        internal TaskLoggingHelper UnderlyingLogHelper
        {
            get
            {
                if (_underlyingLogHelper == null)
                {
                    if(UnderlyingTask != null)
                    {
                        _underlyingLogHelper = UnderlyingTask.Log;
                    }
                }

                return _underlyingLogHelper;
            }

            private set
            {
                _underlyingLogHelper = value;
            }
        }

        #endregion

        #region Constructor
        public NetSdkBuildTaskLogger()
        {
            _isInitialized = false;
            DefaultMessageImportance = MessageImportance.Low;
            _isBuildEngineInitialized = false;
            syncObject = new object();

            if (Trace.Listeners.Count == 0)
            {
                Trace.Listeners.Add(new DefaultTraceListener());
            }
        }

        public NetSdkBuildTaskLogger(TaskLoggingHelper msbuildLogHelper) : this()
        {
            UnderlyingLogHelper = msbuildLogHelper;
        }

        public NetSdkBuildTaskLogger(Task taskInstance, MessageImportance msgImportance) : this(taskInstance.Log)
        {
            DefaultMessageImportance = msgImportance;
            UnderlyingTask = taskInstance;
        }

        #endregion

        #region Trace
        internal void TraceInfo(string logInfo)
        {
            TraceInfo(logInfo, "INFO");
        }

        internal void TraceInfo(string logInfo, string traceCategory)
        {
            lock (syncObject)
            {
                foreach (TraceListener tl in Trace.Listeners)
                {
                    tl.WriteLine(logInfo, traceCategory);
                }
            }
        }

        internal void TraceError(string errorInfo, string detailedErrorInfo = "")
        {
            lock (syncObject)
            {
                foreach (TraceListener tl in Trace.Listeners)
                {
                    if (string.IsNullOrEmpty(detailedErrorInfo))
                    {
                        tl.Fail(errorInfo);
                    }
                    else
                    {
                        tl.Fail(errorInfo, detailedErrorInfo);
                    }
                }
            }
        }

        internal void TraceException(Exception ex)
        {
            //TraceError(ex.Message, ex.ToString());
            throw ex;
        }

        #endregion

        #region LogDebugInfo
        void LogDebugInfo(string infoMessageFormat, params string[] infoParam)
        {
            LogInfo(infoMessageFormat, infoParam);
        }

        void LogDebugInfo(string debugInfo)
        {
            LogInfo(debugInfo);
        }
        #endregion

        #region LogInfo
        /// LOGGING GUIDELINES FOR EACH VERBOSITY LEVEL:
        /// 1) Quiet -- only display a summary at the end of build
        /// 2) Minimal -- only display errors, warnings, high importance events and a build summary
        /// 3) Normal -- display errors, warnings, high importance events, some status events, and a build summary
        /// 4) Detailed -- display all errors, warnings, high and normal importance events, all status events, and a build summary
        /// 5) Diagnostic -- display all events, and a build summary
        public virtual void LogInfo(MessageImportance msgImportance, string messageToLog)
        {
            if (IsBuildEngineInitialized)
            {
                UnderlyingLogHelper?.LogMessage(msgImportance, messageToLog);
            }
            else
            {
                //Console.WriteLine("LogDisabled: {0}", messageToLog);
                TraceInfo(messageToLog);
            }
        }

        public void LogInfo(string messageToLog)
        {
            LogInfo(DefaultMessageImportance, messageToLog);
        }

        #region log with format
        public void LogInfo(MessageImportance msgImp, string outputFormat, params string[] infoParam)
        {
            LogInfo(msgImp, string.Format(outputFormat, infoParam));
        }

        public void LogInfo(string infoMessageFormat, params string[] infoParam)
        {
            LogInfo(string.Format(infoMessageFormat, infoParam));
        }

        #endregion

        #region Enumerable Log

        #region Dictionary
        public void LogInfo(Dictionary<string, string> dictionary)
        {
            LogInfo(DefaultMessageImportance, dictionary);
        }

        public void LogInfo(MessageImportance msgImportance, Dictionary<string, string> dictionary)
        {
            string logFormat = "KV:{0}_{1}";
            foreach (KeyValuePair<string, string> kv in dictionary)
            {
                LogInfo(msgImportance, logFormat, kv.Key, kv.Value);
            }
        }

        public void LogInfo(Dictionary<string, string> dictionary, string infoMessageFormat, params string[] infoParam)
        {
            LogInfo(DefaultMessageImportance, dictionary, infoMessageFormat, infoParam);
        }

        public void LogInfo(MessageImportance msgImportance, Dictionary<string, string> dictionary, string infoMessageFormat, params string[] infoParam)
        {
            LogInfo(msgImportance, infoMessageFormat, infoParam);
            LogInfo(msgImportance, dictionary);
        }
        #endregion

        #region IEnumerable<T>
        public void LogInfo<T>(IEnumerable collection, Func<T, string> logDelegate)
        {
            IEnumerator colIterator = collection.GetEnumerator();
            int count = 1;
            while (colIterator.MoveNext())
            {
                T item = (T)colIterator.Current;
                string logValue = logDelegate(item);
                LogInfo("Collection[{0}]: {1}", count.ToString(), logValue);
            }

            colIterator = null;
        }

        public void LogInfo<T>(MessageImportance msgImportance, IEnumerable<T> IEnumInfoToLog, Func<T, string> logDelegate)
        {
            foreach (var info in IEnumInfoToLog)
            {
                LogInfo(msgImportance, logDelegate(info));
            }
        }

        public void LogInfo<T>(MessageImportance msgImportance, IEnumerable<T> IEnumInfoToLog) //where T : class
        {
            LogInfo<T>(msgImportance, IEnumInfoToLog, (i) => i.ToString());
        }

        public void LogInfo<T>(IEnumerable<T> IEnumInfoToLog)
        {
            LogInfo<T>(DefaultMessageImportance, IEnumInfoToLog);
        }

        public void LogInfo<T>(IEnumerable<T> IEnumInfoToLog, Func<T, string> logDelegate)
        {
            LogInfo<T>(DefaultMessageImportance, IEnumInfoToLog, logDelegate);
        }

        public void LogInfo<T>(string logMessagePriorToPrintCollection, IEnumerable<T> IEnumInfoToLog)
        {
            LogInfo(DefaultMessageImportance, logMessagePriorToPrintCollection);
            LogInfo(DefaultMessageImportance, IEnumInfoToLog);
        }

        public void LogInfo<T>(IEnumerable<T> IEnumInfoToLog, string logMessagePriorToPrintCollection, params string[] infoParam)
        {
            LogInfo(DefaultMessageImportance, logMessagePriorToPrintCollection, infoParam);
            LogInfo(DefaultMessageImportance, IEnumInfoToLog);
        }

        public void LogInfo<T>(MessageImportance msgImportance, IEnumerable<T> IEnumInfoToLog, string logMessagePriorToPrintCollection, params string[] infoParam)
        {
            LogInfo(msgImportance, logMessagePriorToPrintCollection, infoParam);
            LogInfo(msgImportance, IEnumInfoToLog);
        }
        #endregion

        #endregion

        #region Tuple Log

        public void LogInfo(IEnumerable<Tuple<string, string, string>> tupleCol)
        {
            string logFormat = "Tuple:{0}_{1}_{2}";
            foreach (Tuple<string, string, string> tup in tupleCol)
            {
                LogInfo(logFormat, tup.Item1, tup.Item2, tup.Item3);
            }
        }

        public void LogInfo(IEnumerable<Tuple<string, string>> tupCol)
        {
            string logFormat = "Tuple:{0}_{1}";
            foreach (Tuple<string, string> tup in tupCol)
            {
                LogInfo(logFormat, tup.Item1, tup.Item2);
            }
        }


        #endregion

        #endregion

        #region Log Error/Exception

        public virtual void LogError(string errorMessage)
        {
            HasLoggedErrors_Internal = true;
            if (IsBuildEngineInitialized)
            {
                UnderlyingLogHelper?.LogError(errorMessage);
            }
            else
            {
                TraceError(errorMessage);
            }
        }

        public void LogError(string errorMessageFormat, params string[] errorParam)
        {
            LogError(string.Format(errorMessageFormat, errorParam));
        }

        public void LogError<T>(IEnumerable<T> errorCollection, Func<T, string> errorDelegate)
        {
            foreach (T errItem in errorCollection)
            {
                LogError(errorDelegate(errItem));
            }
        }

        public virtual void LogException(Exception ex, bool showDetails)
        {
            HasLoggedErrors_Internal = true;
            if (IsBuildEngineInitialized)
            {
                UnderlyingLogHelper?.LogErrorFromException(ex, true, showDetails, null);
            }
            else
            {
                TraceException(ex);
                //throw ex;
            }
        }

        public void LogException<T>(string exceptionMessage) where T : Exception
        {
            T newEx = (T)Activator.CreateInstance(typeof(T), new object[] { exceptionMessage });
            LogException(newEx, showDetails: true);
        }

        public void LogException<T>(string exceptionMessageFormat, params string[] messageParam) where T : Exception
        {
            string exMsg = string.Format(exceptionMessageFormat, messageParam);
            T newEx = (T)Activator.CreateInstance(typeof(T), new object[] { exMsg });
            LogException(newEx, showDetails: true);
        }

        public void LogException(Exception ex)
        {
            LogException(ex, showDetails: true);
        }
        #endregion

        #region Log Warning
        public virtual void LogWarning(string warningMessage)
        {
            if (IsBuildEngineInitialized)
            {
                UnderlyingLogHelper.LogWarning(warningMessage);
            }
            else
            {
                TraceInfo(warningMessage, "WARNING");
            }
        }

        public void LogWarning(string warningMessageFormat, params string[] warnParam)
        {
            LogWarning(string.Format(warningMessageFormat, warnParam));
        }

        public virtual void LogWarning<T>(string logWrnPriorToPrintCollection, IEnumerable<T> IEnumInfoToLog)
        {
            LogWarning(logWrnPriorToPrintCollection);
            LogWarning(IEnumInfoToLog);
        }

        public virtual void LogWarning<T>(IEnumerable<T> IEnumWrnToLog, Func<T, string> logDelegate)
        {
            foreach (T item in IEnumWrnToLog)
            {
                LogWarning(logDelegate(item));
            }
        }

        public virtual void LogWarning<T>(IEnumerable<T> IEnumWrnToLog)
        {
            LogWarning(IEnumWrnToLog, i => i.ToString());
        }

        public virtual void LogWarning<T>(IEnumerable<T> IEnumWrnToLog, string logWrnPriorToPrintCollection, params string[] wrnParam)
        {
            LogWarning(logWrnPriorToPrintCollection, wrnParam);
        }
       
        #endregion
    }
}