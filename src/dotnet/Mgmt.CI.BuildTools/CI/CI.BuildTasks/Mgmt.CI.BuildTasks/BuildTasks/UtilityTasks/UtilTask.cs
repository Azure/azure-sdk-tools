// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.UtilityTasks
{
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using MS.Az.Mgmt.CI.BuildTasks.Common.ExtensionMethods;

    public class UtilTask : NetSdkBuildTask
    {
        #region const

        #endregion

        #region fields

        #endregion

        #region Properties
        #region Input properties
        public string UtilityName { get; set; }

        public string ToolsPkgRootDirPath { get; set; }

        public string PsModuleRootDirPath { get; set; }
        #endregion

        #region internal properties
        BuildUtilityTaskEnum BuildUtility { get; set; }
        #endregion
        public override string NetSdkTaskName => "UtilTask";
        #endregion

        #region Constructor
        public UtilTask()
        {
            UtilityName = string.Empty;
            BuildUtility = BuildUtilityTaskEnum.NotSupported;
        }
        #endregion

        #region Public Functions
        public override bool Execute()
        {
            base.Execute();
            ParseInput();
            ExecuteUtility();

            //if(WhatIf)
            //{
            //    WhatIfAction();
            //}

            return TaskLogger.TaskSucceededWithNoErrorsLogged;
        }

        protected override void WhatIfAction()
        {
            
        }
        #endregion

        #region private functions

        void ExecuteUtility()
        {
            switch(BuildUtility)
            {
                case BuildUtilityTaskEnum.InstallProjectTemplates:
                    {
                        
                        break;
                    }
                case BuildUtilityTaskEnum.InstallPsModules:
                    {
                        InstallPsModulesUtilTask psModUtil = new InstallPsModulesUtilTask(this.TaskLogger);
                        psModUtil.PsModulesSourceRootDirPath = PsModuleRootDirPath;
                        psModUtil.ExecuteUtil();
                        break;
                    }
                case BuildUtilityTaskEnum.NotSupported:
                    {
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }

        void ParseInput()
        {
            var utilValues = Enum.GetValues(typeof(BuildUtilityTaskEnum)).Cast<BuildUtilityTaskEnum>();

            if (!string.IsNullOrWhiteSpace(UtilityName))
            {
                foreach (BuildUtilityTaskEnum member in utilValues)
                {
                    if (member.GetDescriptionAttributeValue().Equals(UtilityName, StringComparison.OrdinalIgnoreCase))
                    {
                        BuildUtility = member;
                        break;
                    }
                }
            }

            if(BuildUtility == BuildUtilityTaskEnum.NotSupported)
            {
                string helpStrFormat = "msbuild build.proj /t:Util /p:UtilityName={0}";
                foreach(BuildUtilityTaskEnum member in utilValues)
                {
                    if(member != BuildUtilityTaskEnum.NotSupported)
                    {
                        TaskLogger.LogWarning(helpStrFormat, member.ToString());
                    }
                }
                //TaskLogger.LogException<ApplicationException>("Unable to execute task without valid UtilityName");
            }
        }

        #endregion

    }

    enum BuildUtilityTaskEnum
    {
        [Description("InstallProjectTemplates")]
        InstallProjectTemplates,

        [Description("InstallPsModules")]
        InstallPsModules,

        [Description("NotSupported")]
        NotSupported
    }
}
