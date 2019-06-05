// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.Common.Services
{
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Utilities;
    using MS.Az.Mgmt.CI.BuildTasks.Services;
    using NuGet.Versioning;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class SdkNugetPackageSvc : NetSdkUtilTask
    {
        #region const

        #endregion

        #region fields
        NuGetVersion _highestPublishedVersion;
        #endregion

        #region Properties
        public string NugetVersionString { get; set; }

        public Version NugetPkgVersion { get; set; }

        public string NugetPkgName { get; set; }

        NuGetVersion HighestPublishedVersion
        {
            get
            {
                if(_highestPublishedVersion == null)
                {
                    NugetServerClient nugServer = new NugetServerClient();
                    _highestPublishedVersion = nugServer.GetHighestVersion(NugetPkgName);
                }

                return _highestPublishedVersion;
            }
        }


        #endregion

        #region Constructor
        public SdkNugetPackageSvc(string nugetPkgName, string nugPkgVersionString)
        {
            Check.NotEmptyNotNull(nugPkgVersionString);
            Check.NotEmptyNotNull(nugetPkgName);

            NugetPkgName = nugetPkgName;
            NugetVersionString = nugPkgVersionString;
            NuGetVersion nugVer = new NuGetVersion(NugetVersionString);

            NugetPkgVersion = nugVer.Version;
        }
        #endregion

        #region Public Functions

        #endregion

        #region private functions
        NuGetVersion GetHighestPublishedVersion()
        {
            NugetServerClient nugServer = new NugetServerClient();
            return nugServer.GetHighestVersion(NugetPkgName);
        }
        #endregion

    }
}
