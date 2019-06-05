// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Tests.CI.BuildTasks.TasksTests
{
    using MS.Az.Mgmt.CI.BuildTasks.BuildTasks.PreBuild;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Tests.CI.Common.Base;
    using Xunit;
    using Xunit.Abstractions;

    public class DetectRPScopeTaskTests : BuildTasksTestBase
    {
        #region CONST
        const string NET_SDK_PUB_URL = @"https://github.com/azure/azure-sdk-for-net";
        #endregion
        #region field
        internal string rootDir = string.Empty;
        internal string sourceRootDir = string.Empty;
        readonly ITestOutputHelper OutputTrace;
        #endregion

        public DetectRPScopeTaskTests(ITestOutputHelper output)
        {
            // create an env. variable 'testAssetdir' and point to a directory that will host multiple repos
            // e.g. sdkfornet directory structure as well as Fluent directory structure
            // basically test asset directory will be the root for all other repos that can be used for testing directory structure
            //rootDir = this.TestAssetsDirPath;
            //rootDir = Path.Combine(rootDir, "sdkForNet");
            //sourceRootDir = rootDir;

            this.OutputTrace = output;
        }

        [Fact]
        public void InvalidPrNumber()
        {
            DetectRPScopeTask rpScope = new DetectRPScopeTask(NET_SDK_PUB_URL, -1);
            Assert.Throws<ArgumentException>(() => rpScope.Execute());
        }

        [Theory]
        [InlineData(NET_SDK_PUB_URL, 6396)]
        [InlineData(NET_SDK_PUB_URL, 6418)]
        [InlineData(NET_SDK_PUB_URL, 6419)]
        [InlineData(NET_SDK_PUB_URL, 6304)]
        [InlineData(NET_SDK_PUB_URL, 6453)]
        [InlineData(@"azure/azure-sdk-for-net", 6304)]
        public void SingleScope(string ghUrl, long ghPrNumber)
        {
            DetectRPScopeTask rpScope = new DetectRPScopeTask(ghUrl, ghPrNumber);

            if (rpScope.Execute())
            {
                switch (ghPrNumber)
                {
                    case 6396:
                        {
                            Assert.NotNull(rpScope.ScopesFromPR);
                            Assert.True(rpScope.ScopesFromPR.Length == 1);
                            break;
                        }

                    case 6418:
                        {
                            Assert.NotNull(rpScope.ScopesFromPR);
                            Assert.True(rpScope.ScopesFromPR.Length == 1);
                            break;
                        }

                    case 6419:
                        {
                            Assert.NotNull(rpScope.ScopesFromPR);
                            Assert.True(rpScope.ScopesFromPR.Length == 1);
                            break;
                        }

                    case 6304:
                        {
                            Assert.NotNull(rpScope.ScopesFromPR);
                            Assert.True(rpScope.ScopesFromPR.Length == 1);
                            break;
                        }
                    case 6453:
                        {
                            Assert.Null(rpScope.ScopesFromPR);
                            break;
                        }
                    default:
                        {
                            Assert.True(false);
                            break;
                        }
                }
            }
            else
            {
                Assert.True(false);
            }
        }
    }
}
