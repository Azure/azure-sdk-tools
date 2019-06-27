// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
/*
namespace BuildTasks.Tests
{
    using Microsoft.Build.Evaluation;
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Xunit;
    using Microsoft.Build.Framework;
    //using MS.Az.Mgmt.CI.BuildTasks.BuildStages;
    using MS.Az.Mgmt.CI.BuildTasks.Tasks;

    public class PostBuildTests : IClassFixture<PostBuildFixture>
    {
        PostBuildFixture _postBuildFixture;

        CategorizeProjectTaskTest catProjTest;
        public PostBuildTests(PostBuildFixture postBuildFixture)
        {
            this._postBuildFixture = postBuildFixture;
            catProjTest = new CategorizeProjectTaskTest();
        }

        [Fact]
        public void LoadFrom()
        {
            Assembly asm = Assembly.LoadFrom(@"D:\Myfork\psSdkJson6\src\SDKs\Billing\Management.Billing\bin\Debug\netstandard1.4\Microsoft.Azure.Management.Billing.dll");

            Assert.NotNull(asm);

        }

        //[Fact(Skip = "Build the scope before running this test")]
        [Fact]
        public void BuildOneProject()
        {
            SDKCategorizeProjectsTask sdkCat = new SDKCategorizeProjectsTask();
            sdkCat.SourceRootDirPath = catProjTest.sourceRootDir;
            sdkCat.BuildScope = @"SDKs\Billing";
            sdkCat.IgnorePathTokens = Path.Combine(catProjTest.ignoreDir);

            if (sdkCat.Execute())
            {
                Assert.Single<ITaskItem>(sdkCat.net452SdkProjectsToBuild);
            }

            PostBuildTask postBldTsk = new PostBuildTask()
            {
                //InvokePostBuildTask = true,
                SdkProjects = sdkCat.net452SdkProjectsToBuild
            };

            if (postBldTsk.Execute())
            {
                string apiTag = postBldTsk.ApiTag;
                string apiTagPropsFile = postBldTsk.ApiTagPropsFile;

                Assert.NotEmpty(apiTag);
                Assert.True(File.Exists(apiTagPropsFile));

                Project proj;
                if (ProjectCollection.GlobalProjectCollection.GetLoadedProjects(apiTagPropsFile).Count != 0)
                {
                    proj = ProjectCollection.GlobalProjectCollection.GetLoadedProjects(apiTagPropsFile).FirstOrDefault<Project>();
                }
                else
                {
                    proj = new Project(apiTagPropsFile);
                }

                string apiTagProperty = proj.GetPropertyValue("AzureApiTag");
                Assert.Equal(apiTag, apiTagProperty);
            }
        }


        [Fact(Skip ="Build the scope before running this test")]
        public void BuildAzureStackScope()
        {
            SDKCategorizeProjectsTask sdkCat = new SDKCategorizeProjectsTask();
            sdkCat.SourceRootDirPath = catProjTest.sourceRootDir;
            sdkCat.BuildScope = @"AzureStack\Admin\AzureBridgeAdmin";
            sdkCat.IgnorePathTokens = Path.Combine(catProjTest.ignoreDir);

            if (sdkCat.Execute())
            {
                Assert.True(sdkCat.net452SdkProjectsToBuild.Count() > 0);
            }

            PostBuildTask postBldTsk = new PostBuildTask()
            {
                //InvokePostBuildTask = true,
                SdkProjects = sdkCat.net452SdkProjectsToBuild
            };

            if (postBldTsk.Execute())
            {
                string apiTag = postBldTsk.ApiTag;
                string apiTagPropsFile = postBldTsk.ApiTagPropsFile;

                Assert.NotEmpty(apiTag);
                Assert.True(File.Exists(apiTagPropsFile));

                Project proj;
                if (ProjectCollection.GlobalProjectCollection.GetLoadedProjects(apiTagPropsFile).Count != 0)
                {
                    proj = ProjectCollection.GlobalProjectCollection.GetLoadedProjects(apiTagPropsFile).FirstOrDefault<Project>();
                }
                else
                {
                    proj = new Project(apiTagPropsFile);
                }

                string apiTagProperty = proj.GetPropertyValue("AzureApiTag");
                Assert.Equal<string>(apiTag, apiTagProperty);
            }
        }

        //[Fact]
        //public void BuildMultiApiProject()
        //{
        //    SDKCategorizeProjects sdkCat = new SDKCategorizeProjects();
        //    sdkCat.SourceRootDirPath = catProjTest.sourceRootDir;
        //    sdkCat.BuildScope = @"SDKs\Authorization\MultiApi";
        //    sdkCat.IgnorePathTokens = Path.Combine(catProjTest.ignoreDir);

        //    if (sdkCat.Execute())
        //    {
        //        Assert.True(sdkCat.net452SdkProjectsToBuild.Count() > 0);
        //    }

        //    PostBuildTask postBldTsk = new PostBuildTask()
        //    {
        //        //InvokePostBuildTask = true,
        //        SdkProjects = sdkCat.net452SdkProjectsToBuild
        //    };

        //    if (postBldTsk.Execute())
        //    {
        //        string apiTag = postBldTsk.ApiTag;
        //        string apiTagPropsFile = postBldTsk.ApiTagPropsFile;

        //        Assert.NotEmpty(apiTag);
        //        Assert.True(File.Exists(apiTagPropsFile));

        //        Project proj;
        //        if (ProjectCollection.GlobalProjectCollection.GetLoadedProjects(apiTagPropsFile).Count != 0)
        //        {
        //            proj = ProjectCollection.GlobalProjectCollection.GetLoadedProjects(apiTagPropsFile).FirstOrDefault<Project>();
        //        }
        //        else
        //        {
        //            proj = new Project(apiTagPropsFile);
        //        }

        //        string apiTagProperty = proj.GetPropertyValue("AzureApiTag");
        //        Assert.Equal<string>(apiTag, apiTagProperty);
        //    }
        //}

        [Fact]
        public void VerifyPropsFileTest()
        {
            // if apitags is null or empty we should not consider that as an error
            Assert.True(new PostBuildTask().VerifyPropsFile(null, null));
        }

        [Fact]
        public void GetApiMapSplitPartialClass()
        {
            string asmPath = Assembly.GetExecutingAssembly().CodeBase;

            PostBuildTask postBld = new PostBuildTask()
            {
                AssemblyFullPath = asmPath,
                FQTypeName = "TestSdkInfo.SplitInfo.ResourceSDKInfo"
            };

            if (postBld.Execute())
            {
                string apiTag = postBld.ApiTag;
                Assert.NotEmpty(apiTag);

                Assert.Equal<string>("Resource_2017-03-30;Resource_2016-03-30;Resource_2017-01-31;", apiTag);
            }
        }

        [Fact]
        public void MissingSdkInfo()
        {
            string asmPath = Assembly.GetExecutingAssembly().CodeBase;

            PostBuildTask postBld = new PostBuildTask()
            {
                AssemblyFullPath = asmPath,
                FQTypeName = "SomeRandomTypeName"
            };

            if (postBld.Execute())
            {
                string apiTag = postBld.ApiTag;
                Assert.Empty(apiTag);
            }
        }

        [Fact]
        public void MissingApiInfo()
        {
            string asmPath = Assembly.GetExecutingAssembly().CodeBase;

            PostBuildTask postBld = new PostBuildTask()
            {
                AssemblyFullPath = asmPath,
                FQTypeName = "TestSdkInfo.MissingProperty.PropMissing"
            };

            if (postBld.Execute())
            {
                string apiTag = postBld.ApiTag;
                Assert.Empty(apiTag);
            }
        }

        [Fact(Skip = "Enable way to bypass based on target framework within the task")]
        public void SkipNetCoreTargets()
        {
            string scope = @"SDKs\Compute";
            SDKCategorizeProjectsTask sdkCat = CategorizeProjects(scope, 1);

            //string asmPath = Assembly.GetExecutingAssembly().CodeBase;


            PostBuildTask postBld = new PostBuildTask()
            {
                SdkProjects = sdkCat.net452SdkProjectsToBuild,
                ProjectTargetFramework = "NetStandard1.4"
            };


            if (postBld.Execute())
            {
                Assert.Empty(postBld.ApiTag);
            }
        }

        [Fact]
        public void GetApiMapforMultiApiProject()
        {
            string exeAsmDirPath = GetExeAsmDirPath();
            string testAsm = Path.Combine(exeAsmDirPath, "SdkInfoSample.dll");
            
            PostBuildTask postBld = new PostBuildTask()
            {
                AssemblyFullPath = testAsm
            };

            if (postBld.Execute())
            {
                string apiTag = postBld.ApiTag;
                Assert.NotEmpty(apiTag);
            }
        }

        
        private SDKCategorizeProjectsTask CategorizeProjects(string scope, int expectedProjectCount)
        {
            SDKCategorizeProjectsTask sdkCat = new SDKCategorizeProjectsTask();
            sdkCat.SourceRootDirPath = catProjTest.sourceRootDir;
            sdkCat.BuildScope = scope;
            sdkCat.IgnorePathTokens = Path.Combine(catProjTest.ignoreDir);

            if (sdkCat.Execute())
            {
                Assert.Equal<int>(expectedProjectCount, sdkCat.net452SdkProjectsToBuild.Count());
            }

            return sdkCat;
        }

        private string GetExeAsmDirPath()
        {
            string codeBasePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase);
            Uri codeBaseUri = new Uri(codeBasePath);

            return codeBaseUri.LocalPath;
        }

    }

    public class PostBuildFixture : IDisposable
    {


        public PostBuildFixture()
        {

        }

        public void Dispose()
        {
            
        }
    }
}
*/