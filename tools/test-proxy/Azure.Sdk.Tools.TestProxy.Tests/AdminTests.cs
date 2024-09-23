using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Matchers;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using Azure.Sdk.Tools.TestProxy.Transforms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
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
    public class AdminTests
    {
        private NullLoggerFactory _nullLogger = new NullLoggerFactory();

        [Fact]
        public async void TestAddSanitizersThrowsOnEmptyArray()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            
            string requestBody = @"[]";

            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(requestBody);
            httpContext.Request.ContentLength = httpContext.Request.Body.Length;
            await testRecordingHandler.SanitizerRegistry.ResetSessionSanitizers();

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            var assertion = await Assert.ThrowsAsync<HttpException>(
                async () => await controller.AddSanitizers()
            );

            assertion.StatusCode.Equals(HttpStatusCode.BadRequest);
        }

        [Fact]

        public async void TestAddSanitizersHandlesPopulatedArray()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();

            var defaultSessionSanitizers = await testRecordingHandler.SanitizerRegistry.GetSanitizers();

            string requestBody = @"[
    {
        ""Name"": ""GeneralRegexSanitizer"",
        ""Body"": { 
            ""regex"": ""[a-zA-Z]?"",
            ""value"": ""hello_there"",
            ""condition"": {
                ""UriRegex"": "".+/Tables""
            }
        }
    },
    {
        ""Name"": ""HeaderRegexSanitizer"",
        ""Body"": { 
            ""key"": ""Location"",
            ""value"": ""https://fakeazsdktestaccount.table.core.windows.net/Tables""
        }
    }
]";

            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(requestBody);
            httpContext.Request.ContentLength = httpContext.Request.Body.Length;
            await testRecordingHandler.SanitizerRegistry.ResetSessionSanitizers();
            httpContext.Response.Body = new MemoryStream();

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await controller.AddSanitizers();

            var amendedSessionSanitizers = await testRecordingHandler.SanitizerRegistry.GetSanitizers();

            Assert.Equal(defaultSessionSanitizers.Count + 2, amendedSessionSanitizers.Count);
            Assert.True(amendedSessionSanitizers[defaultSessionSanitizers.Count] is GeneralRegexSanitizer);
            Assert.True(amendedSessionSanitizers[defaultSessionSanitizers.Count + 1] is HeaderRegexSanitizer);

            httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            var response = await JsonDocument.ParseAsync(httpContext.Response.Body, options: new JsonDocumentOptions() { AllowTrailingCommas = true });
            Assert.Equal((int)HttpStatusCode.OK, httpContext.Response.StatusCode);

            var prop = response.RootElement.GetProperty("Sanitizers");
            var returnedSanitizerIds = TestHelpers.EnumerateArray<string>(prop);

            Assert.Equal(2, returnedSanitizerIds.Count);
        }

        [Fact]
        public async void TestAddSanitizersThrowsOnSingleBadInput()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            
            string requestBody = @"[
    {
        ""Name"": ""GeneralRegexSanitizer"",
        ""Body"": { 
            ""regex"": ""[a-zA-Z]?"",
            ""value"": ""hello_there"",
            ""condition"": {
                ""UriRegex"": "".+/Tables""
            }
        }
    },
    {
        ""Name"": ""BadRegexIdentifier"",
        ""Body"": { 
            ""key"": ""Location"",
            ""value"": ""https://fakeazsdktestaccount.table.core.windows.net/Tables""
        }
    }
]";

            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(requestBody);
            httpContext.Request.ContentLength = httpContext.Request.Body.Length;
            await testRecordingHandler.SanitizerRegistry.Clear();

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            var assertion = await Assert.ThrowsAsync<HttpException>(
                async () => await controller.AddSanitizers()
            );

            assertion.StatusCode.Equals(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void TestAddSanitizerThrowsOnInvalidAbstractionId()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "AnInvalidSanitizer";

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await testRecordingHandler.SanitizerRegistry.Clear();

            var assertion = await Assert.ThrowsAsync<HttpException>(
               async () => await controller.AddSanitizer()
            );
            assertion.StatusCode.Equals(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void TestAddSanitizerThrowsOnEmptyAbstractionId()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await testRecordingHandler.SanitizerRegistry.Clear();

            var assertion = await Assert.ThrowsAsync<HttpException>(
               async () => await controller.AddSanitizer()
            );
            assertion.StatusCode.Equals(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void TestAddTransformThrowsOnInvalidAbstractionId()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "AnInvalidTransform";

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            testRecordingHandler.Transforms.Clear();

            var assertion = await Assert.ThrowsAsync<HttpException>(
               async () => await controller.AddTransform()
            );
            assertion.StatusCode.Equals(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void TestAddTransformThrowsOnEmptyAbstractionId()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            testRecordingHandler.Transforms.Clear();

            var assertion = await Assert.ThrowsAsync<HttpException>(
               async () => await controller.AddTransform()
            );
            assertion.StatusCode.Equals(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void TestSetMatcherThrowsOnInvalidAbstractionId()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "AnInvalidMatcher";

            var controller = new Admin(testRecordingHandler, _nullLogger)
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
        public async void TestSetMatcherThrowsOnEmptyAbstractionId()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();

            var controller = new Admin(testRecordingHandler, _nullLogger)
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
        public async Task TestSetMatcher()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "BodilessMatcher";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody("{}");

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await testRecordingHandler.SanitizerRegistry.Clear();
            await controller.SetMatcher();

            var result = testRecordingHandler.Matcher;
            Assert.True(result is BodilessMatcher);
        }

        [Fact]
        public async Task TestSetCustomMatcher()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "CustomDefaultMatcher";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody("{ \"excludedHeaders\": \"Content-Type,Content-Length\", \"ignoredHeaders\": \"Connection\", \"compareBodies\": false, \"ignoredQueryParameters\": \"api-version,location\" }");
            
            // content length must be set for the body to be parsed in SetMatcher
            httpContext.Request.ContentLength = httpContext.Request.Body.Length;

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await controller.SetMatcher();
            var matcher = testRecordingHandler.Matcher;
            Assert.True(matcher is CustomDefaultMatcher);
            
            var compareBodies = (bool) typeof(RecordMatcher).GetField("_compareBodies", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(matcher);
            Assert.False(compareBodies);

            Assert.Contains("Content-Type", matcher.ExcludeHeaders);
            Assert.Contains("Content-Length", matcher.ExcludeHeaders);
            Assert.Contains("Connection", matcher.IgnoredHeaders);
            Assert.Contains("api-version", matcher.IgnoredQueryParameters);
            Assert.Contains("location", matcher.IgnoredQueryParameters);
        }

        [Fact]
        public async Task TestSetCustomMatcherWithQueryOrdering()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "CustomDefaultMatcher";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody("{ \"ignoreQueryOrdering\": true }");

            // content length must be set for the body to be parsed in SetMatcher
            httpContext.Request.ContentLength = httpContext.Request.Body.Length;

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await controller.SetMatcher();
            var matcher = testRecordingHandler.Matcher;
            Assert.True(matcher is CustomDefaultMatcher);

            var queryOrderingValue = (bool)typeof(RecordMatcher).GetField("_ignoreQueryOrdering", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(matcher);
            Assert.True(queryOrderingValue);
        }


        [Fact]
        public async void TestSetMatcherIndividualRecording()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            await testRecordingHandler.StartPlaybackAsync("Test.RecordEntries/oauth_request_with_variables.json", httpContext.Response);
            var recordingId = httpContext.Response.Headers["x-recording-id"];
            httpContext.Request.Headers["x-recording-id"] = recordingId;
            httpContext.Request.Headers["x-abstraction-identifier"] = "BodilessMatcher";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody("{}");

            var controller = new Admin(testRecordingHandler, _nullLogger)
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

            var controller = new Admin(testRecordingHandler, _nullLogger)
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
            httpContext.Response.Body = new MemoryStream();

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await testRecordingHandler.SanitizerRegistry.Clear();
            await controller.AddSanitizer();

            httpContext.Response.Body.Seek(0, SeekOrigin.Begin);

            var response = await JsonDocument.ParseAsync(httpContext.Response.Body, options: new JsonDocumentOptions() { AllowTrailingCommas = true });
            Assert.Equal((int)HttpStatusCode.OK, httpContext.Response.StatusCode);
            Assert.True(!string.IsNullOrWhiteSpace(response.RootElement.GetProperty("Sanitizer").GetString()));

            var result = (await testRecordingHandler.SanitizerRegistry.GetSanitizers()).First();
            Assert.True(result is HeaderRegexSanitizer);
        }

        [Fact]
        public async void TestAddSanitizerWithOddDefaults()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();

            httpContext.Request.Headers["x-abstraction-identifier"] = "BodyKeySanitizer";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody("{ \"jsonPath\": \"$.TableName\" }");
            httpContext.Request.Headers["Content-Length"] = new string[] { "34" };
            httpContext.Request.Headers["Content-Type"] = new string[] { "application/json" };

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await testRecordingHandler.SanitizerRegistry.Clear();
            await controller.AddSanitizer();

            var result = (await testRecordingHandler.SanitizerRegistry.GetSanitizers()).First();
            Assert.True(result is BodyKeySanitizer);
        }

        [Fact]
        public async void TestAddSanitizerWrongEmptyValue()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "HeaderRegexSanitizer";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody("{ \"key\": \"\", \"value\": \"https://fakeazsdktestaccount.table.core.windows.net/Tables\" }");
            httpContext.Request.ContentLength = 92;

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await testRecordingHandler.SanitizerRegistry.Clear();
            
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

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await testRecordingHandler.SanitizerRegistry.Clear();
            await controller.AddSanitizer();

            var result = (await testRecordingHandler.SanitizerRegistry.GetSanitizers()).First();
            Assert.True(result is HeaderRegexSanitizer);
        }

        [Fact]
        public async void TestAddSanitizerIndividualRecording()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            await testRecordingHandler.StartPlaybackAsync("Test.RecordEntries/oauth_request_with_variables.json", httpContext.Response);
            var recordingId = httpContext.Response.Headers["x-recording-id"];

            httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-recording-id"] = recordingId;
            httpContext.Request.Headers["x-abstraction-identifier"] = "HeaderRegexSanitizer";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody("{ \"key\": \"Location\", \"value\": \"https://fakeazsdktestaccount.table.core.windows.net/Tables\" }");
            httpContext.Request.ContentLength = 92;

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            await controller.AddSanitizer();

            var result = (await testRecordingHandler.SanitizerRegistry.GetSanitizers(testRecordingHandler.PlaybackSessions[recordingId])).Last();
            
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

            var controller = new Admin(testRecordingHandler, _nullLogger)
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
        public async Task GenerateInstanceThrowsOnBadBodyFormat()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "UriRegexSanitizer";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody("{\"value\":\"replacementValue\",\"regex\":[\"a_regex_goes_here_but_this_test_is_after_another_error\"]}");
            httpContext.Request.ContentLength = 199;

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            await testRecordingHandler.SanitizerRegistry.Clear();
            
            var assertion = await Assert.ThrowsAsync<HttpException>(
               async () => await controller.AddSanitizer()
            );

            Assert.True(assertion.StatusCode.Equals(HttpStatusCode.BadRequest));
            Assert.Contains("Array parameters are not supported", assertion.Message);
        }

        [Fact]
        public async Task AddSanitizerThrowsOnAdditionOfBadRegex()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "UriRegexSanitizer";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody("{\"value\":\"replacementValue\",\"regex\":\"[\"}");
            httpContext.Request.ContentLength = 25;

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            await testRecordingHandler.SanitizerRegistry.Clear();

            var assertion = await Assert.ThrowsAsync<HttpException>(
               async () => await controller.AddSanitizer()
            );

            Assert.True(assertion.StatusCode.Equals(HttpStatusCode.BadRequest));
            Assert.Contains("Expression of value [ does not successfully compile.", assertion.Message);
        }


        [Fact]
        public async Task TestAddTransform()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            var apiVersion = "2016-03-21";
            httpContext.Request.Headers["x-api-version"] = apiVersion;
            httpContext.Request.Headers["x-abstraction-identifier"] = "ApiVersionTransform";

            var controller = new Admin(testRecordingHandler, _nullLogger)
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
        public async Task TestAddHeaderTransform()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "HeaderTransform";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(
                "{ \"key\": \"Location\", \"value\": \"https://fakeazsdktestaccount.table.core.windows.net/Tables\", \"condition\": { \"uriRegex\": \".*/token\", \"responseHeader\": { \"key\": \"Location\", \"valueRegex\": \".*/value\" } }}");
            httpContext.Request.ContentLength = httpContext.Request.Body.Length;

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            testRecordingHandler.Transforms.Clear();
            await controller.AddTransform();
            var result = testRecordingHandler.Transforms.First();

            Assert.True(result is HeaderTransform);
            var key = (string) typeof(HeaderTransform).GetField("_key", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(result);
            var replacement = (string) typeof(HeaderTransform).GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(result);
            var regex = result.Condition.ResponseHeader.ValueRegex;
            Assert.Equal("Location", key);
            Assert.Equal("https://fakeazsdktestaccount.table.core.windows.net/Tables", replacement);
            Assert.Equal(".*/value", regex);
            Assert.Equal(".*/token", result.Condition.UriRegex);
        }

        [Theory]
        [InlineData("{ \"key\": \"Location\", \"value\": \"https://fakeazsdktestaccount.table.core.windows.net/Tables\", \"condition\": { \"uriRegex\": \".*/token\", \"responseHeader\": { \"valueRegex\": \".*/value\" } }}")]
        [InlineData("{ \"key\": \"Location\", \"value\": \"https://fakeazsdktestaccount.table.core.windows.net/Tables\", \"condition\": { \"uriRegex\": \".*/token\", \"responseHeader\": { \"key\": \"\", \"valueRegex\": \".*/value\" } }}")]
        public async Task TestAddHeaderTransformThrowsWhenMissingOrEmptyResponseHeaderKey(string body)
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "HeaderTransform";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(body);
            httpContext.Request.ContentLength = httpContext.Request.Body.Length;

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            testRecordingHandler.Transforms.Clear();
            var assertion = await Assert.ThrowsAsync<HttpException>(
                async () => await controller.AddTransform()
            );
            assertion.StatusCode.Equals(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async void TestAddTransformIndividualRecording()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            await testRecordingHandler.StartPlaybackAsync("Test.RecordEntries/oauth_request_with_variables.json", httpContext.Response);
            var recordingId = httpContext.Response.Headers["x-recording-id"];
            var apiVersion = "2016-03-21";
            httpContext.Request.Headers["x-api-version"] = apiVersion;
            httpContext.Request.Headers["x-abstraction-identifier"] = "ApiVersionTransform";
            httpContext.Request.Headers["x-recording-id"] = recordingId;

            var controller = new Admin(testRecordingHandler, _nullLogger)
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

            var controller = new Admin(testRecordingHandler, _nullLogger)
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

        [Theory]
        [InlineData("{ \"value\": \"hello_there\", \"condition\": {\"uriRegex\": \"Broken Data Structure\"}", "The body of this request is invalid JSON.")]
        [InlineData("{ \"value\": \"hello_there\", \"condition\": {\"UriRegex2\": \"Invalid Key\"}}", "At least one trigger regex must be present.")]
        [InlineData("{ \"value\": \"hello_there\", \"condition\": {\"UriRegex\": \"[\"}}", " Invalid pattern '[' at offset 1")] // [ alone is a bad regex
        [InlineData("{ \"value\": \"hello_there\", \"condition\": {}}", "At least one trigger regex must be present.")] // empty condition keys, but defined condition object
        public async Task TestAddSanitizerWithInvalidConditionJson(string requestBody, string errorText)
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "GeneralRegexSanitizer";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(requestBody);
            httpContext.Request.ContentLength = httpContext.Request.Body.Length;

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            await testRecordingHandler.SanitizerRegistry.Clear();
            var assertion = await Assert.ThrowsAsync<HttpException>(
               async () => await controller.AddSanitizer()
            );
            Assert.Equal(HttpStatusCode.BadRequest, assertion.StatusCode);
            Assert.Empty(await testRecordingHandler.SanitizerRegistry.GetSanitizers());
            Assert.Contains(errorText, assertion.Message);
        }

        [Theory]
        [InlineData("{ \"value\": \"hello_there\", \"condition\": {\"uriRegex\": \"CONDITION_REGEX\"}}", "Bad Capitalization, proper name")]
        [InlineData("{ \"value\": \"hello_there\", \"regex\": \"[a-zA-Z]?\", \"condition\": {\"UriRegex\": \"CONDITION_REGEX\"}}", ".+/Tables")]
        public async Task TestAddSanitizerWithValidUriRegexCondition(string requestBody, string conditionRegex)
        {
            requestBody = requestBody.Replace("CONDITION_REGEX", conditionRegex);
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "GeneralRegexSanitizer";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(requestBody);
            httpContext.Request.ContentLength = httpContext.Request.Body.Length;

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            await testRecordingHandler.SanitizerRegistry.Clear();
            await controller.AddSanitizer();

            var appliedSanitizers = await testRecordingHandler.SanitizerRegistry.GetSanitizers();

            Assert.Single(appliedSanitizers);
            Assert.True(appliedSanitizers.First() is GeneralRegexSanitizer);
            Assert.True(appliedSanitizers.First().Condition != null);
            Assert.True(appliedSanitizers.First().Condition is ApplyCondition);
            Assert.Equal(conditionRegex, appliedSanitizers.First().Condition.UriRegex);
        }

        [Theory]
        [InlineData("{ \"value\": \"hello_there\", \"condition\": {\"uriRegex\": \"CONDITION_REGEX\"}}", "An extra key present in body.")]
        [InlineData("{ \"condition\": {\"UriRegex\": \"CONDITION_REGEX\"}}", ".+/Tables")]
        public async Task TestAddTransformWithValidUriRegexCondition(string body, string regex)
        {
            var requestBody = body.Replace("CONDITION_REGEX", regex);
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "ApiVersionTransform";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(requestBody);
            httpContext.Request.ContentLength = httpContext.Request.Body.Length;

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            testRecordingHandler.Transforms.Clear();
            await controller.AddTransform();
            var createdTransform = testRecordingHandler.Transforms.First();

            Assert.Single(testRecordingHandler.Transforms);
            Assert.True(createdTransform is ApiVersionTransform);
            Assert.True(createdTransform.Condition != null);
            Assert.True(createdTransform.Condition is ApplyCondition);
            Assert.Equal(regex, createdTransform.Condition.UriRegex);
        }

        [Theory]
        [InlineData("{ \"value\": \"hello_there\", \"condition\": {\"uriRegex\": \"Broken Data Structure\"}", "The body of this request is invalid JSON.")]
        [InlineData("{ \"value\": \"hello_there\", \"condition\": {\"UriRegex2\": \"Invalid Key\"}}", "At least one trigger regex must be present.")]
        [InlineData("{ \"value\": \"hello_there\", \"condition\": {\"UriRegex\": \"[\"}}", " Invalid pattern '[' at offset 1")] // [ alone is a bad regex
        [InlineData("{ \"value\": \"hello_there\", \"condition\": {}}", "At least one trigger regex must be present.")] // empty condition keys, but defined condition object
        public async Task TestAddTransformWithBadUriRegexCondition(string body, string errorText)
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "ApiVersionTransform";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(body);
            httpContext.Request.ContentLength = httpContext.Request.Body.Length;

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            testRecordingHandler.Transforms.Clear();
            var assertion = await Assert.ThrowsAsync<HttpException>(
               async () => await controller.AddTransform()
            );
            Assert.Equal(HttpStatusCode.BadRequest, assertion.StatusCode);
            Assert.Empty(testRecordingHandler.Transforms);
            Assert.Contains(errorText, assertion.Message);
        }

        [Fact]
        public async Task TestAddSanitizerThrowsOnMissingRequiredArgument()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "GeneralStringSanitizer";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody("{\"value\":\"replacementValue\"}");
            httpContext.Request.ContentLength = 33;

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            await testRecordingHandler.SanitizerRegistry.Clear();

            var assertion = await Assert.ThrowsAsync<HttpException>(
               async () => await controller.AddSanitizer()
            );

            Assert.True(assertion.StatusCode.Equals(HttpStatusCode.BadRequest));
            Assert.Contains("Required parameter key \"target\" was not found in the request body.", assertion.Message);
        }

        [Fact]
        public async Task TestAddSanitizerContinuesWithTwoRequiredParams()
        {
            var targetKey = "Content-Type";
            var targetString = "application/javascript";

            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "HeaderStringSanitizer";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody("{\"target\":\"" + targetString + "\", \"key\":\"" + targetKey + "\"}");
            httpContext.Request.ContentLength = 79;

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            await testRecordingHandler.SanitizerRegistry.Clear();
            await controller.AddSanitizer();

            var addedSanitizer = (await testRecordingHandler.SanitizerRegistry.GetSanitizers()).First();
            Assert.True(addedSanitizer is HeaderStringSanitizer);

            var actualTargetString = (string)typeof(HeaderStringSanitizer).GetField("_targetValue", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(addedSanitizer);
            var actualTargetKey = (string)typeof(HeaderStringSanitizer).GetField("_targetKey", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(addedSanitizer);
            Assert.Equal(targetKey, actualTargetKey);
            Assert.Equal(targetString, actualTargetString);
        }

        [Fact]
        public async Task RemoveSanitizersSilentlyAcceptsInvalidSanitizer()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            var testSet = new RemoveSanitizerList() { Sanitizers = new List<string>() { "AZSDK00-1" } };

            await controller.RemoveSanitizers(testSet);

            Assert.Equal(200, httpContext.Response.StatusCode);
        }

        [Fact]
        public async Task RemoveSanitizerErrorsForMissingId()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            var testSet = new RemoveSanitizerList() { Sanitizers = new List<string>() {} };

            var assertion = await Assert.ThrowsAsync<HttpException>(
                async () => await controller.RemoveSanitizers(testSet)
            );

            Assert.Equal(HttpStatusCode.BadRequest, assertion.StatusCode);
            Assert.Equal("At least one sanitizerId for removal must be provided.", assertion.Message);
        }

        [Fact]
        public async Task RemoveSanitizerSucceedsForExistingSessionSanitizer()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            var expectedSanitizerCount = (await testRecordingHandler.SanitizerRegistry.GetSanitizers()).Count;
            await controller.RemoveSanitizers(new RemoveSanitizerList() { Sanitizers = new List<string>() { "AZSDK0000" } });

            httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            var response = await JsonDocument.ParseAsync(httpContext.Response.Body, options: new JsonDocumentOptions() { AllowTrailingCommas = true });
            Assert.Equal((int)HttpStatusCode.OK, httpContext.Response.StatusCode);

            var prop = response.RootElement.GetProperty("Removed");
            var returnedSanitizerIds = TestHelpers.EnumerateArray<string>(prop);

            Assert.Single(returnedSanitizerIds);
            Assert.Equal("AZSDK0000", returnedSanitizerIds.First());

            Assert.Equal(expectedSanitizerCount - 1, (await testRecordingHandler.SanitizerRegistry.GetSanitizers()).Count);
        }

        [Fact]
        public async Task RemoveSanitizerSucceedsForAddedRecordingSanitizer()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            await testRecordingHandler.SanitizerRegistry.Clear();
            var httpContext = new DefaultHttpContext();
            await testRecordingHandler.StartPlaybackAsync("Test.RecordEntries/oauth_request_with_variables.json", httpContext.Response);
            var recordingId = httpContext.Response.Headers["x-recording-id"];
            
            // use returned recordingId to register new sanitizers
            httpContext.Request.Headers["x-recording-id"] = recordingId;
            httpContext.Response.Body = new MemoryStream();
            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };
            var session = testRecordingHandler.GetActiveSession(recordingId);

            var forRemoval = await testRecordingHandler.RegisterSanitizer(new HeaderRegexSanitizer("Content-Type"), recordingId);
            await testRecordingHandler.RegisterSanitizer(new HeaderRegexSanitizer("Connection"), recordingId);
            await controller.RemoveSanitizers(new RemoveSanitizerList() { Sanitizers = new List<string>() { forRemoval } });

            var activeRecordingSanitizers = await testRecordingHandler.SanitizerRegistry.GetSanitizers(session);

            Assert.Single(activeRecordingSanitizers);
            var privateSetting = (string)typeof(HeaderRegexSanitizer).GetField("_targetKey", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(activeRecordingSanitizers.First());
            Assert.Equal("Connection", privateSetting);
        }

        [Fact]
        public async void GetSanitizersReturnsSessionSanitizers()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            await testRecordingHandler.RegisterSanitizer(new HeaderRegexSanitizer("Connection"));

            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            await controller.GetSanitizers();

            httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            var response = await JsonDocument.ParseAsync(httpContext.Response.Body, options: new JsonDocumentOptions() { AllowTrailingCommas = true });
            Assert.Equal((int)HttpStatusCode.OK, httpContext.Response.StatusCode);

            var prop = response.RootElement.GetProperty("Sanitizers");
            List<string> foundIds = new List<string>();

            foreach(var i in prop.EnumerateArray())
            {
                foundIds.Add(i.GetProperty("Id").GetRawText());
            }

            Assert.Equal(testRecordingHandler.SanitizerRegistry.DefaultSanitizerList.Count + 1, foundIds.Count);
        }

        [Fact]
        public async Task GetSanitizersReturnsRecordingSanitizers()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            await testRecordingHandler.SanitizerRegistry.Clear();
            await testRecordingHandler.StartPlaybackAsync("Test.RecordEntries/oauth_request_with_variables.json", httpContext.Response);
            var recordingId = httpContext.Response.Headers["x-recording-id"];
            httpContext.Request.Headers["x-recording-id"] = recordingId;
            httpContext.Response.Body = new MemoryStream();
            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            await testRecordingHandler.RegisterSanitizer(new HeaderRegexSanitizer("Connection"), recordingId);
            await controller.GetSanitizers();

            httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            var response = await JsonDocument.ParseAsync(httpContext.Response.Body, options: new JsonDocumentOptions() { AllowTrailingCommas = true });
            Assert.Equal((int)HttpStatusCode.OK, httpContext.Response.StatusCode);

            var prop = response.RootElement.GetProperty("Sanitizers");
            List<string> foundIds = new List<string>();

            foreach(var i in prop.EnumerateArray())
            {
                foundIds.Add(i.GetProperty("Id").GetRawText());
            }

            Assert.Single(foundIds);
        }

    }
}
