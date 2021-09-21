using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Matchers;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using Azure.Sdk.Tools.TestProxy.Transforms;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class RecordingHandlerTests
    {
        private void _checkDefaultExtensions(RecordingHandler handlerForTest)
        {
            Assert.Equal(2, handlerForTest.Transforms.Count);
            Assert.IsType<StorageRequestIdTransform>(handlerForTest.Transforms[0]);
            Assert.IsType<ClientIdTransform>(handlerForTest.Transforms[1]);

            Assert.NotNull(handlerForTest.Matcher);
            Assert.IsType<RecordMatcher>(handlerForTest.Matcher);

            Assert.Single(handlerForTest.Sanitizers);
            Assert.IsType<RecordedTestSanitizer>(handlerForTest.Sanitizers[0]);
        }

        [Fact]
        public void TestResetAfterAddition()
        {
            // arrange
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());

            // act
            testRecordingHandler.Sanitizers.Add(new BodyRegexSanitizer("sanitized", ".*"));
            testRecordingHandler.Matcher = new BodilessMatcher();
            testRecordingHandler.Transforms.Add(new ApiVersionTransform());
            testRecordingHandler.SetDefaultExtensions();

            //assert
            _checkDefaultExtensions(testRecordingHandler);
        }

        [Fact]
        public void TestResetAfterRemoval()
        {
            // arrange
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());

            // act
            testRecordingHandler.Sanitizers.Clear();
            testRecordingHandler.Matcher = null;
            testRecordingHandler.Transforms.Clear();
            testRecordingHandler.SetDefaultExtensions();

            //assert
            _checkDefaultExtensions(testRecordingHandler);
        }

        [Fact]
        public async void TestInMemoryPurgesSucessfully()
        {
            var recordingHandler = TestHelpers.LoadRecordSessionIntoInMemoryStore("Test.RecordEntries/post_delete_get_content.json");
            var httpContext = new DefaultHttpContext();
            var key = recordingHandler.InMemorySessions.Keys.First();

            await recordingHandler.StartPlayback(key, httpContext.Response, Common.RecordingType.InMemory);
            var playbackSession = httpContext.Response.Headers["x-recording-id"];
            recordingHandler.StopPlayback(playbackSession, true);

            Assert.True(0 == recordingHandler.InMemorySessions.Count);
        }

        [Fact]
        public async void TestInMemoryDoesntPurgeErroneously()
        {
            var recordingHandler = TestHelpers.LoadRecordSessionIntoInMemoryStore("Test.RecordEntries/post_delete_get_content.json");
            var httpContext = new DefaultHttpContext();
            var key = recordingHandler.InMemorySessions.Keys.First();

            await recordingHandler.StartPlayback(key, httpContext.Response, Common.RecordingType.InMemory);
            var playbackSession = httpContext.Response.Headers["x-recording-id"];
            recordingHandler.StopPlayback(playbackSession, false);

            Assert.True(1 == recordingHandler.InMemorySessions.Count);
        }

        [Fact]
        public async void TestLoadOfAbsoluteRecording()
        {
            var tmpPath = Path.GetTempPath();
            var currentPath = Directory.GetCurrentDirectory();
            var pathToRecording = Path.Combine(currentPath, "Test.RecordEntries/oauth_request.json");
            //  we intentionally change the context to somewhere that we can't see the recordings
            var recordingHandler = new RecordingHandler(tmpPath);
            var guid = Guid.NewGuid().ToString();

            // given the changed storage context, can we load an absolute recording?
            TestHelpers.LoadPlaybackSessionIntoHandler(pathToRecording, guid, recordingHandler);

            // ensure that the first item in the session
            var entry = recordingHandler.PlaybackSessions[guid].Session.Entries.First();

            // if we loaded the entry appropriately, we should have the stuff we xpect
            Assert.Equal("https://login.microsoftonline.com/12345678-1234-1234-1234-123456789012/oauth2/v2.0/token", entry.RequestUri);
        }

        [Fact]
        public async void TestLoadOfRelativeRecording()
        {
            var tmpPath = Path.GetTempPath();
            var currentPath = Directory.GetCurrentDirectory();
            var pathToRecording = Path.Combine(currentPath, "Test.RecordEntries/oauth_request.json");
            //  we intentionally change the context to somewhere that we can't see the recordings
            var recordingHandler = new RecordingHandler(tmpPath);
            var guid = Guid.NewGuid().ToString();

            // given the changed storage context, can we load an absolute recording?
            TestHelpers.LoadPlaybackSessionIntoHandler(pathToRecording, guid, recordingHandler);

            // ensure that the first item in the session
            var entry = recordingHandler.PlaybackSessions[guid].Session.Entries.First();

            // if we loaded the entry appropriately, we should have the stuff we xpect
            Assert.Equal("https://login.microsoftonline.com/12345678-1234-1234-1234-123456789012/oauth2/v2.0/token", entry.RequestUri);

        }

        [Fact]
        public async void TestWriteAbsoluteRecording()
        {
            // can we write an absolute recording with storage context?
        }

        [Fact]
        public async void TestWriteRelativeRecording()
        {
            // can we write a relative recording with storage context?
        }

        [Fact]
        public async void TestLoadNonexistentAbsoluteRecording()
        {
            // loading something that doesn't exist breaks
        }

        [Fact]
        public async void TestLoadNonexistentRelativeRecording()
        {
            // loading something that doesn't exist breaks
        }
    }
}
