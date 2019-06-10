// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Tests.CI.BuildTasks.TasksTests
{
    using MS.Az.Mgmt.CI.BuildTasks.Models;
    using MS.Az.Mgmt.CI.BuildTasks.Tasks.PreBuild;
    using MS.Az.NetSdk.Build.Utilities;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Tests.CI.Common.Base;
    using Xunit;
    using Xunit.Abstractions;
    using System.Linq;
    using MS.Az.Mgmt.CI.BuildTasks.BuildTasks;

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

            SkipTestExecutionTask ste = new SkipTestExecutionTask(rootDir);
            ste.BuildScope = @"SDKs\Advisor";
            ste.ExcludeFromTestExecution = true;
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
                SkipTestExecutionTask ste = new SkipTestExecutionTask(rootDir);
                ste.BuildScope = relativeScopePath;
                ste.ExcludeFromTestExecution = true;
                ste.ProjectType = "Test";
                Assert.True(ste.Execute());
            }
        }
    }
}
