// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Tests.CI.BuildTasks.ModelTests
{
    using MS.Az.Mgmt.CI.BuildTasks.Models;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Tests.CI.Common.Base;
    using Xunit;
    using Xunit.Abstractions;

    public class TargetFxModelTests : BuildTasksTestBase
    {
        internal string rootDir = string.Empty;
        internal string sourceRootDir = string.Empty;
        readonly ITestOutputHelper OutputTrace;

        public TargetFxModelTests(ITestOutputHelper output)
        {
            rootDir = this.TestAssetsDirPath;
            rootDir = Path.Combine(rootDir, "sdkForNet");
            sourceRootDir = rootDir;

            this.OutputTrace = output;
        }

        [Fact]
        public void WindowsPlatformTargetFx()
        {
            string projectFile = Path.Combine(rootDir, "src", "SDKs", "Dns", "Management.Dns", "Microsoft.Azure.Management.Dns.csproj");
            string baselineFxMoniker = "net452;net461;netstandard1.4;netstandard2.0";
            string targetFxMoniker = "net452;net461;netstandard1.4;netstandard2.0";
            this.StartEmulatingWindowsPlatform();

            TargetFx tfx = new TargetFx(projectFile, targetFxMoniker, baselineFxMoniker, SdkProjectType.Sdk);
            Assert.Equal("net452;net461;netstandard1.4;netstandard2.0".ToLower(), tfx.EnvironmentSpecificTargetFxMonikerString.ToLower());
        }

        [Fact]
        public void NonWindowsPlatformTargetFx()
        {
            string projectFile = Path.Combine(rootDir, "src", "SDKs", "Dns", "Management.Dns", "Microsoft.Azure.Management.Dns.csproj");
            string baselineFxMoniker = "net452;net461;netstandard1.4;netstandard2.0";
            string targetFxMoniker = "net452;net461;netstandard1.4;netstandard2.0";
            this.StartEmulatingNonWindowsPlatform();

            TargetFx tfx = new TargetFx(projectFile, targetFxMoniker, baselineFxMoniker, SdkProjectType.Sdk);
            Assert.Equal("netstandard1.4;netstandard2.0".ToLower(), tfx.EnvironmentSpecificTargetFxMonikerString.ToLower());
        }
    }
}
