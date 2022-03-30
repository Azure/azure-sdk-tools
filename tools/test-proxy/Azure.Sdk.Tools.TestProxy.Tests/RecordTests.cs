using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Matchers;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using Azure.Sdk.Tools.TestProxy.Transforms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class RecordTests
    {
        private NullLoggerFactory _nullLogger = new NullLoggerFactory();

        [Fact]
        public async Task TestStartRecordSimple()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            var body = "{\"x-recording-file\":\"recordings/TestStartRecordSimple.json\"}";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(body);
            httpContext.Request.ContentLength = body.Length;

            var controller = new Record(testRecordingHandler, new NullLoggerFactory())
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await controller.Start();
            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();
            Assert.NotNull(recordingId);
            Assert.True(testRecordingHandler.RecordingSessions.ContainsKey(recordingId));
        }

        [Fact]
        public async Task TestStartRecordInMemory()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();

            var controller = new Record(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await controller.Start();
            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();

            var (fileName, session) = testRecordingHandler.RecordingSessions[recordingId];

            Assert.Empty(fileName);
        }

        [Theory]
        [InlineData("recordings/TestStartRecordSimple.json")]
        [InlineData("recordings/TestStartRecordSimplé.json")]
        public async Task TestStopRecordingSimple(string targetFile)
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            var body = "{\"x-recording-file\":\"" + targetFile + "\"}";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(body);
            httpContext.Request.ContentLength = body.Length;

            var controller = new Record(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await controller.Start();
            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();
            httpContext.Request.Headers["x-recording-id"] = recordingId;
            httpContext.Request.Headers.Remove("x-recording-file");

            controller.Stop();

            var fullPath = testRecordingHandler.GetRecordingPath(targetFile);
            Assert.True(File.Exists(fullPath));
        }

        [Fact]
        public async Task TestStopRecordingInMemory()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());

            var recordContext = new DefaultHttpContext();
            var recordController = new Record(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = recordContext
                }
            };
            await recordController.Start();
            var inMemId = recordContext.Response.Headers["x-recording-id"].ToString();
            recordContext.Request.Headers["x-recording-id"] = new string[] { inMemId
            };
            recordController.Stop();

            Assert.True(testRecordingHandler.InMemorySessions.Count() == 1);
            Assert.NotNull(testRecordingHandler.InMemorySessions[inMemId]);
        }

        [Fact]
        public async Task TestPlaybackThrowsOnDifferentUriOrder()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var playbackContext = new DefaultHttpContext();
            var targetFile = "Test.RecordEntries/request_with_subscriptionid.json";
            playbackContext.Request.Headers["x-recording-file"] = targetFile;
            var body = "{\"x-recording-file\":\"" + targetFile + "\"}";
            playbackContext.Request.Body = TestHelpers.GenerateStreamRequestBody(body);
            playbackContext.Request.ContentLength = body.Length;

            var controller = new Playback(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = playbackContext
                }
            };
            await controller.Start();
            var recordingId = playbackContext.Response.Headers["x-recording-id"].ToString();

            // prepare recording context
            playbackContext.Request.Headers.Clear();
            playbackContext.Response.Headers.Clear();
            var requestHeaders = new Dictionary<string, string>(){
                { ":authority", "localhost:5001" },
                { ":method", "POST" },
                { ":path", "/" },
                { ":scheme", "https" },
                { "Accept-Encoding", "gzip" },
                { "Content-Length", "0" },
                { "User-Agent", "Go-http-client/2.0" },
                { "x-recording-id", recordingId },
                { "x-recording-upstream-base-uri", "https://management.azure.com/" }
            };
            foreach (var kvp in requestHeaders)
            {
                playbackContext.Request.Headers.Add(kvp.Key, kvp.Value);
            }
            playbackContext.Request.Method = "POST";

            // the query parameters are in reversed order from the recording deliberately.
            var queryString = "?uselessUriAddition=hellothere&api-version=2019-05-01";
            var path = "/subscriptions/12345678-1234-1234-5678-123456789010/providers/Microsoft.ContainerRegistry/checkNameAvailability";
            playbackContext.Request.Host = new HostString("https://localhost:5001");
            playbackContext.Features.Get<IHttpRequestFeature>().RawTarget = path + queryString;

            var resultingException = await Assert.ThrowsAsync<TestRecordingMismatchException>(
               async () => await testRecordingHandler.HandlePlaybackRequest(recordingId, playbackContext.Request, playbackContext.Response)
            );

            Assert.Contains("Uri doesn't match:", resultingException.Message);
        }
    }
}
