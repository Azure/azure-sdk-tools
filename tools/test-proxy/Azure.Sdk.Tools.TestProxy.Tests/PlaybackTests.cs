using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Matchers;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using Azure.Sdk.Tools.TestProxy.Transforms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class PlaybackTests
    {
        [Fact]
        public async void TestStartPlaybackSimple()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-recording-file"] = "Test.RecordEntries/requests_with_continuation.json";

            var controller = new Playback(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await controller.Start();

            var value = httpContext.Response.Headers["x-recording-id"].ToString();
            Assert.NotNull(value);
            Assert.True(testRecordingHandler.PlaybackSessions.ContainsKey(value));
        }

        [Fact]
        public async void TestStartPlaybackInMemory()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());

            // get a recordingId that can be used for in-mem
            var recordContext = new DefaultHttpContext();
            var recordController = new Record(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = recordContext
                }
            };
            recordController.Start();
            var inMemId = recordContext.Response.Headers["x-recording-id"].ToString();
            recordContext.Request.Headers["x-recording-id"] = new string[] { inMemId };
            recordController.Stop();

            // apply same recordingId when starting in-memory session
            var playbackContext = new DefaultHttpContext();
            playbackContext.Request.Headers["x-recording-id"] = inMemId;
            var playbackController = new Playback(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = playbackContext
                }
            };
            await playbackController.Start();

            var value = playbackContext.Response.Headers["x-recording-id"].ToString();
            Assert.NotNull(value);
            Assert.True(testRecordingHandler.PlaybackSessions.ContainsKey(value));
            Assert.True(testRecordingHandler.InMemorySessions.Count() == 1);
        }

        [Fact]
        public async void TestStartPlaybackInMemoryThrowsInInvalidRecordingId()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());

            var playbackContext = new DefaultHttpContext();
            var recordingId = Guid.NewGuid().ToString();
            playbackContext.Request.Headers["x-recording-id"] = recordingId;
            var playbackController = new Playback(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = playbackContext
                }
            };

            var assertion = await Assert.ThrowsAsync<HttpException>(
                async () => await playbackController.Start()
            );

            Assert.Equal($"There is no in-memory session with id {recordingId} available for playback retrieval.", assertion.Message);
        }

        [Fact]
        public async void TestStartPlaybackThrowsOnInvalidInput()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());

            var playbackContext = new DefaultHttpContext();
            var recordingId = Guid.NewGuid().ToString();
            playbackContext.Request.Headers["x-recording-id"] = recordingId;
            var playbackController = new Playback(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = playbackContext
                }
            };

            var assertion = await Assert.ThrowsAsync<HttpException>(
                async () => await playbackController.Start()
            );

            Assert.Equal($"There is no in-memory session with id {recordingId} available for playback retrieval.", assertion.Message);
        }

        [Fact]
        public async void TestStopPlaybackSimple()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-recording-file"] = "Test.RecordEntries/requests_with_continuation.json";

            var controller = new Playback(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await controller.Start();
            var targetRecordingId = httpContext.Response.Headers["x-recording-id"].ToString();
            
            httpContext.Request.Headers["x-recording-id"] = new string[] { targetRecordingId };
            controller.Stop();

            Assert.False(testRecordingHandler.PlaybackSessions.ContainsKey(targetRecordingId));
        }

        [Fact]
        public async void TestStopPlaybackInMemory()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());

            // get a recordingId that can be used for in-mem
            var recordContext = new DefaultHttpContext();
            var recordController = new Record(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = recordContext
                }
            };
            recordController.Start();
            var inMemId = recordContext.Response.Headers["x-recording-id"].ToString();
            recordContext.Request.Headers["x-recording-id"] = new string[] { inMemId };
            recordController.Stop();

            var playbackContext = new DefaultHttpContext();
            playbackContext.Request.Headers["x-recording-id"] = inMemId;
            var playbackController = new Playback(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = playbackContext
                }
            };
            await playbackController.Start();
            var targetRecordingId = playbackContext.Response.Headers["x-recording-id"].ToString();
            playbackContext.Request.Headers["x-recording-id"] = new string[] { targetRecordingId };
            playbackController.Stop();

            testRecordingHandler.InMemorySessions.ContainsKey(targetRecordingId);
        }

        [Fact]
        public async Task TestPlaybackSetsRetryAfterToZero()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-recording-file"] = "Test.RecordEntries/response_with_retry_after.json";

            var controller = new Playback(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await controller.Start();

            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();
            Assert.NotNull(recordingId);
            Assert.True(testRecordingHandler.PlaybackSessions.ContainsKey(recordingId));
            var entry = testRecordingHandler.PlaybackSessions[recordingId].Session.Entries[0];
            HttpRequest request = TestHelpers.CreateRequestFromEntry(entry);
            HttpResponse response = new DefaultHttpContext().Response;
            await testRecordingHandler.HandlePlaybackRequest(recordingId, request, response);
            Assert.Equal("0", response.Headers["Retry-After"]);
        }
    }
}
