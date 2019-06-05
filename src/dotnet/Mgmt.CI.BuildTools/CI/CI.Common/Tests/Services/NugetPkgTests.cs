// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Tests.CI.Common.Services
{
    using MS.Az.Mgmt.CI.BuildTasks.Services;
    using NuGet.Versioning;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Tests.CI.Common.Base;
    using Xunit;

    public class NugetPkgTests: BuildTasksTestBase
    {

        [Fact]
        public void GetPublishedPackages()
        {
            string pkgName = @"Microsoft.Rest.ClientRuntime.Azure";
            NugetServerClient nugServer = new NugetServerClient();
            List<NuGetVersion> allVersions = nugServer.GetAvailablePackageVersion(pkgName);
            Assert.NotNull(allVersions);
            Assert.True(allVersions.Count > 3);

            NuGetVersion highestVer = nugServer.GetHighestVersion(pkgName);
            Assert.True(highestVer.Major > 2);
        }

        [Fact]
        public void ParseNugVersion()
        {
            string rng = @"[3.3.19, 4.0.0)";
            if(VersionRange.TryParse(rng, out VersionRange verRng))
            {
                Assert.Equal("3.3.19", verRng.MinVersion.ToString());
            }
        }
    }
}
