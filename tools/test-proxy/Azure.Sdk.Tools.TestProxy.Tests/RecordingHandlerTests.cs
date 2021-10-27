using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Matchers;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using Azure.Sdk.Tools.TestProxy.Transforms;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class RecordingHandlerTests
    {

        [Flags]
        enum CheckSkips
        {
            None = 0,
            IncludeTransforms = 1,
            IncludeSanitizers = 2,
            IncludeMatcher = 4,

            Default = IncludeTransforms | IncludeSanitizers | IncludeMatcher
        }

        private void _checkDefaultExtensions(RecordingHandler handlerForTest, CheckSkips skipsToCheck = CheckSkips.Default)
        {
            if (skipsToCheck.HasFlag(CheckSkips.IncludeTransforms))
            {
                Assert.Equal(2, handlerForTest.Transforms.Count);
                Assert.IsType<StorageRequestIdTransform>(handlerForTest.Transforms[0]);
                Assert.IsType<ClientIdTransform>(handlerForTest.Transforms[1]);
            }

            if (skipsToCheck.HasFlag(CheckSkips.IncludeMatcher))
            {
                Assert.NotNull(handlerForTest.Matcher);
                Assert.IsType<RecordMatcher>(handlerForTest.Matcher);
            }

            if (skipsToCheck.HasFlag(CheckSkips.IncludeSanitizers))
            {
                Assert.Single(handlerForTest.Sanitizers);
                Assert.IsType<RecordedTestSanitizer>(handlerForTest.Sanitizers[0]);
            }
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
        public void TestResetTargetsRecordingOnly()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            testRecordingHandler.StartRecording("recordingings/cool.json", httpContext.Response);
            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();


            testRecordingHandler.Sanitizers.Clear();
            testRecordingHandler.Sanitizers.Add(new BodyRegexSanitizer("sanitized", ".*"));
            testRecordingHandler.AddRecordSanitizer(recordingId, new GeneralRegexSanitizer("sanitized", ".*"));
            testRecordingHandler.SetDefaultExtensions(recordingId);
            var session = testRecordingHandler.RecordingSessions.First().Value;

            // session sanitizer is still set to a single one
            Assert.Single(testRecordingHandler.Sanitizers);
            Assert.IsType<BodyRegexSanitizer>(testRecordingHandler.Sanitizers[0]);
            _checkDefaultExtensions(testRecordingHandler, CheckSkips.IncludeMatcher | CheckSkips.IncludeTransforms);
            Assert.Empty(session.ModifiableSession.AdditionalSanitizers);
            Assert.Empty(session.ModifiableSession.AdditionalTransforms);
            Assert.Null(session.ModifiableSession.CustomMatcher);
        }

        [Fact]
        public void TestResetTargetsSessionOnly()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            testRecordingHandler.StartRecording("recordingings/cool.json", httpContext.Response);
            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();

            testRecordingHandler.Sanitizers.Clear();
            testRecordingHandler.Sanitizers.Add(new BodyRegexSanitizer("sanitized", ".*"));
            testRecordingHandler.Transforms.Clear();
            testRecordingHandler.AddRecordSanitizer(recordingId, new GeneralRegexSanitizer("sanitized", ".*"));
            testRecordingHandler.SetDefaultExtensions();
            var session = testRecordingHandler.RecordingSessions.First().Value;

            Assert.Single(session.ModifiableSession.AdditionalSanitizers);
            Assert.IsType<GeneralRegexSanitizer>(session.ModifiableSession.AdditionalSanitizers[0]);

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

        [Fact]
        public void TestStopRecordingWithVariables()
        {
            var tmpPath = Path.GetTempPath();
            var startHttpContext = new DefaultHttpContext();
            var pathToRecording = "recordings/oauth_request_new.json";
            var recordingHandler = new RecordingHandler(tmpPath);
            var dict = new Dictionary<string, string>{
                { "key1","valueabc123" },
                { "key2", "value123abc" }
            };
            var endHttpContext = new DefaultHttpContext();

            recordingHandler.StartRecording(pathToRecording, startHttpContext.Response);
            var sessionId = startHttpContext.Response.Headers["x-recording-id"].ToString();
            
            recordingHandler.StopRecording(sessionId, variables: new SortedDictionary<string, string>(dict));
            var storedVariables = TestHelpers.LoadRecordSession(Path.Combine(tmpPath, pathToRecording)).Session.Variables;

            Assert.Equal(dict.Count, storedVariables.Count);

            foreach(var kvp in dict)
            {
                Assert.Equal(kvp.Value, storedVariables[kvp.Key]);
            }
        }

        [Fact]
        public void TestStopRecordingWithoutVariables()
        {
            var tmpPath = Path.GetTempPath();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = "recordings/oauth_request_new.json";
            var recordingHandler = new RecordingHandler(tmpPath);

            recordingHandler.StartRecording(pathToRecording, httpContext.Response);
            var sessionId = httpContext.Response.Headers["x-recording-id"].ToString();
            recordingHandler.StopRecording(sessionId, variables: new SortedDictionary<string, string>());

            var storedVariables = TestHelpers.LoadRecordSession(Path.Combine(tmpPath, pathToRecording)).Session.Variables;

            Assert.Empty(storedVariables);
        }

        [Fact]
        public void TestStopRecordingNullVariables()
        {
            var tmpPath = Path.GetTempPath();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = "recordings/oauth_request_new.json";
            var recordingHandler = new RecordingHandler(tmpPath);

            recordingHandler.StartRecording(pathToRecording, httpContext.Response);
            var sessionId = httpContext.Response.Headers["x-recording-id"].ToString();
            recordingHandler.StopRecording(sessionId, variables: null);

            var storedVariables = TestHelpers.LoadRecordSession(Path.Combine(tmpPath, pathToRecording)).Session.Variables;

            Assert.Empty(storedVariables);
        }

        [Fact]
        public async void TestStartPlaybackWithVariables()
        {
            var httpContext = new DefaultHttpContext();
            var recordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            httpContext.Response.Body = new MemoryStream();

            await recordingHandler.StartPlayback("Test.RecordEntries/oauth_request_with_variables.json", httpContext.Response);
            
            Dictionary<string, string> results = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                TestHelpers.GenerateStringFromStream(httpContext.Response.Body)
            );

            Assert.Equal(2, results.Count);
            Assert.Equal("value1", results["key1"]);
            Assert.Equal("value2", results["key2"]);
        }

        [Fact]
        public async void TestStartPlaybackWithoutVariables()
        {
            var startHttpContext = new DefaultHttpContext();
            var recordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());

            await recordingHandler.StartPlayback("Test.RecordEntries/oauth_request.json", startHttpContext.Response);
        }
    }
}
