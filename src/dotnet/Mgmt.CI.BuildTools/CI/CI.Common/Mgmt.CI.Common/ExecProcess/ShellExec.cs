// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Common.ExecProcess
{
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;

    public class ShellExec : NetSdkUtilTask
    {
        #region CONST
        const int DEFAULT_WAIT_TIMEOUT = 60000;  // 60 seconds default timeout
                                                 //const string COMMAND_ARGS = "push {0} -source {1} -ApiKey {2} -NonInteractive -Timeout {3}";

        const int E_FAIL = -2147467259;
        const int ERROR_FILE_NOT_FOUND = 2;

        #region const
        const string GIT_DIR_POSTFIX = "_shEx";
        const int TEMP_DIR_COUNT = 1000;
        #endregion
        #endregion

        #region Fields
        Process _shellProc;
        ProcessStartInfo _shellProcStartInfo;
        string _shellProcCommandPath;
        string _workingDirPath;

        #endregion

        #region Properties
        public int LastExitCode { get; protected set; }

        public Exception LastException { get; protected set; }

        public virtual string ShellProcessCommandPath
        {
            get => _shellProcCommandPath;
            set => _shellProcCommandPath = value;
        }

        protected virtual int DefaultTimeOut
        {
            get => DEFAULT_WAIT_TIMEOUT;
        }

        public virtual string WorkingDirPath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_workingDirPath))
                {
                    _workingDirPath = Directory.GetCurrentDirectory();
                }

                return _workingDirPath;
            }

            set
            {
                _workingDirPath = value;
            }
        }

        public virtual ProcessStartInfo ShellProcessInfo
        {
            get
            {
                if (_shellProcStartInfo == null)
                {
                    _shellProcStartInfo = new ProcessStartInfo(ShellProcessCommandPath);
                    _shellProcStartInfo.WorkingDirectory = WorkingDirPath;
                    _shellProcStartInfo.CreateNoWindow = true;
                    _shellProcStartInfo.UseShellExecute = false;
                    _shellProcStartInfo.RedirectStandardError = true;
                    _shellProcStartInfo.RedirectStandardInput = true;
                    _shellProcStartInfo.RedirectStandardOutput = true;
                }

                return _shellProcStartInfo;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        public Process ShellProcess
        {
            get
            {
                if (_shellProc == null)
                {
                    _shellProc = new Process();
                    _shellProc.StartInfo = ShellProcessInfo;
                }

                return _shellProc;
            }
        }

        #endregion

        #region Constructor
        protected ShellExec()
        {
            _shellProcCommandPath = string.Empty;
        }

        public ShellExec(string commandPath) : this()
        {
            ShellProcessCommandPath = commandPath;
        }
        #endregion

        #region Public Functions
        protected virtual string BuildShellProcessArgs()
        {
            throw new NotImplementedException();
        }

        public virtual int ExecuteCommand()
        {
            return ExecuteCommand(BuildShellProcessArgs(), WorkingDirPath);
        }

        public virtual int ExecuteCommand(string args)
        {
            return ExecuteCommand(args, WorkingDirPath);
        }

        public virtual int ExecuteCommand(string args, string workingDir)
        {
            try
            {
                ShellProcessInfo.WorkingDirectory = workingDir;
                ShellProcess.StartInfo.Arguments = args;
                ShellProcess.Start();
                ShellProcess.WaitForExit(DefaultTimeOut);
                LastExitCode = ShellProcess.ExitCode;
            }
            catch (Win32Exception win32Ex)
            {
                LastExitCode = win32Ex.ErrorCode;
                LastException = win32Ex;
            }
            catch (Exception ex)
            {
                LastExitCode = ex.HResult;
                LastException = ex;
            }

            return LastExitCode;
        }

        public virtual string GetError()
        {
            return ShellProcess.StandardError?.ReadToEnd();
        }

        public virtual string GetOutput()
        {
            return ShellProcess.StandardOutput?.ReadToEnd();
        }

        public virtual string AnalyzeExitCode(int exitCode = 9999)
        {
            StringBuilder sb = new StringBuilder();
            if (exitCode == 9999) exitCode = LastExitCode;

            if (LastException != null)
            {
                sb.AppendLine(LastException.ToString());
            }
            else
            {
                if (exitCode != 0)
                {
                    sb.AppendLine(ShellProcess.StandardError?.ReadToEnd());
                }
                else
                {
                    sb.AppendLine(ShellProcess?.StandardOutput?.ReadToEnd());
                }
            }

            return sb.ToString();
        }

        public virtual void AnalyzeAndLogFinalExitCode()
        {
            string output = AnalyzeExitCode();

            if (LastException != null)
            {
                UtilLogger.LogException(LastException);
            }
            else
            {
                if (LastExitCode != 0)
                {
                    UtilLogger.LogError(output);
                }
                else
                {
                    UtilLogger.LogInfo(output);
                }
            }
        }

        public string GetTempLocation()
        {
            string WorkspaceDirPath = Path.GetTempPath();
            int tempDirCount = 0;
            //string tempFileName = string.Concat(Path.GetTempFileName(), GIT_DIR_POSTFIX);
            string tempFileName = string.Concat(Path.GetFileNameWithoutExtension(Path.GetTempFileName()), GIT_DIR_POSTFIX);
            string tempCloneDir = Path.Combine(WorkspaceDirPath, tempFileName);

            while (DirFileExists(tempCloneDir) && tempDirCount < TEMP_DIR_COUNT)
            {
                tempFileName = string.Concat(Path.GetFileNameWithoutExtension(Path.GetTempFileName()), GIT_DIR_POSTFIX);
                tempCloneDir = Path.Combine(WorkspaceDirPath, tempFileName);
                tempDirCount++;
            }

            if (tempDirCount >= TEMP_DIR_COUNT)
            {
                ApplicationException appEx = new ApplicationException(string.Format("Cleanup Temp. More than '{0}' directories detected", TEMP_DIR_COUNT.ToString()));
                //UtilLogger.LogException(appEx);
            }

            if (!Directory.Exists(tempCloneDir))
            {
                Directory.CreateDirectory(tempCloneDir);
            }

            return tempCloneDir;
        }

        private bool DirFileExists(string path)
        {
            bool dirExists = true;
            bool fileExists = true;
            bool dirNotEmpty = true;
            bool dirFileExists = true;

            dirExists = Directory.Exists(path);
            fileExists = File.Exists(path);
            if (Directory.Exists(path))
            {
                var contents = Directory.EnumerateFiles(path);
                dirNotEmpty = contents.Any<string>();
            }
            else
            {
                dirNotEmpty = false;
            }

            dirFileExists = (dirExists && fileExists && dirNotEmpty);
            return dirFileExists;
        }
        #endregion
    }
}
