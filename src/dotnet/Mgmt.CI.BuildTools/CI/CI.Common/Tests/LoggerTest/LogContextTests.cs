// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Tests.CI.Common.LoggerTests
{
    using global::Tests.CI.Common.Base;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Logger;
    using Xunit;

    /// <summary>
    /// Tests for testing logger under non msbuild scenarios
    /// By default we will trace all messages (info, error, exception)
    /// Logger under msbuild will be log to msbuild logger as well as trace listner
    /// </summary>
    public class LogContextTests : BuildTasksTestBase
    {
        [Fact]
        public void DefaultLogInfo()
        {
            NetSdkBuildTaskLogger sdkLogger = new NetSdkBuildTaskLogger();
            sdkLogger.LogInfo("Test Log message");
        }

        [Fact]
        public void DefaultLogFail()
        {
            NetSdkBuildTaskLogger sdkLogger = new NetSdkBuildTaskLogger();
            sdkLogger.LogInfo("Test Log message");
            sdkLogger.LogError("Some test error message");
        }
    }
}
