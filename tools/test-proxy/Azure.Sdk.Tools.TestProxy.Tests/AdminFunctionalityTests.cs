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
        public async Task TestSetMatcher()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "BodilessMatcher";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody("{}");

            var controller = new Admin(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            testRecordingHandler.Sanitizers.Clear();
            await controller.SetMatcher();

            var result = testRecordingHandler.Matcher;
            Assert.True(result is BodilessMatcher);
        }

        [Fact]
        public async void TestSetMatcherIndividualRecording()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            await testRecordingHandler.StartPlayback("Test.RecordEntries/oauth_request_with_variables.json", httpContext.Response);
            var recordingId = httpContext.Response.Headers["x-recording-id"];
            httpContext.Request.Headers["x-recording-id"] = recordingId;
            httpContext.Request.Headers["x-abstraction-identifier"] = "BodilessMatcher";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody("{}");

            var controller = new Admin(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await controller.SetMatcher();

            var result = testRecordingHandler.PlaybackSessions[recordingId].CustomMatcher;
            Assert.True(result is BodilessMatcher);
        }

        [Fact]
        public async void TestSetMatcherThrowsOnBadRecordingId()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-recording-id"] = "bad-recording-id";
            httpContext.Request.Headers["x-abstraction-identifier"] = "BodilessMatcher";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody("{}");

            var controller = new Admin(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            var assertion = await Assert.ThrowsAsync<HttpException>(
               async () => await controller.SetMatcher()
            );
            assertion.StatusCode.Equals(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void TestAddSanitizer()
        {
            // arrange
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
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
            await controller.AddSanitizer();

            var result = testRecordingHandler.Sanitizers.First();
            Assert.True(result is HeaderRegexSanitizer);
        }

        [Fact]
        public async void TestAddSanitizerWrongEmptyValue()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "HeaderRegexSanitizer";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody("{ \"key\": \"\", \"value\": \"https://fakeazsdktestaccount.table.core.windows.net/Tables\" }");
            httpContext.Request.ContentLength = 92;

            var controller = new Admin(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            testRecordingHandler.Sanitizers.Clear();
            
            var assertion = await Assert.ThrowsAsync<HttpException>(
               async () => await controller.AddSanitizer()
            );
            assertion.StatusCode.Equals(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void TestAddSanitizerAcceptableEmptyValue()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "HeaderRegexSanitizer";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody("{ \"key\": \"Location\", \"value\": \"\" }");
            httpContext.Request.ContentLength = 92;

            var controller = new Admin(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            testRecordingHandler.Sanitizers.Clear();
            await controller.AddSanitizer();

            var result = testRecordingHandler.Sanitizers.First();
            Assert.True(result is HeaderRegexSanitizer);
        }

        [Fact]
        public async void TestAddSanitizerIndividualRecording()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            await testRecordingHandler.StartPlayback("Test.RecordEntries/oauth_request_with_variables.json", httpContext.Response);
            var recordingId = httpContext.Response.Headers["x-recording-id"];
            httpContext.Request.Headers["x-recording-id"] = recordingId;
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
            await controller .AddSanitizer();

            var result = testRecordingHandler.PlaybackSessions[recordingId].AdditionalSanitizers.First();
            Assert.True(result is HeaderRegexSanitizer);
        }

        [Fact]
        public async void TestAddSanitizerThrowsOnBadRecordingId()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-recording-id"] = "bad-recording-id";
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

            var assertion = await Assert.ThrowsAsync<HttpException>(
               async () => await controller.AddSanitizer()
            );
            assertion.StatusCode.Equals(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task TestAddTransform()
        {
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
            await controller .AddTransform();
            var result = testRecordingHandler.Transforms.First();

            Assert.True(result is ApiVersionTransform);
        }

        [Fact]
        public async void TestAddTransformIndividualRecording()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            await testRecordingHandler.StartPlayback("Test.RecordEntries/oauth_request_with_variables.json", httpContext.Response);
            var recordingId = httpContext.Response.Headers["x-recording-id"];
            var apiVersion = "2016-03-21";
            httpContext.Request.Headers["x-api-version"] = apiVersion;
            httpContext.Request.Headers["x-abstraction-identifier"] = "ApiVersionTransform";
            httpContext.Request.Headers["x-recording-id"] = recordingId;

            var controller = new Admin(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await controller .AddTransform();

            var result = testRecordingHandler.PlaybackSessions[recordingId].AdditionalTransforms.First();
            Assert.True(result is ApiVersionTransform);
        }

        [Fact]
        public async void TestAddTransformThrowsOnBadRecordingId()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            var apiVersion = "2016-03-21";
            httpContext.Request.Headers["x-api-version"] = apiVersion;
            httpContext.Request.Headers["x-abstraction-identifier"] = "ApiVersionTransform";
            httpContext.Request.Headers["x-recording-id"] = "bad-recording-id";

            var controller = new Admin(testRecordingHandler)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            var assertion = await Assert.ThrowsAsync<HttpException>(
               async () => await controller.AddTransform()
            );
            assertion.StatusCode.Equals(HttpStatusCode.BadRequest);
        }
    }
}
