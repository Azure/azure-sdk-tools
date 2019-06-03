// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Common.Tasks
{
    using Microsoft.Build.Utilities;
    using System;
    using System.Diagnostics;
    //using System.ServiceProcess;
    using ThreadTask = System.Threading.Tasks;

    /// <summary>
    /// Utility task to help debug
    /// </summary>
    public class DebugTask : Task
    {
        /// <summary>
        /// Default timeout
        /// </summary>
        const int DEFAULT_TASK_TIMEOUT = 30000;

        /// <summary>
        /// Task Timeout
        /// </summary>
        public int Timeoutmiliseconds { get; set; }

        //protected override INetSdkTask TaskInstance { get => this; }

        public override bool Execute()
        {
            if (Timeoutmiliseconds == 0) Timeoutmiliseconds = DEFAULT_TASK_TIMEOUT;

            if (!IsCIEnvironmentAvailable)
            {
                if (!Debugger.IsAttached)
                {
                    ThreadTask.Task waitingTask = ThreadTask.Task.Run(() =>
                    {
                        Console.WriteLine("Press any key to continue or it will continue in {0} seconds", (Timeoutmiliseconds / 1000));
                        GetProcessInfo();
                        Console.ReadLine();
                    });

                    waitingTask.Wait(TimeSpan.FromMilliseconds(Timeoutmiliseconds));
                }
            }
            return true;
        }

        public virtual bool ExecWithInfo(string info)
        {
            Console.WriteLine("Currently executing info: {0}", info);
            return Execute();
        }

        private void GetProcessInfo()
        {
            Process proc = Process.GetCurrentProcess();
            Console.WriteLine("{0}: {1}", proc.ProcessName, proc.Id.ToString());
        }

        /// <summary>
        /// Detect if running under travis or jenkins
        /// https://docs.travis-ci.com/user/environment-variables/#default-environment-variables
        /// </summary>
        private bool IsCIEnvironmentAvailable
        {
            get
            {
                bool isRunningUnderTravis = false;
                bool isRunningUnderJenkins = false;

                //Travis Env variables
                string isTravisCIString = System.Environment.GetEnvironmentVariable("CI");
                string travisHome = System.Environment.GetEnvironmentVariable("HOME");

                string jenkinsHome = System.Environment.GetEnvironmentVariable("JENKINS_HOME");
                string jenkinsUrl = System.Environment.GetEnvironmentVariable("JENKINS_URL");

                bool isTravisCI = Convert.ToBoolean(isTravisCIString);
                if(isTravisCI == true)
                {
                    if(travisHome.Contains("travis"))
                    {
                        isRunningUnderTravis = true;
                    }
                }

                //Jenkins Env variables
                if(!string.IsNullOrWhiteSpace(jenkinsHome))
                {
                    if(!string.IsNullOrWhiteSpace(jenkinsUrl))
                    {
                        isRunningUnderJenkins = true;
                    }
                }

                //IEnumerable<ServiceController> jenkinsSvcs = ServiceController.GetServices().Where<ServiceController>((svc) => svc.DisplayName.ToLower().Contains("jenkins"));
                //if (jenkinsSvcs.Any<ServiceController>())
                //{
                //    isProcessRunningAsService = true;
                //}

                //using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                //{
                //    isRunningAsSystemAccount = identity.IsSystem;
                //}

                return (isRunningUnderTravis || isRunningUnderJenkins);
            }
        }
    }
}
