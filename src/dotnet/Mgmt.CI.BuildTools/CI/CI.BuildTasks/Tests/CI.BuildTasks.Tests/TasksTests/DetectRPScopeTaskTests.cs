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
        const string NET_SDK_PUB_URL = @"http://github.com/azure/azure-sdk-for-net";
        const string NET_SDK_PUB_URL_pr = @"https://github.com/azure/azure-sdk-for-net-pr";
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
            DetectRPScopeTask rpScope = new DetectRPScopeTask(NET_SDK_PUB_URL, "-1");
            if(rpScope.Execute())
            {
                Assert.Empty(rpScope.ScopesFromPR);
            }
        }

        [Fact]
        public void NonExistantPrNumber()
        {
            DetectRPScopeTask rpScope = new DetectRPScopeTask(NET_SDK_PUB_URL, "10945356");
            if (rpScope.Execute())
            {
                Assert.Empty(rpScope.ScopesFromPR);
            }
            //Assert.Throws<ArgumentException>(() => rpScope.Execute());
        }

        [Fact]
        public void DefaultToBuildEntireMgmtProjects()
        {
            string ghUrl = NET_SDK_PUB_URL;
            long ghPrNumber = 6453; //6606
            DetectRPScopeTask rpScope = new DetectRPScopeTask(ghUrl, ghPrNumber.ToString());

            if (rpScope.Execute())
            {
                Assert.Empty(rpScope.ScopesFromPR);
                Assert.True(string.IsNullOrWhiteSpace(rpScope.PRScopeString));
            }
            else
            {
                Assert.True(false);
            }
        }

        [Fact]
        public void MultipleScopes()
        {
            string ghUrl = NET_SDK_PUB_URL;
            long ghPrNumber = 6620;
            DetectRPScopeTask rpScope = new DetectRPScopeTask(ghUrl, ghPrNumber.ToString());

            if(rpScope.Execute())
            {
                Assert.True(rpScope.ScopesFromPR.Length > 1);
                Assert.True(!string.IsNullOrWhiteSpace(rpScope.PRScopeString));
            }
            else
            {
                Assert.True(false);
            }
        }

        [Fact]
        public void PrivateRepoPR()
        {
            string ghUrl = NET_SDK_PUB_URL_pr;
            long ghPrNumber = 923;
            DetectRPScopeTask rpScope = new DetectRPScopeTask(ghUrl, ghPrNumber.ToString());

            if (rpScope.Execute())
            {
                Assert.Empty(rpScope.ScopesFromPR);
                Assert.True(string.IsNullOrWhiteSpace(rpScope.PRScopeString));
            }
            else
            {
                Assert.True(false);
            }
        }

        [Theory]
        [InlineData(NET_SDK_PUB_URL, 6396)]
        [InlineData(NET_SDK_PUB_URL, 6418)]
        [InlineData(NET_SDK_PUB_URL, 6419)]
        [InlineData(NET_SDK_PUB_URL, 6304)]
        [InlineData(NET_SDK_PUB_URL, 6453)]
        [InlineData(NET_SDK_PUB_URL, 6687)]
        [InlineData(@"azure/azure-sdk-for-net", 6304)]
        public void SingleScope(string ghUrl, long ghPrNumber)
        {
            DetectRPScopeTask rpScope = new DetectRPScopeTask(ghUrl, ghPrNumber.ToString());

            if (rpScope.Execute())
            {
                switch (ghPrNumber)
                {
                    case 6687:
                        {
                            Assert.Empty(rpScope.ScopesFromPR);
                            break;
                        }

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
                            Assert.Single(rpScope.ScopesFromPR);
                            Assert.True(!string.IsNullOrWhiteSpace(rpScope.PRScopeString));
                            break;
                        }
                    case 6453:
                        {
                            Assert.Empty(rpScope.ScopesFromPR);
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
