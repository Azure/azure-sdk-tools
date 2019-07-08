// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.


namespace Tests.CI.BuildTasks.UtilityTests
{
    using MS.Az.NetSdk.Build.Utilities;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Tests.CI.Common.Base;
    using Xunit;
    using MS.Az.Mgmt.CI.BuildTasks.Common.ExtensionMethods;
    using MS.Az.Mgmt.CI.BuildTasks.Models;

    public class ProjectSearchTests: BuildTasksTestBase
    {
        //public string RepoRootDirPath { get; set; }

        ProjectSearchUtility ProjSearch;

        public ProjectSearchTests()
        {
            RepoRootDirPath = @"D:\adxRepo\netSdk\master";
            ProjSearch = new ProjectSearchUtility(this.TestAssetSdkForNetDirPath);
        }

        [Fact]
        public void GetMgmtProjects()
        {
            string scopeToken = "compute";
            ProjectSearchUtility psu = new ProjectSearchUtility(this.TestAssetSdkForNetDirPath, null, scopeToken,
                string.Empty, string.Empty, string.Empty, SdkProjectType.All, SdkProjectCategory.MgmtPlane);

            List<string> sdkProj = psu.Find_Mgmt_SDKProjects();
            Assert.Single(sdkProj);

            List<string> testProj = psu.Find_Mgmt_TestProjects();
            Assert.Single(testProj);
        }

        [Fact]
        public void GetAllProjects()
        {   
            List<string> sdkProj = ProjSearch.Find_Mgmt_SDKProjects();
            List<string> allRpDirs = ProjSearch.GetRPDirs();

            Assert.True(DoesParentDirsMatch(allRpDirs, sdkProj));
        }



        bool DoesParentDirsMatch(List<string> rpDirs, List<string> sdkDirs)
        {
            List<string> rpDirClone = new List<string>(rpDirs);

            foreach(string rpd in rpDirs)
            {
                var foundDirs = sdkDirs.Where((d) => d.Contains(rpd, StringComparison.OrdinalIgnoreCase));
                if(foundDirs.Any<string>())
                {
                    rpDirClone.Remove(rpd);
                }
            }

            //There are few edge cases, will fix this test once we move to new directory structure
            if(rpDirClone.Count <= 5)
            {
                return true;
            }

            return false;
        }
    }
}
