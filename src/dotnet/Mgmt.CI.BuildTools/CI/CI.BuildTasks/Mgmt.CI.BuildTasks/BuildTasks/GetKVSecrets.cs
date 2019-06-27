// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.BuildTasks
{
    using Microsoft.Build.Framework;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;

    /// <summary>
    /// Basic KV Task
    /// </summary>
    public class GetKVSecrets : NetSdkBuildTask
    {
        public override string NetSdkTaskName => "GetKVSecrets";

        public string SecretIdentifier { get; set; }

        [Output]
        public string RetrievedKVSecret { get; set; }
        public override bool Execute()
        {
            base.Execute();
            if (WhatIf)
            {
                WhatIfAction();
            }
            else
            {
                TaskLogger.LogInfo("Retrieving from keyvault for identifier '{0}'", SecretIdentifier);
                RetrievedKVSecret = this.KVSvc.GetSecret(SecretIdentifier);

                if(string.IsNullOrEmpty(RetrievedKVSecret))
                {
                    TaskLogger.LogInfo("Retrieved empty value from keyvault");
                }
                else
                {
                    TaskLogger.LogInfo("Retrieved non-empty value from keyvault. Avoiding logging.");
                }
            }

            return TaskLogger.TaskSucceededWithNoErrorsLogged;
        }
    }
}
