using System;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Store;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class GitProcessHandlerTests
    {
        public GitProcessHandlerTests()
        {
            var loggerFactory = new LoggerFactory();
            DebugLogger.ConfigureLogger(loggerFactory);
        }

        [Theory]
        // 2.37.0 is the min version of git required by the TestProxy
        // Windows git version strings
        [InlineData("git version 2.37.2.windows.2", false)]
        [InlineData("git version 2.36.0.windows.2", true)]
        // Mac git version strings
        [InlineData("git version 2.37.1 (Apple Git-133)", false)]
        [InlineData("git version 1.37.1 (Apple Git-133)", true)]
        // Linux git version string
        [InlineData("git version 2.37.0", false)]
        // Check the actual version on the machine
        [InlineData(null, false)]
        public void CheckGitVersionTests(string gitTestVersionString, bool shouldThrow)
        {
            GitProcessHandler gph = new GitProcessHandler();
            if (shouldThrow)
            {
                Action action = () => gph.VerifyGitMinVersion(gitTestVersionString);
                Exception ex = Assert.Throws<GitVersionException>(action);
                Assert.StartsWith($"{gitTestVersionString} is less than the minimum supported Git version", ex.Message);
            }
            else
            {
                try
                {
                    gph.VerifyGitMinVersion(gitTestVersionString);
                    Assert.True(true);
                }
                catch (Exception ex)
                {
                    Assert.True(false, $"Expected no exception, but received exception of type {ex.GetType()}, message:{ex.Message}");
                }
            }
        }
    }
}
