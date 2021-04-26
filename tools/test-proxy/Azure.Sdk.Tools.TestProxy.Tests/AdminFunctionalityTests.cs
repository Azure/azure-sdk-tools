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
            // arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-"] = "";

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

        }

        [Fact]
        public void TestAddTransformIndividualRecording()
        {

        }
    }
}
