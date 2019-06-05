// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Common.Logger
{
    using System;
    using System.Collections.Generic;
    public interface INetSdkTaskLogger
    {
        //NetSdkTaskVerbosityLevel CurrentVerbosityLevel { get; }

        //MessageImportance DefaultMessageImportance { get; }

        //void LogError(NetSdkTaskVerbosityLevel verboseLevel, string errorMessage);
        bool TaskSucceededWithNoErrorsLogged { get; }

        //LogInfo
        void LogInfo(string messageToLog);     //Abstract
        void LogInfo(string infoMessageFormat, params string[] infoParam);
        void LogInfo(Dictionary<string, string> dictionary);
        void LogInfo(IEnumerable<Tuple<string, string, string>> tupleCol);
        void LogInfo(IEnumerable<Tuple<string, string>> tupleCol);
        void LogInfo<T>(IEnumerable<T> IEnumInfoToLog);
        void LogInfo<T>(IEnumerable<T> IEnumInfoToLog, Func<T, string> logDelegate);
        //void LogInfo(MessageImportance msgImportance, IEnumerable<Tuple<string, string, string>> tupCol);
        //void LogInfo(MessageImportance msgImportance, string messageToLog);
        //void LogInfo(MessageImportance msgImportance, string infoFormat, params string[] infoParam);
        //void LogInfo<T>(MessageImportance msgImportance, IEnumerable<T> IEnumInfoToLog) where T : class;
        //void LogInfo<T>(MessageImportance msgImportance, IEnumerable<T> IEnumInfoToLog, Func<T, string> logDelegate);

        //Log Warning
        void LogWarning(string warningMessage);     //Abstract
        void LogWarning(string warningMessageFormat, params string[] warningParam);

        // Log Error
        void LogError(string errorMessage);     //Abstract

        void LogError<T>(IEnumerable<T> errorCollection, Func<T, string> errorDelegate);     //Abstract
        void LogError(string errorMessageFormat, string[] errorParam);

        // Log Exception
        void LogException(Exception ex);     //Abstract
        void LogException(Exception ex, bool showDetails);
    }

    /// <summary>
    /// Verbosity levels
    /// </summary>
    //public enum NetSdkTaskVerbosityLevel
    //{
    //    Quite = 0,
    //    Minimal = 1,
    //    Normal = 2,
    //    Detailed = 3,
    //    Diagnostic = 4
    //}
}