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
        public async void TestStartRecordSimple()
        {
            //RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            //var httpContext = new DefaultHttpContext();
            //httpContext.Request.Headers["x-abstraction-identifier"] = "HeaderRegexSanitizer";
            //httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody("{ \"key\": \"\", \"value\": \"https://fakeazsdktestaccount.table.core.windows.net/Tables\" }");
            //httpContext.Request.ContentLength = 92;

            //var controller = new Admin(testRecordingHandler)
            //{
            //    ControllerContext = new ControllerContext()
            //    {
            //        HttpContext = httpContext
            //    }
            //};
            //testRecordingHandler.Sanitizers.Clear();

            //var assertion = await Assert.ThrowsAsync<HttpException>(
            //   async () => await controller.AddSanitizer()
            //);
            //assertion.StatusCode.Equals(HttpStatusCode.BadRequest);
        }

        public async void TestStartRecordInMemory()
        {
        }

        public async void TestStartRecord()
        {
        }

        public async void TestStopRecordingSimple()
        {
        }

        public async void TestStopRecordingInMemory()
        {
        }
    }
}
