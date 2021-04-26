using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System;
using System.IO;
using Xunit;
using Azure.Sdk.Tools.TestProxy;
using System.Linq;
using Azure.Sdk.Tools.TestProxy.Transforms;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class AdminFunctionalityTests
    {
        [Fact]
        public void TestSetMatcher()
        {

        }

        [Fact]
        public void TestSetMatcherIndividualRecording()
        {

        }

        [Fact]
        public void TestAddSanitizer()
        {

        }

        [Fact]
        public void TestAddSanitizerIndividualRecording()
        {

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
    }
}
