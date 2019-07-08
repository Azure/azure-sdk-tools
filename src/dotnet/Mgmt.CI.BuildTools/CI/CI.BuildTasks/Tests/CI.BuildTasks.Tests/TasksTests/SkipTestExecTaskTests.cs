// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Tests.CI.BuildTasks.TasksTests
{
    using MS.Az.Mgmt.CI.BuildTasks.BuildTasks;
    using MS.Az.NetSdk.Build.Utilities;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Tests.CI.Common.Base;
    using Xunit;
    using Xunit.Abstractions;

    public class SkipTestExecTaskTests : BuildTasksTestBase
    {
        #region fields
        internal string rootDir = string.Empty;
        internal string sourceRootDir = string.Empty;
        internal string ignoreDir = string.Empty;
        readonly ITestOutputHelper OutputTrace;
        #endregion
        public SkipTestExecTaskTests(ITestOutputHelper output)
        {
            rootDir = this.TestAssetsDirPath;
            rootDir = Path.Combine(rootDir, "sdkForNet");
            sourceRootDir = rootDir;

            this.OutputTrace = output;
        }

        [Fact]
        public void SkipTestExecutionForOneProjects()
        {
            ProjectSearchUtility psu = new ProjectSearchUtility(rootDir);            
            List<string> scopeDirs = psu.FindTopLevelScopes(returnPartialScopePaths: true);
            Assert.NotEmpty(scopeDirs);

            SkipBuildOrTestExecutionTask ste = new SkipBuildOrTestExecutionTask(rootDir);
            ste.BuildScope = @"Advisor";
            ste.SkipFromTestExecution = true;
            ste.ProjectType = "Test";
            Assert.True(ste.Execute());
        }

        [Fact]
        public void SkipTestExecutionForMultipleProjects()
        {
            SkipBuildOrTestExecutionTask ste = new SkipBuildOrTestExecutionTask(rootDir);
            ste.BuildScopes = @"Advisor;Cdn";
            ste.SkipFromTestExecution = true;
            ste.ProjectType = "Test";
            Assert.True(ste.Execute());
        }

        [Fact]
        public void SkipPrivateDns()
        {
            SkipBuildOrTestExecutionTask ste = new SkipBuildOrTestExecutionTask(rootDir);
            ste.BuildScope = @"privatedns";
            ste.SkipFromTestExecution = true;
            ste.SkipFromBuild = true;
            ste.ProjectType = "Test";
            Assert.True(ste.Execute());
        }

        [Fact]
        public void SkipTestExecutionForAllProjects()
        {
            SkipBuildOrTestExecutionTask ste = new SkipBuildOrTestExecutionTask(rootDir);
            ste.BuildScope = "sdk";
            ste.SkipFromTestExecution = true;
            ste.ProjectType = "Test";
            Assert.Throws<NotSupportedException>(() => ste.Execute());

            //We are executing the positive case under whatIf to avoid file changes
            ste = new SkipBuildOrTestExecutionTask(rootDir);
            string sdkComputeScope = Path.Combine("sdk", "compute");
            ste.BuildScopes = string.Concat(sdkComputeScope, ";", "storage");
            ste.SkipFromTestExecution = true;
            ste.ProjectType = "Test";
            ste.WhatIf = true;

            Assert.True(ste.Execute());
        }

        [Fact]
        public void ExecuteSdkScope()
        {
            SkipBuildOrTestExecutionTask ste = new SkipBuildOrTestExecutionTask(rootDir);
            ste.BuildScopes = "sdk;compute";
            ste.SkipFromTestExecution = true;
            ste.ProjectType = "Test";
            Assert.Throws<NotSupportedException>(() => ste.Execute());
        }
    }
}
