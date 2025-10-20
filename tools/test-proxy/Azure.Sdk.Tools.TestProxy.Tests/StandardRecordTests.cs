using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    [Collection("AccessesProxyConfig")]
    public class StandardRecordTests : IDisposable
    {
        public StandardRecordTests()
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
        public async Task TestStartRecordStandardProxy()
        {
            Startup.ProxyConfiguration.Mode = UniversalRecordingMode.StandardRecord;
            Startup.ProxyConfiguration.RecordingId = string.Empty;
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            var body = "{\"x-recording-file\":\"TestStartRecordNewFormat.json\"}";
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
            Assert.Equal(UniversalRecordingMode.StandardRecord, Startup.ProxyConfiguration.Mode);
            Assert.Equal(recordingId, Startup.ProxyConfiguration.RecordingId);
        }

        [Fact]
        public async Task TestStopRecordStandardProxy()
        {
            var testFile = "TestStartRecordNewFormat.json";
            Startup.ProxyConfiguration.Mode = UniversalRecordingMode.StandardRecord;
            Startup.ProxyConfiguration.RecordingId = string.Empty;
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            var body = "{\"x-recording-file\":\"" + testFile + "\"}";
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
            httpContext.Request.ContentLength = 0;
            await controller.Stop();
            Assert.True(File.Exists(Path.Combine(Directory.GetCurrentDirectory(), testFile)));
        }

        [Fact]
        public async Task TestTopRecordThrowsOnWrongMode()
        {
            var testFile = "TestStartRecordNewFormat.json";
            Startup.ProxyConfiguration.Mode = UniversalRecordingMode.StandardRecord;
            Startup.ProxyConfiguration.RecordingId = string.Empty;
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            var body = "{\"x-recording-file\":\"" + testFile + "\"}";
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
            httpContext.Request.ContentLength = 0;

            Startup.ProxyConfiguration.Mode = UniversalRecordingMode.StandardPlayback;
            await Assert.ThrowsAsync<HttpException>(
                () => controller.Stop()
            );
        }
    }
}
