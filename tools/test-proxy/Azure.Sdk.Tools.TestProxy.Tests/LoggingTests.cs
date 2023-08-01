using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    /// <summary>
    /// Logging tests cannot be run in parallel with other tests because they share a static logger.
    /// </summary>
    [Collection(nameof(LoggingCollection))]
    public class LoggingTests
    {
        [Fact]
        public async Task PlaybackLogsSanitizedRequest()
        {
            var logger = new TestLogger();
            DebugLogger.Logger = logger;

            try
            {
                RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
                var httpContext = new DefaultHttpContext();
                var body = "{\"x-recording-file\":\"Test.RecordEntries/request_with_binary_content.json\"}";
                httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(body);
                httpContext.Request.ContentLength = body.Length;

                var controller = new Playback(testRecordingHandler, new NullLoggerFactory())
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
                request.Headers["Authorization"] = "fake-auth-header";

                HttpResponse response = new DefaultHttpContext().Response;
                await testRecordingHandler.HandlePlaybackRequest(recordingId, request, response);

                Assert.Single(logger.Logs);
                var logEntry = logger.Logs[0].ToString();
                Assert.DoesNotContain(@"""Authorization"":[""fake-auth-header""]", logEntry);
                Assert.Contains(@"""Authorization"":[""Sanitized""]", logEntry);
            }
            finally
            {
                DebugLogger.Logger = null;
            }
        }

        [Fact]
        public async Task RecordingHandlerLogsSanitizedRequests()
        {
            var logger = new TestLogger();
            DebugLogger.Logger = logger;
            var httpContext = new DefaultHttpContext();
            var bodyBytes = Encoding.UTF8.GetBytes("{\"hello\":\"world\"}");
            var mockClient = new HttpClient(new MockHttpHandler(bodyBytes, "application/json"));
            var path = Directory.GetCurrentDirectory();
            var recordingHandler = new RecordingHandler(path)
            {
                RedirectableClient = mockClient,
                RedirectlessClient = mockClient
            };

            var relativePath = "recordings/logs";
            var fullPathToRecording = Path.Combine(path, relativePath) + ".json";

            await recordingHandler.StartRecordingAsync(relativePath, httpContext.Response);

            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();

            httpContext.Request.ContentType = "application/json";
            httpContext.Request.Headers["Authorization"] = "fake-auth-header";
            httpContext.Request.ContentLength = 0;
            httpContext.Request.Headers["x-recording-id"] = recordingId;
            httpContext.Request.Headers["x-recording-upstream-base-uri"] = "http://example.org";
            httpContext.Request.Method = "GET";
            httpContext.Request.Body = new MemoryStream(CompressionUtilities.CompressBody(bodyBytes, httpContext.Request.Headers));

            await recordingHandler.HandleRecordRequestAsync(recordingId, httpContext.Request, httpContext.Response);
            recordingHandler.StopRecording(recordingId);

            try
            {
                Assert.Single(logger.Logs);
                var logEntry = logger.Logs[0].ToString();
                Assert.DoesNotContain(@"""Authorization"":[""fake-auth-header""]", logEntry);
                Assert.Contains(@"""Authorization"":[""Sanitized""]", logEntry);
            }
            finally
            {
                File.Delete(fullPathToRecording);
                DebugLogger.Logger = null;
            }
        }
    }

    [CollectionDefinition(nameof(LoggingCollection), DisableParallelization = true)]
    public class LoggingCollection
    {
    }
}