// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.Common.Utilities
{
    using System;
    using System.Runtime.InteropServices;
    using System.Runtime.InteropServices.ComTypes;

    class NativeMethods
    {
        #region DLL Imports
        [DllImport("ole32.dll")]
        private static extern void CreateBindCtx(int reserved, out IBindCtx ppbc);

        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);
        #endregion

        #region public Functions
        static void QueryRot()
        {
            IRunningObjectTable rot;
            IEnumMoniker enumMoniker;
            int retVal = GetRunningObjectTable(0, out rot);

            if (retVal == 0)
            {
                rot.EnumRunning(out enumMoniker);

                IntPtr fetched = IntPtr.Zero;
                IMoniker[] moniker = new IMoniker[1];
                while (enumMoniker.Next(1, moniker, fetched) == 0)
                {
                    IBindCtx bindCtx;
                    CreateBindCtx(0, out bindCtx);
                    string displayName;
                    moniker[0].GetDisplayName(bindCtx, null, out displayName);
                    Console.WriteLine("Display Name: {0}", displayName);
                }
            }
        }
        #endregion
    }
}
