// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Tests.CI.Common.Base
{
    using Tests.CI.Common.ExtensionMethods;
    using MS.Az.Mgmt.CI.BuildTasks.Common;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Models;
    using MS.Az.Mgmt.CI.BuildTasks.Services;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Xunit;
    using Xunit.Abstractions;
    using Microsoft.Build.Locator;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Utilities;

    public class BuildTasksTestBase : IClassFixture<SharedXUnitTestFixture>
    {
        #region CONST
        const string TEST_ASSETS_DIR_NAME = "testAssets";
        const string SDK_FOR_NET_DIR_NAME = "sdkForNet";

        public const string NET_SDK_PUB_URL = @"http://github.com/azure/azure-sdk-for-net";
        public const string NET_SDK_PUB_URL_pr = @"https://github.com/azure/azure-sdk-for-net-pr";
        #endregion
        #region Fields
        ITestOutputHelper _testOutputHelper;
        string _testAssetDirPath;
        string _testAssertSdkForNetDirPath;
        #endregion

        #region Properties

        public bool IsDisposed { get; private set; }
        #region Dir Paths

        /// <summary>
        /// Any directory that will host testAssets directory
        /// TestAssets directory is meant to be root for multiple repository directory structre
        /// e.g. root directory that will host sdk-for-net directory structure or Fluet for.NET directory structure
        /// </summary>
        public string TestAssetsDirPath
        {
            get
            {
                if(string.IsNullOrWhiteSpace(_testAssetDirPath))
                {
                    _testAssetDirPath = Environment.GetEnvironmentVariable("testAssetDir");

                    if(string.IsNullOrEmpty(_testAssetDirPath))
                    {
                        _testAssetDirPath = FileSys.TraverseUptoRootWithDirToken(TEST_ASSETS_DIR_NAME);
                        if (Directory.Exists(_testAssetDirPath))
                        {
                            _testAssetDirPath = Path.Combine(_testAssetDirPath, TEST_ASSETS_DIR_NAME);
                        }
                    }
                }

                return _testAssetDirPath;
            }
        }

        /// <summary>
        /// SDK-FOR-NET root directory
        /// e.g. root\sdk\Compute
        /// e.g. root\src\SDKs\Compute
        /// </summary>
        public string TestAssetSdkForNetDirPath
        {
            get
            {
                if(string.IsNullOrWhiteSpace(_testAssertSdkForNetDirPath))
                {
                    _testAssertSdkForNetDirPath = Path.Combine(TestAssetsDirPath, SDK_FOR_NET_DIR_NAME);
                    Check.DirectoryExists(_testAssertSdkForNetDirPath);
                }

                return _testAssertSdkForNetDirPath;
            }
        }

        public FileSystemUtility FileSys { get; set; }

        public string RepoRootDirPath { get; set; }

        #endregion

        #region VS Ide
        /// <summary>
        /// Detect if running under VS IDE context
        /// Note:
        /// Hosting process can be truned off from project properites, 
        /// in that case this property will always return false
        /// </summary>
        bool IsInVisualStudio
        {
            get
            {
                bool inIDE, isVsHost, isDebuggerAttached;
                inIDE = isVsHost = isDebuggerAttached = false;

                string[] args = System.Environment.GetCommandLineArgs();
                if (args != null && args.Length > 0)
                {
                    string prgName = args[0].ToUpper();
                    isVsHost = prgName.EndsWith("VSHOST.EXE");
                }

                //Check if Debugger is attached
                if(System.Diagnostics.Debugger.IsAttached)
                {
                    isDebuggerAttached = true;
                }

                inIDE = (isVsHost || isDebuggerAttached);

                return inIDE;
            }
        }

        #endregion

        #region xUnit properties
        protected ITestOutputHelper DebugTestOutput
        {
            get
            {
                return _testOutputHelper;
            }
            private set
            {
                _testOutputHelper = value;
                GlobalTestInfo.GlobalTestOutput = _testOutputHelper;
            }
        }
        protected SharedXUnitTestFixture TestSharedFixture { get; private set; }

        #endregion

        #endregion

        #region Constructor/Cleanup

        #region Constructors/Init
        public BuildTasksTestBase()
        {
            Init();
        }

        public BuildTasksTestBase(ITestOutputHelper outputHelper, SharedXUnitTestFixture testFixture) : this()
        {
            TestSharedFixture = testFixture;
            DebugTestOutput = outputHelper;
            #region Available interfaces
            //IDataDiscoverer
            //ITestCaseOrderer
            //ITestCollectionOrderer
            //ITestFrameworkTypeDiscoverer
            //ITraitDiscoverer
            //IXunitTestCaseDiscoverer
            //IXunitTestCollectionFactory
            #endregion
        }

        void Init()
        {
            if(!MSBuildLocator.IsRegistered)
            {
                VisualStudioInstance vsInst = MSBuildLocator.RegisterDefaults();
            }
            
            FileSys = new FileSystemUtility();
        }

        #endregion

        #endregion

        #region protected functions

        protected void StartEmulatingWindowsPlatform()
        {
            Environment.SetEnvironmentVariable("emulateWindowsEnv", "true");
        }

        protected void StartEmulatingNonWindowsPlatform()
        {
            Environment.SetEnvironmentVariable("emulateNonWindowsEnv", "true");
        }

        #endregion

        #region Cleanup
        ~BuildTasksTestBase()
        {
            CleanUp();
        }

        /// <summary>
        /// Detects if the current repo
        /// </summary>
        private void CleanUp()
        {
            //if (!IsDisposed)
            //{
            //    if (IsInVisualStudio == false)
            //    {
            //        if (!this.GitRepoClient.IsDisposed)
            //        {
            //            if (this.GitRepoClient.IsStatusDirty)
            //            {
            //                //GitRepoClient.RemoveUnTrackedFiles();
            //            }
            //        }
            //    }

            //    foreach (KeyValuePair<string, GitRepositoryClient> kv in GitRepoClient.SubModuleGitClients)
            //    {
            //        GitRepositoryClient subModGitClient = kv.Value;
            //        if (!subModGitClient.IsDisposed)
            //        {
            //            if (subModGitClient.IsStatusDirty)
            //            {
            //                subModGitClient.RemoveUnTrackedFiles();
            //            }
            //        }
            //    }

            //    this.GitRepoClient.Dispose();
            }
        }

    /// <summary>
    /// Dispose method to clean up resources        
    /// </summary>
    //public virtual void Dispose()
    //{
    //    CleanUp();
    //    if(TestSharedFixture != null)
    //    {
    //        TestSharedFixture.Dispose();
    //    }
    //    IsDisposed = true;
    //}
    #endregion
}
