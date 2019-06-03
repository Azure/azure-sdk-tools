// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Common.Base
{
    using System;
    public interface INetSdkTask : IDisposable
    {
        /// <summary>
        /// Task name, either class name or a friendly name that identifies your task (Build task or Util task)
        /// </summary>
        string NetSdkTaskName { get; }

        //bool IsDisposed { get; }
    }
}
