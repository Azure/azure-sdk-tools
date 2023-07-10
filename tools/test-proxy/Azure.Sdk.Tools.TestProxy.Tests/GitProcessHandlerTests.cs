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

        const string longTimeOutError = @"
            Cloning into '.'...
            fatal: unable to access 'https://github.com/Azure/azure-sdk-assets/': Failed to connect to github.com port 443: Operation timed out


            at Azure.Sdk.Tools.TestProxy.Store.GitStore.InitializeAssetsRepo(GitAssetsConfiguration config, Boolean forceInit) in /mnt/vss/_work/1/s/tools/test-proxy/Azure.Sdk.Tools.TestProxy/Store/GitStore.cs:line 456
            at Azure.Sdk.Tools.TestProxy.Store.GitStore.Restore(String pathToAssetsJson) in /mnt/vss/_work/1/s/tools/test-proxy/Azure.Sdk.Tools.TestProxy/Store/GitStore.cs:line 152
            at Azure.Sdk.Tools.TestProxy.Startup.Run(Object commandObj) in /mnt/vss/_work/1/s/tools/test-proxy/Azure.Sdk.Tools.TestProxy/Startup.cs:line 154
            at CommandLine.ParserResultExtensions.WithParsedAsync[T](ParserResult`1 result, Func`2 action)
            at Azure.Sdk.Tools.TestProxy.Startup.Main(String[] args) in /mnt/vss/_work/1/s/tools/test-proxy/Azure.Sdk.Tools.TestProxy/Startup.cs:line 67
            at Azure.Sdk.Tools.TestProxy.Startup.<Main>(String[] args)";

        const string tooManyRequestsError = @"fatal: unable to access 'https://github.com/Azure/azure-sdk-assets/': The requested URL returned error: 429
        fatal: could not fetch 26bfd3d8ada54a78573cb64f6fa79c8d4a300c8c from promisor remote

             at Azure.Sdk.Tools.TestProxy.Store.GitStore.CheckoutRepoAtConfig(GitAssetsConfiguration config) in /mnt/vss/_work/1/s/tools/test-proxy/Azure.Sdk.Tools.TestProxy/Store/GitStore.cs:line 330
             at Azure.Sdk.Tools.TestProxy.Store.GitStore.InitializeAssetsRepo(GitAssetsConfiguration config, Boolean forceInit) in /mnt/vss/_work/1/s/tools/test-proxy/Azure.Sdk.Tools.TestProxy/Store/GitStore.cs:line 459
             at Azure.Sdk.Tools.TestProxy.Store.GitStore.Restore(String pathToAssetsJson) in /mnt/vss/_work/1/s/tools/test-proxy/Azure.Sdk.Tools.TestProxy/Store/GitStore.cs:line 152
             at Azure.Sdk.Tools.TestProxy.RecordingHandler.RestoreAssetsJson(String assetsJson, Boolean forceCheckout) in /mnt/vss/_work/1/s/tools/test-proxy/Azure.Sdk.Tools.TestProxy/RecordingHandler.cs:line 164
             at Azure.Sdk.Tools.TestProxy.RecordingHandler.StartPlaybackAsync(String sessionId, HttpResponse outgoingResponse, RecordingType mode, String assetsPath) in /mnt/vss/_work/1/s/tools/test-proxy/Azure.Sdk.Tools.TestProxy/RecordingHandler.cs:line 373
             at Azure.Sdk.Tools.TestProxy.Playback.Start() in /mnt/vss/_work/1/s/tools/test-proxy/Azure.Sdk.Tools.TestProxy/Playback.cs:line 46
             at lambda_method37(Closure , Object )";

        const string longTimeOutWithValue = @"fatal: unable to access 'https://github.com/Azure/azure-sdk-assets/': Failed to connect to github.com port 443 after 21019 ms: Couldn't connect to server
      fatal: could not fetch 87d05b71fcffcf3c7fa8e611bda09b505ff586a4 from promisor remote


         at Azure.Sdk.Tools.TestProxy.Store.GitStore.CheckoutRepoAtConfig(GitAssetsConfiguration config) in /mnt/vss/_work/1/s/tools/test-proxy/Azure.Sdk.Tools.TestProxy/Store/GitStore.cs:line 330
         at Azure.Sdk.Tools.TestProxy.Store.GitStore.InitializeAssetsRepo(GitAssetsConfiguration config, Boolean forceInit) in /mnt/vss/_work/1/s/tools/test-proxy/Azure.Sdk.Tools.TestProxy/Store/GitStore.cs:line 459
         at Azure.Sdk.Tools.TestProxy.Store.GitStore.Restore(String pathToAssetsJson) in /mnt/vss/_work/1/s/tools/test-proxy/Azure.Sdk.Tools.TestProxy/Store/GitStore.cs:line 152
         at Azure.Sdk.Tools.TestProxy.RecordingHandler.RestoreAssetsJson(String assetsJson, Boolean forceCheckout) in /mnt/vss/_work/1/s/tools/test-proxy/Azure.Sdk.Tools.TestProxy/RecordingHandler.cs:line 164
         at Azure.Sdk.Tools.TestProxy.RecordingHandler.StartPlaybackAsync(String sessionId, HttpResponse outgoingResponse, RecordingType mode, String assetsPath) in /mnt/vss/_work/1/s/tools/test-proxy/Azure.Sdk.Tools.TestProxy/RecordingHandler.cs:line 373
         at Azure.Sdk.Tools.TestProxy.Playback.Start() in /mnt/vss/_work/1/s/tools/test-proxy/Azure.Sdk.Tools.TestProxy/Playback.cs:line 46
         at lambda_method37(Closure , Object )";

        [Theory]
        [InlineData("", longTimeOutError, 1, true)]
        [InlineData("", tooManyRequestsError, 1, true)]
        [InlineData("", longTimeOutWithValue, 1, true)]
        [InlineData("", "fatal: unable to access 'https://github.com/Azure/azure-sdk-assets/': Failed to connect to github.com port 443 after 1 ms: Couldn't connect to server", 1, true)]
        [InlineData("", "fatal: unable to access 'https://github.com/Azure/azure-sdk-assets/': Failed to connect to github.com port 443 after 0 ms: Couldn't connect to server", 1, true)]
        [InlineData("", "fatal: unable to access 'https://github.com/Azure/azure-sdk-assets/': Failed to connect to github.com port 443 after ms: Couldn't connect to server", 1, false)]
        [InlineData("", "", 0, false)]
        public void VerifyGitExceptionParser(string inputStdOut, string inputStdErr, int exitCode, bool expectedResult)
        {
            var processHandler = new GitProcessHandler();
            var commandResult = new CommandResult();
            commandResult.StdOut = inputStdOut;
            commandResult.StdErr = inputStdErr;
            commandResult.ExitCode = exitCode;

            var checkResult = processHandler.IsRetriableGitError(commandResult);

            Assert.Equal(expectedResult, checkResult);
        }

        [Theory]
        // 2.37.0 is the min version of git required by the TestProxy
        // Windows git version strings
        [InlineData("git version 2.37.2.windows.2", false)]
        [InlineData("git version 2.24.0.windows.2", true)]
        // Mac git version strings
        [InlineData("git version 2.37.1 (Apple Git-133)", false)]
        [InlineData("git version 1.37.1 (Apple Git-133)", true)]
        // Linux git version string
        [InlineData("git version 2.25.0", false)]
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
