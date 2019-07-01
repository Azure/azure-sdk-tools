// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.


namespace Tests.CI.BuildTasks.TasksTests
{
    using MS.Az.Mgmt.CI.BuildTasks.BuildTasks.PreBuild;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Utilities;
    using MS.Az.Mgmt.CI.BuildTasks.Models;
    using MS.Az.Mgmt.CI.BuildTasks.Tasks.PreBuild;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Tests.CI.Common.Base;
    using Xunit;
    using Xunit.Abstractions;

    public class UpdateNetSdkApiTagInfoTaskTests : BuildTasksTestBase
    {

        #region const

        #endregion

        #region fields
        internal string rootDir = string.Empty;
        internal string sourceRootDir = string.Empty;
        readonly ITestOutputHelper OutputTrace;
        FileSystemUtility _fileSysUtil;
        #endregion

        #region Properties
        FileSystemUtility FileSysUtil
        {
            get
            {
                if(_fileSysUtil == null)
                {
                    _fileSysUtil = new FileSystemUtility();
                }

                return _fileSysUtil;
            }
        }
        #endregion

        #region Constructor
        public UpdateNetSdkApiTagInfoTaskTests(ITestOutputHelper output)
        {
            //create an env. variable 'testAssetdir' and point to a directory that will host multiple repos
            // e.g. sdkfornet directory structure as well as Fluent directory structure
            // basically test asset directory will be the root for all other repos that can be used for testing directory structure
            // inside test assert directory will have a directory for sdkfornet repo (for these tests)

            // also make sure you execute any target in that repo because that would force the nuget package to
            // download and get all the files necessary for the tasks to execute successfully
            rootDir = this.TestAssetsDirPath;
            rootDir = Path.Combine(rootDir, "sdkForNet");
            sourceRootDir = rootDir;

            this.OutputTrace = output;
        }
        #endregion

        #region Tests
        [Fact]
        public void UpdateSdkInfoScopedProjects()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = "Compute";

            if(cproj.Execute())
            {
                Assert.Single(cproj.SDK_Projects);
            }

            UpdateNetSdkApiTagInfoTask updateSdkInfo = new UpdateNetSdkApiTagInfoTask();
            updateSdkInfo.SdkProjectFilePaths = cproj.SDK_Projects;

            //TODO: Add a verification step to verify if the azPropFile was updated
            if (updateSdkInfo.Execute())
            {
                Assert.True(true);
            }
        }

        [Fact]
        public void CreatePropfile()
        {
            CategorizeSDKProjectsTask cproj = new CategorizeSDKProjectsTask(rootDir);
            cproj.BuildScope = "Compute";

            if (cproj.Execute())
            {
                Assert.Single(cproj.SDK_Projects);
            }

            DeleteAzPropFiles(cproj.SDK_Projects);

            UpdateNetSdkApiTagInfoTask updateSdkInfo = new UpdateNetSdkApiTagInfoTask();
            updateSdkInfo.SdkProjectFilePaths = cproj.SDK_Projects;

            //TODO: Add a verification step to verify if the azPropFile was created
            if (updateSdkInfo.Execute())
            {
                Assert.True(true);
            }
        }
        #endregion

        #region private functions
        void DeleteAzPropFiles(SDKMSBTaskItem[] sdkProjFilePaths)
        {
            string azPropFileName = "AzSdk.RP.props";

            foreach (SDKMSBTaskItem item in sdkProjFilePaths)
            {
                string projDirPath = Path.GetDirectoryName(item.ItemSpec);
                string azPropDirPath = FileSysUtil.TraverseUptoRootWithFileToken(azPropFileName, projDirPath);

                if(Directory.Exists(azPropDirPath))
                {
                    File.Delete(Path.Combine(azPropDirPath, azPropFileName));
                }
            }
        }
        #endregion
    }
}
