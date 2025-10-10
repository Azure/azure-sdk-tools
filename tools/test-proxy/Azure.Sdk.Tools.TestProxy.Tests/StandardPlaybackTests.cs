using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Models;
using Azure.Sdk.Tools.TestProxy.Store;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    [Collection("AccessesProxyConfig")]
    public class StandardPlaybackTests : IDisposable
    {
        public StandardPlaybackTests()
        {
            Startup.ProxyConfiguration.Mode = UniversalRecordingMode.Azure;
            Startup.ProxyConfiguration.RecordingId = null;
        }

        public void Dispose()
        {
            Startup.ProxyConfiguration.Mode = UniversalRecordingMode.Azure;
            Startup.ProxyConfiguration.RecordingId = null;
        }

        [Fact]
        public async Task TestPlaybackStartStandardProxy()
        {
            // set the startup mode to standard record
            Startup.ProxyConfiguration.Mode = UniversalRecordingMode.StandardRecord;
            Startup.ProxyConfiguration.RecordingId = string.Empty;
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            var body = "{\"x-recording-file\":\"Test.RecordEntries/failing_multipart_body.json\"}";
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
            Assert.False(string.IsNullOrEmpty(recordingId));
            Assert.True(testRecordingHandler.PlaybackSessions.ContainsKey(recordingId));
            Assert.Equal(UniversalRecordingMode.StandardPlayback, Startup.ProxyConfiguration.Mode);
            Assert.Equal(recordingId, Startup.ProxyConfiguration.RecordingId);
        }

        [Fact]
        public async Task TestStopPlaybackStandardProxy()
        {
            Startup.ProxyConfiguration.Mode = UniversalRecordingMode.StandardRecord;
            Startup.ProxyConfiguration.RecordingId = string.Empty;
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            var body = "{\"x-recording-file\":\"Test.RecordEntries/failing_multipart_body.json\"}";
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
            await controller.Stop();
            Assert.Null(Startup.ProxyConfiguration.RecordingId);
            Assert.Equal(UniversalRecordingMode.StandardPlayback, Startup.ProxyConfiguration.Mode);
        }

        [Fact]
        public async Task TestTopPlaybackThrowsOnWrongMode()
        {
            Startup.ProxyConfiguration.Mode = UniversalRecordingMode.StandardRecord;
            Startup.ProxyConfiguration.RecordingId = string.Empty;
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            var body = "{\"x-recording-file\":\"Test.RecordEntries/failing_multipart_body.json\"}";
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

            Startup.ProxyConfiguration.Mode = UniversalRecordingMode.StandardRecord;
            await Assert.ThrowsAsync<HttpException>(
                () => controller.Stop()
            );
        }
    }
}
