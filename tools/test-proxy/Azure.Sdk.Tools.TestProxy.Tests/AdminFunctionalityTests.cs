using Azure.Sdk.Tools.TestProxy.Sanitizers;
using Azure.Sdk.Tools.TestProxy.Transforms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    /// <summary>
    /// The tests contained here-in are intended to exercise the actual admin functionality of the controller. 
    /// Specifically, handling add/remove/update of various sanitizers, transforms, and matchers. 
    /// 
    /// The admin controller uses Activator.CreateInstance to create these dynamically, so we need to ensure we actually
    /// catch edges cases with this creation logic. ESPECIALLY when we're dealing with parametrized ones.
    /// 
    /// The testing of the actual functionality of each of these concepts should take place in SanitizerTests, TransformTests, etc.
    /// </summary>
    public class AdminFunctionalityTests
    {


        [Fact]
        public void TestSetMatcher()
        {

        }

        [Fact]
        public void TestAddSanitizer()
        {
            // arrange
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            var apiVersion = "2016-03-21";
            httpContext.Request.Headers["x-api-version"] = apiVersion;
            httpContext.Request.Headers["x-abstraction-identifier"] = "HeaderRegexSanitizer";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody("{ \"key\": \"Location\", \"value\": \"https://fakeazsdktestaccount.table.core.windows.net/Tables\" }");
            httpContext.Request.ContentLength = 92;

            var controller = new Admin(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            testRecordingHandler.Sanitizers.Clear();

            controller.AddSanitizer();

            var result = testRecordingHandler.Sanitizers.First();
            Assert.True(result is HeaderRegexSanitizer);
        }

        [Fact]
        public void TestAddTransform()
        {
            // arrange
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            var apiVersion = "2016-03-21";
            httpContext.Request.Headers["x-api-version"] = apiVersion;
            httpContext.Request.Headers["x-abstraction-identifier"] = "ApiVersionTransform";

            var controller = new Admin(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            testRecordingHandler.Transforms.Clear();

            // act
            controller.AddTransform();

            var result = testRecordingHandler.Transforms.First();

            // assert
            Assert.True(result is ApiVersionTransform);
        }

        [Fact]
        public void TestAddTransformIndividualRecording()
        {

        }


        [Fact]
        public void TestAddSanitizerIndividualRecording()
        {

        }

        [Fact]
        public void TestSetMatcherIndividualRecording()
        {

        }
    }
}
