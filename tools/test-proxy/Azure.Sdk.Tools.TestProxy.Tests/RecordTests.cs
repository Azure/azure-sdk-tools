using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Matchers;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using Azure.Sdk.Tools.TestProxy.Transforms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class RecordTests
    {
        [Fact]
        public void TestStartRecordSimple()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-recording-file"] = "recordings/TestStartRecordSimple.json";

            var controller = new Record(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            controller.Start();
            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();
            Assert.NotNull(recordingId);
            Assert.True(testRecordingHandler.RecordingSessions.ContainsKey(recordingId));
        }

        [Fact]
        public void TestStartRecordInMemory()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();

            var controller = new Record(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            controller.Start();
            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();

            var (fileName, session) = testRecordingHandler.RecordingSessions[recordingId];

            Assert.Empty(fileName);
        }

        [Fact]
        public void TestStopRecordingSimple()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            var targetFile = "recordings/TestStartRecordSimple.json";
            httpContext.Request.Headers["x-recording-file"] = targetFile;

            var controller = new Record(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            controller.Start();
            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();
            httpContext.Request.Headers["x-recording-id"] = recordingId;
            httpContext.Request.Headers.Remove("x-recording-file");

            controller.Stop();

            var fullPath = testRecordingHandler.GetRecordingPath(targetFile);
            Assert.True(File.Exists(fullPath));
        }

        [Fact]
        public void TestStopRecordingInMemory()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());

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
            recordContext.Request.Headers["x-recording-id"] = new string[] { inMemId
            };
            recordController.Stop();

            Assert.True(testRecordingHandler.InMemorySessions.Count() == 1);
            Assert.NotNull(testRecordingHandler.InMemorySessions[inMemId]);
        }
    }
}
