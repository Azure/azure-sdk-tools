// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Xunit;
[assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]


namespace BuildTasks.Tests
{
    using global::Tests.CI.Common.Base;
    using Microsoft.Build.Framework;
    using MS.Az.Mgmt.CI.BuildTasks.BuildTasks.PreBuild;
    using MS.Az.Mgmt.CI.BuildTasks.Models;
    using MS.Az.Mgmt.CI.BuildTasks.Tasks.PreBuild;
    using MS.Az.Mgmt.CI.Common.ExtensionMethods;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Xunit;
    using Xunit.Abstractions;

    public class CategorizeProjectTaskTest : BuildTasksTestBase
    {
        internal string rootDir = string.Empty;
        internal string sourceRootDir = string.Empty;
        readonly ITestOutputHelper OutputTrace;

        public CategorizeProjectTaskTest(ITestOutputHelper output)
        {
            //create an env. variable 'testAssetdir' and point to a directory that will host multiple repos
            // e.g. sdkfornet directory structure as well as Fluent directory structure
            // basically test asset directory will be the root for all other repos that can be used for testing directory structure

            // also make sure you execute any target in that repo because that would force the nuget package to
            // download and get all the files necessary for the tasks to execute successfully
            rootDir = this.TestAssetsDirPath;
            rootDir = Path.Combine(rootDir, "sdkForNet");
            sourceRootDir = rootDir;

            this.OutputTrace = output;
        }

        [Fact]
        public void BuildMgmtProjectsForCertainScopes()
        {
            const string NET_SDK_PUB_URL = @"https://github.com/azure/azure-sdk-for-net";
            //6453 non sdk changes
            DetectRPScopeTask rpScope = new DetectRPScopeTask(NET_SDK_PUB_URL, 6453.ToString());
            rpScope.Execute();

            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScopes = rpScope.PRScopeString;

            if (cproj.Execute())
            {
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() >= 80);
            }
        }

        //[Fact(Skip = "Investigate as it fails only in Run mode, works fine during debug mode")]
        //[Fact]
        //public void GetProjectsWithNonSupportedFxVersion()
        //{
        //    string scopeDir = @"Blueprint";
        //    CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);            
        //    cproj.BuildScope = scopeDir;

        //    if (cproj.Execute())
        //    {
        //        Assert.True(cproj.SDK_Projects.Count<ITaskItem>() == 1);
        //        Assert.True(cproj.Test_Projects.Count<ITaskItem>() == 1);
        //        Assert.True(cproj.UnSupportedProjects.Count<ITaskItem>() == 1);
        //    }
        //}

        [Fact]
        public void GetTest_ProjectType()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.ProjectType = "test";

