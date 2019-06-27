// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.


namespace Tests.CI.BuildTasks.ModelTests
{
    using MS.Az.Mgmt.CI.BuildTasks.Models;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Tests.CI.Common.Base;
    using Xunit;

    public class MsBuildModelTest : BuildTasksTestBase
    {
        //MsbuildProject msBuildProj;
        public MsBuildModelTest()
        {
            RepoRootDirPath = @"D:\myFork\sdkForNet\master";
        }

        [Fact]
        public void LoadPropsFile()
        {
            string sdkPropFilePath = this.FileSys.FindFilePath(RepoRootDirPath, "AzSdk.reference.props");
            Assert.NotNull(sdkPropFilePath);
            MsbuildProject sdkProp = new MsbuildProject(sdkPropFilePath);
            Assert.NotNull(sdkProp);
            sdkProp.Dispose();
        }

        [Fact]
        public void VerifyTargetFx()
        {
            string sdkPropFilePath = this.FileSys.FindFilePath(RepoRootDirPath, "AzSdk.reference.props");
            Assert.NotNull(sdkPropFilePath);
            MsbuildProject sdkProp = new MsbuildProject(sdkPropFilePath);
            Assert.NotNull(sdkProp);

            string targetFx = sdkProp.GetPropertyValue("targetframework");
            Assert.Empty(targetFx);
            targetFx = sdkProp.GetPropertyValue("SdkTargetFx");
            Assert.NotNull(@"net452;net461;netstandard1.4;netstandard2.0");
            sdkProp.Dispose();
        }

        [Fact]
        public void GetPkgReferenceAndVersionInfo()
        {
            string sdkPropFilePath = this.FileSys.FindFilePath(RepoRootDirPath, "AzSdk.reference.props");
            Assert.NotNull(sdkPropFilePath);
            MsbuildProject sdkProp = new MsbuildProject(sdkPropFilePath);
            Assert.NotNull(sdkProp);

            Dictionary<string, string> refVer = sdkProp.GetNugetPkgRefsAndVersionInfo();
            Assert.NotNull(refVer);
            Assert.True(refVer.Count > 2);
            sdkProp.Dispose();
        }
    }
}
