using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System;
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
            Assert.False(string.IsNullOrEmpty(recordingId));
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

            var recordingHandlerSession = testRecordingHandler.RecordingSessions[recordingId];

            Assert.Empty(recordingHandlerSession.Path);
        }

        [Theory]
        [InlineData("recordings/TestStartRecordSimple_nosave.json", "request-response")]
        [InlineData("recordings/TestStartRecordSimplé_nosave.json", "request-response")]
        [InlineData("recordings/TestStartRecordSimple.json", "")]
        [InlineData("recordings/TestStartRecordSimplé.json", "")]
        public async Task TestStopRecordingSimple(string targetFile, string additionalEntryModeHeader)
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
            httpContext.Request.ContentLength = 0;
            httpContext.Request.Body = null;

            if (!string.IsNullOrEmpty(additionalEntryModeHeader))
            {
                httpContext.Request.Headers["x-recording-skip"] = additionalEntryModeHeader;
            }

            await controller.Stop();

            if (string.IsNullOrEmpty(additionalEntryModeHeader))
            {
                var fullPath = await testRecordingHandler.GetRecordingPath(targetFile);
                Assert.True(File.Exists(fullPath));
            }
            else
            {
                var fullPath = await testRecordingHandler.GetRecordingPath(targetFile);
                Assert.False(File.Exists(fullPath));
            }
        }

        [Fact]
        public async Task TestStopRecordingThrowsOnInvalidSkipValue()
        {
            string targetFile = "recordings/TestStartRecordSimple_nosave.json";
            string additionalEntryModeHeader = "request-body";

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

            httpContext.Request.Headers["x-recording-skip"] = additionalEntryModeHeader;
            httpContext.Request.ContentLength = 0;
            httpContext.Request.Body = null;


            var resultingException = await Assert.ThrowsAsync<HttpException>(
               async () => await controller.Stop()
            );

            Assert.Equal("When stopping a recording and providing a \"x-recording-skip\" value, only value \"request-response\" is accepted.", resultingException.Message);
            Assert.Equal(HttpStatusCode.BadRequest, resultingException.StatusCode);
        }

        [Theory]
        [InlineData("")]
        [InlineData("request-response")]
        public async Task TestStopRecordingInMemory(string additionalEntryModeHeader)
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

            if (!string.IsNullOrEmpty(additionalEntryModeHeader))
            {
                recordContext.Request.Headers["x-recording-skip"] = additionalEntryModeHeader;
            }

            await recordController.Stop();

            if (string.IsNullOrEmpty(additionalEntryModeHeader))
            {
                Assert.True(testRecordingHandler.InMemorySessions.Count() == 1);
                Assert.NotNull(testRecordingHandler.InMemorySessions[inMemId]);
            }
            else
            {
                Assert.True(testRecordingHandler.InMemorySessions.Count() == 0);
            }
        }

        [Fact]
        public void TestMultipartMixedCanRoundTrip()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var targetPath = "multipartroundtrip.json";
            var recordingSession = new RecordSession();

            // build up a raw request from what we we would see in a real recording
            RecordEntry rawEntry = new RecordEntry();
            rawEntry.Request.Headers["Content-Type"] = ["multipart/mixed; boundary=batch_dbed2534-1685-4042-8992-9f259d8c24c7"];
            using var stream = System.IO.File.OpenRead("Test.RecordEntries/raw_multipart_request_body.txt");
            using var reader = new StreamReader(stream);
            string rawbase64content = reader.ReadToEnd().Trim();
            rawEntry.Request.Body = Convert.FromBase64String(rawbase64content);
            rawEntry.RequestMethod = Core.RequestMethod.Post;
            recordingSession.Entries.Add(rawEntry);

            // write the raw recording to disk
            var sessionForDisk = new ModifiableRecordSession(recordingSession, new SanitizerDictionary(), "abc123");
            sessionForDisk.Path = targetPath;
            testRecordingHandler.WriteToDisk(sessionForDisk);

            var loadedFromDisk = TestHelpers.LoadRecordSession(targetPath);
            Assert.Equal(rawEntry.Request.Body, loadedFromDisk.Session.Entries[0].Request.Body);
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

            var controller = new Playback(testRecordingHandler, new NullLoggerFactory())
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
                playbackContext.Request.Headers.Append(kvp.Key, kvp.Value);
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
