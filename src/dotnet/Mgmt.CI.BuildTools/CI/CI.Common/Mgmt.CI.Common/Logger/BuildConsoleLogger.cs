// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.Common.Logger
{
    using Microsoft.Build.Framework;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Logger;
    using System;
    using System.Diagnostics;

    public class BuildConsoleLogger : NetSdkBuildTaskLogger /*: INetSdkTaskLogger */
    {
        public override void LogInfo(MessageImportance msgImportance, string messageToLog)
        {
#if DEBUG
            Debug.WriteLine(messageToLog);
#else
            Console.WriteLine(messageToLog);
#endif
        }

        public override void LogError(string errorMessage)
        {
            ShowError(errorMessage);
        }

        public override void LogException(Exception ex, bool showDetails)
        {
            ShowError(ex.ToString());
            throw ex;
        }

        public override void LogWarning(string warningMessage)
        {
            ShowWarning(warningMessage);
        }

        void ShowError(string errorMessage)
        {
#if DEBUG
            Debug.WriteLine(errorMessage);            
#else
            ConsoleColor currentForegroundColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;

            if(Console.BackgroundColor == ConsoleColor.Red)
            {
                Console.ForegroundColor = ConsoleColor.White;
            }

            Console.WriteLine(errorMessage);
            Console.ForegroundColor = currentForegroundColor;
#endif

        }

        void ShowWarning(string warningMessage)
        {
#if DEBUG
            Debug.WriteLine(warningMessage);
#else
            ConsoleColor currentForegroundColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkYellow;

            if (Console.BackgroundColor == ConsoleColor.DarkYellow)
            {
                Console.ForegroundColor = ConsoleColor.DarkBlue;
            }

            Console.WriteLine(warningMessage);
            Console.ForegroundColor = currentForegroundColor;
#endif
        }
    }
}
