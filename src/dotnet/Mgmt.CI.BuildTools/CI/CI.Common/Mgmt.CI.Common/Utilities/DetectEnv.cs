// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Common.Utilities
{
    using System;
    using System.Runtime.InteropServices;
    public class DetectEnv
    {
        public static bool IsRunningUnderLinux
        {
            get
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            }
        }

        public static bool IsRunningUnderMacOS
        {
            get
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            }
        }

        public static bool IsRunningUnderWindowsOS
        {
            get
            {
                bool _isWindows = false;
                string emulateWindowsEnv = Environment.GetEnvironmentVariable("emulateWindowsEnv");
                if (string.IsNullOrEmpty(emulateWindowsEnv))
                {
                    _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                }
                else
                {
                    _isWindows = true;
                }

                return _isWindows;
            }
        }

        public static bool IsRunningUnderNonWindows
        {
            get
            {
                bool _isNonWindows = false;
                string emulateNonWindowsEnv = Environment.GetEnvironmentVariable("emulateNonWindowsEnv");

                if(string.IsNullOrEmpty(emulateNonWindowsEnv))
                {
                    bool isLinux = IsRunningUnderLinux;
                    bool isMac = IsRunningUnderMacOS;

                    if (isLinux || isMac)
                        _isNonWindows = true;
                    else
                        _isNonWindows = false;
                }
                else
                {
                    _isNonWindows = true;
                }

                return _isNonWindows;
            }
        }
    }
}
