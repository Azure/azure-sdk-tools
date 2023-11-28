using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

                var playback = new Playback(testRecordingHandler, new TestLoggingFactory(logger))
                {
                    ControllerContext = new ControllerContext()
                    {
                        HttpContext = httpContext
                    }
                };
                await playback.Start();

                var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();
                Assert.NotNull(recordingId);

                await AddSanitizerAsync(testRecordingHandler, logger);

                Assert.True(testRecordingHandler.PlaybackSessions.ContainsKey(recordingId));
                var entry = testRecordingHandler.PlaybackSessions[recordingId].Session.Entries[0];
                HttpRequest request = TestHelpers.CreateRequestFromEntry(entry);
                request.Headers["Authorization"] = "fake-auth-header";

                HttpResponse response = new DefaultHttpContext().Response;
                await testRecordingHandler.HandlePlaybackRequest(recordingId, request, response);

                AssertLogs(logger, 4, 8);
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

            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/request_with_binary_content.json");
            var request = TestHelpers.CreateRequestFromEntry(session.Session.Entries[0]);
            var httpContext = new DefaultHttpContext();
            var bodyBytes = Encoding.UTF8.GetBytes("{\"hello\":\"world\"}");
            var mockClient = new HttpClient(new MockHttpHandler(bodyBytes, "application/json"));
            var path = Directory.GetCurrentDirectory();
            var testRecordingHandler = new RecordingHandler(path)
            {
                RedirectableClient = mockClient,
                RedirectlessClient = mockClient
            };

            var relativePath = "recordings/logs";
            var fullPathToRecording = Path.Combine(path, relativePath) + ".json";

            await testRecordingHandler.StartRecordingAsync(relativePath, httpContext.Response);

            await AddSanitizerAsync(testRecordingHandler, logger);

            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();

            await testRecordingHandler.HandleRecordRequestAsync(recordingId, request, httpContext.Response);
            testRecordingHandler.StopRecording(recordingId);

            try
            {
                AssertLogs(logger, 2, 8);
            }
            finally
            {
                File.Delete(fullPathToRecording);
                DebugLogger.Logger = null;
            }
        }

        private static void AssertLogs(TestLogger logger, int offset, int expectedLength)
        {
            Assert.Equal(expectedLength, logger.Logs.Count);
            Assert.Equal(
                $"URI: [ http://127.0.0.1:5000/admin/addsanitizer]{Environment.NewLine}Headers: " +
                "[{\"Host\":[\"127.0.0.1:5000\"],\"x-abstraction-identifier\":[\"HeaderRegexSanitizer\"]," +
                "\"Content-Length\":[\"92\"]}]" + Environment.NewLine,
                logger.Logs[0 + offset].ToString());
            Assert.Equal(
                "Request Body Content{\"key\":\"Location\",\"value\":\"https://fakeazsdktestaccount.table.core.windows.net/Tables\"}",
                logger.Logs[1 + offset].ToString());
            // sanitizer request body is currently duplicated for each key/value pair
            Assert.Equal(
                "Request Body Content{\"key\":\"Location\",\"value\":\"https://fakeazsdktestaccount.table.core.windows.net/Tables\"}",
                logger.Logs[2 + offset].ToString());
            Assert.Equal("URI: [ https://fakeazsdktestaccount.table.core.windows.net/Tables]" +
                         Environment.NewLine + "Headers: [{\"Accept\":[\"application/json;odata=minimalmetadata\"],\"Accept-Encoding\":[\"gzip, deflate\"],\"Authorization\":[\"Sanitized\"],\"Connection\":[\"keep-alive\"]," +
                         "\"Content-Length\":[\"12\"],\"Content-Type\":[\"application/octet-stream\"],\"DataServiceVersion\":[\"3.0\"],\"Date\":[\"Tue, 18 May 2021 23:27:42 GMT\"]," +
                         "\"User-Agent\":[\"azsdk-python-data-tables/12.0.0b7 Python/3.8.6 (Windows-10-10.0.19041-SP0)\"],\"x-ms-client-request-id\":[\"a4c24b7a-b830-11eb-a05e-10e7c6392c5a\"]," +
                         "\"x-ms-date\":[\"Tue, 18 May 2021 23:27:42 GMT\"],\"x-ms-version\":[\"2019-02-02\"]}]" + Environment.NewLine,
                logger.Logs[3 + offset].ToString());
        }

        private static async Task AddSanitizerAsync(RecordingHandler testRecordingHandler, TestLogger logger)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = new HostString("127.0.0.1:5000");
            httpContext.Request.Path = "/admin/addsanitizer";
            httpContext.Request.Headers["x-abstraction-identifier"] = "HeaderRegexSanitizer";
            httpContext.Request.Body =
                TestHelpers.GenerateStreamRequestBody(
                    "{ \"key\": \"Location\", \"value\": \"https://fakeazsdktestaccount.table.core.windows.net/Tables\" }");
            httpContext.Request.ContentLength = 92;

            var admin = new Admin(testRecordingHandler, new TestLoggingFactory(logger))
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await admin.AddSanitizer();
        }
    }



    [CollectionDefinition(nameof(LoggingCollection), DisableParallelization = true)]
    public class LoggingCollection
    {
    }
}
