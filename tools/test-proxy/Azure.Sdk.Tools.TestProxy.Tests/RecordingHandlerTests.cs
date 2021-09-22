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
            var httpContext = new DefaultHttpContext();
            var pathToRecording = Path.Combine(currentPath, "Test.RecordEntries/oauth_request.json");

            var recordingHandler = new RecordingHandler(tmpPath);

            await recordingHandler.StartPlayback(pathToRecording, httpContext.Response);

            var playbackSession = recordingHandler.PlaybackSessions.First();
            var entry = playbackSession.Value.Session.Entries.First();

            Assert.Equal("https://login.microsoftonline.com/12345678-1234-1234-1234-123456789012/oauth2/v2.0/token", entry.RequestUri);
        }

        [Fact]
        public async void TestLoadOfRelativeRecording()
        {
            var currentPath = Directory.GetCurrentDirectory();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = "Test.RecordEntries/oauth_request.json";
            var recordingHandler = new RecordingHandler(currentPath);

            await recordingHandler.StartPlayback(pathToRecording, httpContext.Response);

            var playbackSession = recordingHandler.PlaybackSessions.First();
            var entry = playbackSession.Value.Session.Entries.First();

            Assert.Equal("https://login.microsoftonline.com/12345678-1234-1234-1234-123456789012/oauth2/v2.0/token", entry.RequestUri);
        }

        [Fact]
        public void TestWriteAbsoluteRecording()
        {
            var tmpPath = Path.GetTempPath();
            var currentPath = Directory.GetCurrentDirectory();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = Path.Combine(currentPath, "recordings/oauth_request_new.json");
            var recordingHandler = new RecordingHandler(tmpPath);

            recordingHandler.StartRecording(pathToRecording, httpContext.Response);
            var sessionId = httpContext.Response.Headers["x-recording-id"].ToString();
            recordingHandler.StopRecording(sessionId);

            try
            {
                Assert.True(File.Exists(pathToRecording));
            }
            finally
            {
                File.Delete(pathToRecording);
            }
        }

        [Fact]
        public void TestWriteRelativeRecording()
        {
            var currentPath = Directory.GetCurrentDirectory();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = "recordings/oauth_request_new";
            var recordingHandler = new RecordingHandler(currentPath);
            var fullPathToRecording = Path.Combine(currentPath, pathToRecording) + ".json";

            recordingHandler.StartRecording(pathToRecording, httpContext.Response);
            var sessionId = httpContext.Response.Headers["x-recording-id"].ToString();
            recordingHandler.StopRecording(sessionId);


            try
            {
                Assert.True(File.Exists(fullPathToRecording));
            }
            finally
            {
                File.Delete(fullPathToRecording);
            }
        }

        [Fact]
        public async void TestLoadNonexistentAbsoluteRecording()
        {
            var tmpPath = Path.GetTempPath();
            var currentPath = Directory.GetCurrentDirectory();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = Path.Combine(currentPath, "Test.RecordEntries/oauth_request_wrong.json");

            var recordingHandler = new RecordingHandler(tmpPath);

            await Assert.ThrowsAsync<FileNotFoundException>(
               async () => await recordingHandler.StartPlayback(pathToRecording, httpContext.Response)
            );
        }

        [Fact]
        public async void TestLoadNonexistentRelativeRecording()
        {
            var currentPath = Directory.GetCurrentDirectory();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = "Test.RecordEntries/oauth_request_wrong.json";

            var recordingHandler = new RecordingHandler(currentPath);

            await Assert.ThrowsAsync<FileNotFoundException>(
               async () => await recordingHandler.StartPlayback(pathToRecording, httpContext.Response)
            );
        }
    }
}
