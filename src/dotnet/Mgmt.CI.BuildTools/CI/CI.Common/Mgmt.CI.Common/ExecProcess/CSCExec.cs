// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.Common.ExecProcess
{
    using MS.Az.Mgmt.CI.BuildTasks.Common.ExecProcess;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Utilities;
    using System.Collections.Generic;
    using System.IO;

    public class CSCExec : ShellExec
    {
        #region const

        #endregion

        #region fields
        string _dllDirPath;
        string _generatedAssemblyFullPath;
        #endregion

        #region Properties
        protected override int DefaultTimeOut => base.DefaultTimeOut;

        FileSystemUtility FSUtil { get; set; }

        public string GeneratedAssemblyFullPath
        {
            get
            {
                if(string.IsNullOrWhiteSpace(_generatedAssemblyFullPath))
                {
                    _generatedAssemblyFullPath = Path.Combine(DllDirPath, DllName);
                }

                return _generatedAssemblyFullPath;
            }
            set
            {
                _generatedAssemblyFullPath = value;
            }
        }
        public string DllName { get; set; }

        string DllDirPath
        {
            get
            {
                if(string.IsNullOrWhiteSpace(_dllDirPath))
                {
                    _dllDirPath = FSUtil.GetTempDirPath();
                }

                return _dllDirPath;
            }
        }

        public List<string> CsFileList { get; set; }
        #endregion

        #region Constructor
        public CSCExec()
        {
            FSUtil = new FileSystemUtility();
            CsFileList = new List<string>();
        }
        #endregion

        #region Public Functions
        protected override string BuildShellProcessArgs()
        {
            //csc.exe /target:library /out:assembly.dll foo.cs
            string argFormat = @"/target:library /out:{0} {1}";
            string csFiles = string.Join(" ", CsFileList);
            string arg = string.Format(argFormat, GeneratedAssemblyFullPath, csFiles);
            return arg;                
        }

        public override int ExecuteCommand()
        {
            this.ShellProcessCommandPath = "csc.exe";
            int exitCode = base.ExecuteCommand();
            this.AnalyzeAndLogFinalExitCode();
            return exitCode;
        }
        #endregion

        #region private functions

        #endregion

    }
}
