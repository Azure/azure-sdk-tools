using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Sdk.tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Store;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    /// <summary>
    /// An assets store that reports a directory of the test's choosing, so that
    /// <see cref="RecordingHandler.GetRecordingPath"/> can be exercised on the
    /// assets.json branch without a git backed store.
    /// </summary>
    public class FixedPathAssetsStore : IAssetsStore
    {
        private readonly string _contextDirectory;

        public FixedPathAssetsStore(string contextDirectory) => _contextDirectory = contextDirectory;

        public Task<int> Push(string pathToAssetsJson, bool ignoreSecretProtection = false) => Task.FromResult(0);

        public Task<string> Restore(string pathToAssetsJson) => Task.FromResult(_contextDirectory);

        public Task Reset(string pathToAssetsJson) => Task.CompletedTask;

        public AssetsConfiguration ParseConfigurationFile(string pathToAssetsJson) => new AssetsConfiguration();

        public Task<NormalizedString> GetPath(string pathToAssetsJson) => Task.FromResult(new NormalizedString(_contextDirectory));

        public void SetStoreExceptionMode(bool throwOnException) { }
    }

    public class RecordingPathContainmentTests : IDisposable
    {
        private readonly string _root;
        private readonly string _assetsContext;
        private readonly RecordingHandler _handler;

        public RecordingPathContainmentTests()
        {
            _root = Path.Join(Path.GetTempPath(), "test-proxy-containment-" + Guid.NewGuid().ToString("N"));
            _assetsContext = Path.Join(_root, "context");
            Directory.CreateDirectory(_assetsContext);

            _handler = new RecordingHandler(_root, store: new FixedPathAssetsStore(_assetsContext));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        // Rejecting a fully qualified value already says the recording belongs
        // under the assets context. These are the values that say the same thing
        // without being fully qualified.
        [Theory]
        [InlineData("../escape.json")]
        [InlineData("../../escape.json")]
        [InlineData("nested/../../escape.json")]
        [InlineData("./../escape.json")]
        [InlineData("../context-sibling/escape.json")]
        public async Task GetRecordingPathRejectsValuesThatLeaveTheAssetsDirectory(string file)
        {
            var exception = await Assert.ThrowsAsync<HttpException>(
                async () => await _handler.GetRecordingPath(file, "assets.json"));

            Assert.Contains("resolves outside the assets directory", exception.Message);
        }

        [Fact]
        public async Task GetRecordingPathStillRejectsAFullyQualifiedValue()
        {
            var fullyQualified = Path.Join(_root, "escape.json");

            var exception = await Assert.ThrowsAsync<HttpException>(
                async () => await _handler.GetRecordingPath(fullyQualified, "assets.json"));

            Assert.Contains("fully qualified", exception.Message);
        }

        [Theory]
        [InlineData("recording.json")]
        [InlineData("recordings/recording.json")]
        [InlineData("recordings/nested/deep/recording.json")]
        [InlineData("./recordings/recording.json")]
        [InlineData("recordings/inner/../recording.json")]
        public async Task GetRecordingPathAcceptsValuesInsideTheAssetsDirectory(string file)
        {
            var resolved = await _handler.GetRecordingPath(file, "assets.json");

            Assert.StartsWith(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(_assetsContext)) + Path.DirectorySeparatorChar,
                Path.GetFullPath(resolved),
                StringComparison.Ordinal);
            Assert.EndsWith(".json", resolved, StringComparison.Ordinal);
        }

        [Fact]
        public async Task GetRecordingPathAppendsJsonInsideTheAssetsDirectory()
        {
            var resolved = await _handler.GetRecordingPath("recordings/recording", "assets.json");

            Assert.EndsWith(".json", resolved, StringComparison.Ordinal);
        }

        // A directory whose name merely begins with the context directory's name
        // is not inside it, so a plain prefix comparison would be wrong.
        [Fact]
        public async Task GetRecordingPathRejectsASiblingSharingANamePrefix()
        {
            var exception = await Assert.ThrowsAsync<HttpException>(
                async () => await _handler.GetRecordingPath("../context-extra/escape.json", "assets.json"));

            Assert.Contains("resolves outside the assets directory", exception.Message);
        }

        // Without an assets.json the tool is documented as writing wherever the
        // caller points it, so that behaviour is deliberately left alone.
        [Fact]
        public async Task GetRecordingPathWithoutAssetsJsonIsUnchanged()
        {
            var relative = await _handler.GetRecordingPath("recordings/recording.json");
            Assert.Equal(Path.Join(_root, "recordings/recording.json"), relative);

            var fullyQualified = Path.Join(_root, "elsewhere", "recording.json");
            Assert.Equal(fullyQualified, await _handler.GetRecordingPath(fullyQualified));
        }

        [Fact]
        public async Task GetRecordingPathStillRejectsAnEmptyValue()
        {
            await Assert.ThrowsAsync<HttpException>(async () => await _handler.GetRecordingPath("", "assets.json"));
            await Assert.ThrowsAsync<HttpException>(async () => await _handler.GetRecordingPath("   "));
        }
    }
}
