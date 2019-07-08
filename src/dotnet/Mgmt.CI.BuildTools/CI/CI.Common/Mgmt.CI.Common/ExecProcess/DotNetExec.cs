// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.Common.ExecProcess
{
    using MS.Az.Mgmt.CI.BuildTasks.Common.ExecProcess;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Utilities;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    public class DotNetExec : ShellExec
    {
        #region const

        #endregion

        #region fields
        //string _dllDirPath;
        //string _generatedAssemblyFullPath;
        #endregion

        #region Properties
        FileSystemUtility FSUtil { get; set; }
        string ProjectSlnFilePath { get; set; }

        #endregion

        #region Constructor
        public DotNetExec(string projSlnFilePath)
        {
            ProjectSlnFilePath = projSlnFilePath;
        }
        #endregion

        #region Public Functions
        public override int ExecuteCommand()
        {
            this.ShellProcessCommandPath = "dotnet.exe";
            int exitCode = base.ExecuteCommand();
            this.AnalyzeAndLogFinalExitCode();
            return exitCode;
        }
        protected override string BuildShellProcessArgs()
        {
            //dotnet.exe build <path To csproj> list of properties
            string buildPropertyList = @"/v:Q /p:GenerateAssemblyConfigurationAttribute=false /p:GenerateAssemblyVersionAttribute=false /p:GenerateAssemblyFileVersionAttribute=false /p:GenerateAssemblyProductAttribute=false /p:GenerateAssemblyTitleAttribute=false /p:GenerateAssemblyDescriptionAttribute=false /p:GenerateAssemblyCompanyAttribute=false";
            string argFormat = @"build {0} {1}";
            string arg = string.Format(argFormat, ProjectSlnFilePath, buildPropertyList);
            return arg;
        }
        #endregion

        #region private functions

        #endregion

    }
}