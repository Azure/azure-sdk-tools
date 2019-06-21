// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Tests.CI.BuildTasks.TasksTests
{
    using MS.Az.Mgmt.CI.BuildTasks.BuildTasks;
    using MS.Az.NetSdk.Build.Utilities;
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

        [Fact(Skip = "Skip this test")]
        public void SkipTestExecutionForAllProjects()
        {
            ProjectSearchUtility psu = new ProjectSearchUtility(rootDir);
            List<string> scopeDirs = psu.FindTopLevelScopes(returnPartialScopePaths: true);
            Assert.NotEmpty(scopeDirs);

            foreach (string relativeScopePath in scopeDirs)
            {
                SkipBuildOrTestExecutionTask ste = new SkipBuildOrTestExecutionTask(rootDir);
                ste.BuildScope = relativeScopePath;
                ste.SkipFromTestExecution = true;
                ste.ProjectType = "Test";
                Assert.True(ste.Execute());
            }
        }
    }
}
