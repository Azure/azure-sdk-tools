using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System;
using System.IO;
using Xunit;
using Azure.Sdk.Tools.TestProxy;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class AdminFunctionalityTests
    {
        private RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());

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

            // act
            controller.AddTransform();

            // assert
            Assert.Equal(httpContext.Response.Headers["x-api-version"], apiVersion);
        }

        [Fact]
        public void TestAddTransformIndividualRecording()
        {

        }
    }
}