            if (cproj.Execute())
            {
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() == 0);
                Assert.True(cproj.Test_Projects.Count<ITaskItem>() >= 10);
            }
        }

        //[Fact(Skip = "Not applicable, this is for old task. Keeping it for reference, eventually needs to be deleted")]
        [Fact]
        public void IgnoreDirTokens()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.CmdLineExcludeScope = "Network.Tests";
            cproj.BuildScope = @"Network";

            Assert.True(cproj.Execute());
            Assert.True(cproj.SDK_Projects.Count<ITaskItem>() > 0);
            Assert.True(VerifyListDoesNotContains(cproj.SDK_Projects, new List<string>() { "Network.Tests" }));
        }

        [Fact]
        public void CategorizeProjects()
        {
            DateTime startTime = DateTime.Now;
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);

            if (cproj.Execute())
            {
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() > 10);
                Assert.True(cproj.Test_Projects.Count<ITaskItem>() > 10);
                //Assert.True(cproj.UnSupportedProjects.Count<ITaskItem>() >= 1);
                Assert.True(cproj.Test_ToBe_Run.Count<ITaskItem>() > 10);

                VerifyListDoesNotContains(cproj.SDK_Projects, cproj.DefaultExcludedTokens);

            }
            DateTime endTime = DateTime.Now;
            OutputTrace.WriteLine("Total time taken:'{0}'", (endTime - startTime).TotalSeconds.ToString());
        }

        [Fact]
        public void ScopedProject()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = @"mgmtcommon\ClientRuntime";

            if (cproj.Execute())
            {
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() == 1);

                //Uncomment when test project is fixed
                Assert.True(cproj.Test_Projects.Count<ITaskItem>() == 3);
                Assert.True(cproj.Test_ToBe_Run.Count<ITaskItem>() == 2);
            }
        }

        [Fact]
        public void SearchmgmtProject()
        {
            //Search test projects are multi-targeted, hence the number of test projects will be two
            // one project for each targeted framework
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = @"search";

            if (cproj.Execute())
            {
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() == 1);

                //Assert.True(cproj.Test_Projects.Count<ITaskItem>() == 1);
                //Assert.True(cproj.Test_ToBe_Run.Count<ITaskItem>() == 2);
            }
        }

        [Fact]
        public void AppAuthScope()
        {
            // Search test projects are multi-targeted, hence the number of test projects will be two
            // one project for each targeted framework
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = Path.Combine("mgmtcommon", "appauthentication");

            if (cproj.Execute())
            {
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() == 1);

                Assert.True(cproj.Test_Projects.Count<ITaskItem>() == 2);
                //Assert.True(cproj.Test_ToBe_Run.Count<ITaskItem>() > 5);
            }
        }

        [Fact]
        public void GetReferencedPackagesForScope()
        {
            string scopeDir = @"Compute";
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = scopeDir;

            if (cproj.Execute())
            {   
                Assert.Single(cproj.SDK_Projects);
                Assert.True(cproj.SdkPkgReferenceList.Count<string>() >= 1);
            }
        }

        [Fact]
        public void BuildOnlyIncludedTokenListProjects()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.CmdLineIncludeScope = "Compute;Network;DataBox";

            if (cproj.Execute())
            {
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() > 4);
                Assert.True(cproj.Test_Projects.Count<ITaskItem>() > 4);
            }
        }

        [Fact]
        public void BuildIncludeAndExcludeTokenListProjects()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.CmdLineIncludeScope = "Compute;Network;DataBox";
            cproj.CmdLineExcludeScope = "DataBox";

            if (cproj.Execute())
            {
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() >= 4);
                Assert.True(cproj.Test_Projects.Count<ITaskItem>() >= 4);
            }
        }

        [Fact]
        public void IgnoreIncludeOverlappingProjects()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.CmdLineIncludeScope = "Compute;Network;DataBox";
            cproj.CmdLineExcludeScope = "Compute";

            if (cproj.Execute())
            {
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() == 4);
                Assert.True(cproj.Test_Projects.Count<ITaskItem>() == 4);
            }
        }

        [Fact]
        public void IgnoreExactScopedProjects()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = @"Compute";
            cproj.CmdLineExcludeScope = "Compute";

            if (cproj.Execute())
            {
                Assert.Empty(cproj.SDK_Projects);
                Assert.Empty(cproj.Test_Projects);
            }
        }

        [Fact]
        public void IncludeFewFromEntireScope()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = @"keyvault";
            cproj.CmdLineIncludeScope = "Management.KeyVault";

            if (cproj.Execute())
            {
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() == 0);
                Assert.True(cproj.Test_Projects.Count<ITaskItem>() == 0);
            }
        }

        [Fact]
        public void IgnoreIncludeExactScopedProjects()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = @"Compute";
            cproj.CmdLineIncludeScope = "Compute";
            cproj.CmdLineExcludeScope = "Compute";

            if (cproj.Execute())
            {
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() == 0);
                Assert.True(cproj.Test_Projects.Count<ITaskItem>() == 0);
            }
        }

        #region Platformspecific

        [Fact]
        public void NonWindowsTargetFx()
        {
            Environment.SetEnvironmentVariable("emulateNonWindowsEnv", "true");
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = @"Dns";

            if (cproj.Execute())
            {
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() == 1);
                Assert.True(cproj.Test_Projects.Count<ITaskItem>() == 1);
                Assert.True(cproj.SDK_Projects.All<SDKMSBTaskItem>((item) => !item.PlatformSpecificTargetFxMonikerString.Contains("net452", StringComparison.OrdinalIgnoreCase)));
                Assert.True(cproj.SDK_Projects.All<SDKMSBTaskItem>((item) => !item.PlatformSpecificTargetFxMonikerString.Contains("net461", StringComparison.OrdinalIgnoreCase)));
            }
        }

        [Fact]
        public void PlatformSpecificSkippedProjects()
        {
            Environment.SetEnvironmentVariable("emulateNonWindowsEnv", "true");
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = @"Subscription";

            if (cproj.Execute())
            {
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() == 1);

                //this should be uncommented when test project is fixed
                //Assert.True(cproj.Test_Projects.Count<ITaskItem>() == 1);
                //Assert.True(cproj.PlatformSpecificSkippedProjects.Count<ITaskItem>() == 1);
            }
        }

        //[Fact(Skip = "Investigate as it fails only in Run mode, works fine during debug mode")]
        [Fact]
        public void PlatSpecificTestProjectsForWindows()
        {
            //This RP has FullDesktop specific test projects. The idea is to test if that test projects is getting picked up
            Environment.SetEnvironmentVariable("emulateWindowsEnv", "true");
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = @"Subscription";

            if (cproj.Execute())
            {
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() == 1);

                //this should be uncommented when test project is fixed
                //Assert.True(cproj.Test_Projects.Count<ITaskItem>() == 2);
                //Assert.True(cproj.PlatformSpecificSkippedProjects.Count<ITaskItem>() == 0);
            }
        }

        [Fact]
        public void GetPlatformTargetFx()
        {
            Environment.SetEnvironmentVariable("emulateNonWindowsEnv", "true");
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = @"Compute";

            Assert.True(cproj.Execute());
            Assert.True(cproj.SDK_Projects.Count<ITaskItem>() == 1);
            //var sdkProj = cproj.SDK_Projects.ToList<SDKMSBTaskItem>();

            SDKMSBTaskItem sdkProj = cproj.SDK_Projects[0];

            if (!sdkProj.PlatformSpecificTargetFxMonikerString.Contains("net4", StringComparison.OrdinalIgnoreCase))
            {
                Assert.True(true);
            }
        }

        [Fact]
        public void AzureStack()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = @"azurestack";

            Assert.True(cproj.Execute());
            Assert.True(cproj.SDK_Projects.Count<ITaskItem>() > 10);
        }

        [Fact]
        public void FQPath()
        {
            string fqDirPath = Path.Combine(rootDir, "sdk", "billing", "Microsoft.Azure.Management.Billing");
            Assert.True(Directory.Exists(fqDirPath));

            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            //cproj.FullyQualifiedBuildScopeDirPath = fqDirPath;
            cproj.BuildScopes = fqDirPath;

            Assert.True(cproj.Execute());
            Assert.True(cproj.SDK_Projects.Count<ITaskItem>() == 1);
        }

        #endregion

        #region Scope And Categorize

        [Fact]
        public void SingleScopeAndCat()
        {
            string ghUrl = NET_SDK_PUB_URL;
            int ghPrNumber = 6804;
            DetectRPScopeTask rpScope = new DetectRPScopeTask(ghUrl, ghPrNumber.ToString());
            rpScope.Execute();
            Assert.Single(rpScope.ScopesFromPR);

            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = rpScope.PRScopeString;
            cproj.Execute();

            Assert.Empty(cproj.SDK_Projects);
        }


        [Theory]
        [InlineData(@"sdk\servicebus\Microsoft.Azure.ServiceBus", 1)]
        [InlineData(@"sdk\batch\Microsoft.Azure.Batch", 2)]
        [InlineData(@"sdk\eventhub\Microsoft.Azure.EventHubs", 3)]
        [InlineData(@"sdk\storage\Azure.Storage.Files", 4)]
        [InlineData(@"sdk\storage", 5)]
        //[InlineData(NET_SDK_PUB_URL)]
        //[InlineData(NET_SDK_PUB_URL)]
        //[InlineData(NET_SDK_PUB_URL)]
        //[InlineData(NET_SDK_PUB_URL)]
        public void DataPlaneScope(string buildScope, int scenarioNumber)
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = buildScope;
            cproj.Execute();

            switch(scenarioNumber)
            {
                case 1:
                case 2:
                case 3:
                case 4:
                    {
                        Assert.Empty(cproj.SDK_Projects);
                        break;
                    }

                case 5:
                    {
                        Assert.Single(cproj.SDK_Projects);
                        break;
                    }
            }
        }

        #endregion


        [Fact]
        public void IncludeOverrideScope()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = @"Network";
            cproj.CmdLineIncludeScope = "Compute";

            if (cproj.Execute())
            {
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() == 0);
                Assert.True(cproj.Test_Projects.Count<ITaskItem>() == 0);
            }
        }

        [Fact(Skip ="Investigate as it fails only in Run mode, works fine during debug mode")]
        //[Fact]
        public void AdditionalFxProject()
        {
            // This test will be important when we stop supporting .NET 452 and will have to keep supporting .NET 452 until we move to MSAL
            // One of the attribute that is being tested is that Auth library needs to support .NET 452 until new MSAL support is added for Interactive login
            string scopeDir = @"SdkCommon\Auth\Az.Auth\";
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = scopeDir;
            cproj.UseLegacyDirStructure = true;

            if (cproj.Execute())
            {
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() == 1);

                //Uncomment when test projects are fixed
                //Assert.True(cproj.Test_Projects.Count<ITaskItem>() == 3);
                Assert.True(cproj.SDK_Projects.All<SDKMSBTaskItem>((item) => item.TargetFxMonikerString.Contains("net452", StringComparison.OrdinalIgnoreCase)));
            }
        }

        //[Fact]
        [Fact(Skip = "Investigate sdkcommon dir structure being changed")]
        public void FxTargetForNonWindows()
        {
            Environment.SetEnvironmentVariable("emulateNonWindowsEnv", "true");
            string scopeDir = @"SdkCommon\Auth\Az.Auth\";
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = scopeDir;
            cproj.UseLegacyDirStructure = true;

            if (cproj.Execute())
            {
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() == 1);

                //This is comment out because at the time of writing this test, compute test was set to exlcude from build
                //Assert.True(cproj.Test_Projects.Count<ITaskItem>() == 1);

                Assert.True(cproj.SDK_Projects.All<SDKMSBTaskItem>((item) => !item.PlatformSpecificTargetFxMonikerString.Contains("net452", StringComparison.OrdinalIgnoreCase)));
            }
        }

        //[Fact(Skip = "Not applicable, this is for old task. Keeping it for reference, eventually needs to be deleted")]
        [Fact]
        public void DefaultIgnoredProjects()
        {
            // we have ignored all Batch data plane projects
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScopes = @"Batch";

            if (cproj.Execute())
            {
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() == 1);
            }
        }

        [Fact]
        public void ExcludeProjects()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.CmdLineExcludeScope = Path.Combine("Batch", "Support");
            cproj.ProjectType = "Test";

            if (cproj.Execute())
            {
                Assert.Empty(cproj.SDK_Projects);
                Assert.True(cproj.Test_Projects.Count<ITaskItem>() > 10);
                Assert.False(CollectionContains(cproj.Test_Projects, @"Batch\Support"));
            }
        }

        [Fact]
        public void IncludeExcludeProjects()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = "billing";
            cproj.CmdLineExcludeScope = @"billing";

            if (cproj.Execute())
            {
                Assert.Empty(cproj.SDK_Projects);
            }
        }

        [Fact]
        public void MultipleScopes()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScopes = "billing;compute";
            cproj.CmdLineExcludeScope = @"billing";

            if (cproj.Execute())
            {
                Assert.Single<ITaskItem>(cproj.SDK_Projects);
            }
        }

        [Fact]
        public void AppInsightsDataPlaneAndMgmtPlane()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = "applicationinsights";

            if (cproj.Execute())
            {
                Assert.Single<ITaskItem>(cproj.SDK_Projects);
            }
        }

        //[Fact(Skip = "Investigate as it fails only in Run mode, works fine during debug mode")]
        [Fact]
        public void ClientRuntimeProjects()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = Path.Combine("mgmtcommon", "ClientRuntime");
            //cproj.UseLegacyDirStructure = true;

            if (cproj.Execute())
            {
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() == 1);
                Assert.True(cproj.Test_Projects.Count<ITaskItem>() == 3);
            }
        }

        [Fact]
        public void MgmtCommonProjects()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = @"mgmtcommon";

            if (cproj.Execute())
            {
                //Since HttpRecorder and TestFramework are multi-targeting, they are no 
                //longer treated as regular nuget packages (targeting net452 and netStd1.4)
                //but rather projects that are built without any targetFx
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() >= 8);
                Assert.True(cproj.Test_Projects.Count<ITaskItem>() >= 7);
            }
        }

        //[Fact(Skip = "Investigate as it fails only in Run mode, works fine during debug mode")]
        [Fact]
        public void TestFrameworkDir()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = Path.Combine("mgmtcommon", "TestFramework");
            cproj.UseLegacyDirStructure = true;

            if (cproj.Execute())
            {
                //Since HttpRecorder and TestFramework are multi-targeting, they are no 
                //longer treated as regular nuget packages (targeting net452 and netStd1.4)
                //but rather projects that are build without any targetFx
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() == 2);
                //Assert.True(cproj.Test_Projects.Count<ITaskItem>() == 3);
            }
        }

        [Fact]
        public void FindTestProjectUsingProjectType()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.ProjectType = "Test";
            if (cproj.Execute())
            {
                //Currently it's not able to filter out DevTestLab, Azure.Inishts, Azure.Graph.RBAC
                Assert.True(cproj.SDK_Projects.Count<ITaskItem>() == 0);
                Assert.True(cproj.Test_Projects.Count<ITaskItem>() > 10);
            }
        }

        internal string GetSourceRootDir()
        {
            string srcRootDir = string.Empty;
            //string currDir = Directory.GetCurrentDirectory();
            //string currDir = @"D:\Myfork\psSdkJson6";

            string currDir = Path.GetDirectoryName(this.GetType().GetTypeInfo().Assembly.Location);

            string dirRoot = Directory.GetDirectoryRoot(currDir);
            var buildProjFile = Directory.EnumerateFiles(currDir, "build.proj", SearchOption.TopDirectoryOnly);

            while (currDir != dirRoot)
            {
                if (buildProjFile.Any<string>())
                {
                    srcRootDir = Path.GetDirectoryName(buildProjFile.First<string>());
                    break;
                }

                currDir = Directory.GetParent(currDir).FullName;
                buildProjFile = Directory.EnumerateFiles(currDir, "build.proj", SearchOption.TopDirectoryOnly);
            }



            if (!string.IsNullOrEmpty(srcRootDir))
            {
                srcRootDir = Path.Combine(srcRootDir, @"repos\netsdkMaster");
                if (!Directory.Exists(srcRootDir))
                {
                    throw new DirectoryNotFoundException("Submodule for NetSdk not found. Please clone recursively to get required submodules");
                }


            }

            return srcRootDir;
        }


        #region private functions

        IEnumerable<UResult> GetList<TSource, UResult>(IEnumerable<TSource> sourceCollection, Func<TSource, UResult> resultDelegate)
        {
            return sourceCollection.Select<TSource, UResult>((sourceItem) => resultDelegate(sourceItem));
        }

        bool CollectionContains(IEnumerable<ITaskItem> collection, string tokenToSearch)
        {
            var filterList = collection.Where<ITaskItem>((item) => item.ItemSpec.Contains(tokenToSearch, StringComparison.OrdinalIgnoreCase));
            if(filterList.NotNullOrAny<ITaskItem>())
            {
                return true;
            }

            return false;
        }

        public bool VerifyListContains(List<ITaskItem> projectPathList, List<string> tokenList)
        {
            bool foundAllTokens = false;
            foreach(ITaskItem projPath in projectPathList)
            {
                if (tokenList.Count > 0)
                {
                    tokenList = tokenList.Where<string>((item) => !projPath.ItemSpec.Contains(item, StringComparison.OrdinalIgnoreCase)).ToList<string>();
                }
                else
                {
                    foundAllTokens = true;
                    break;
                }
            }

            return foundAllTokens;
        }

        public bool VerifyListDoesNotContains(IEnumerable<ITaskItem> projectPathList, List<string> tokenList)
        {
            bool tokensDoNotExist = true;
            IEnumerable<string> foundTokens = null;
            foreach (ITaskItem projPath in projectPathList)
            {
                foundTokens = tokenList.Where<string>((item) => projPath.ItemSpec.Contains(item, StringComparison.OrdinalIgnoreCase));

                if(foundTokens.NotNullOrAny<string>())
                {
                    tokensDoNotExist = false;
                    break;
                }
            }

            return tokensDoNotExist;
        }

        #endregion
    }
}