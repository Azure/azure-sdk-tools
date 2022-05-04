using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Matchers;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using Azure.Sdk.Tools.TestProxy.Transforms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using Xunit;
using Azure.Core;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class RecordingHandlerTests
    {
        private HttpContext GenerateHttpRequestContext(string[] headerValueStrings)
        {
            HttpContext context = new DefaultHttpContext();

            foreach (var hTuple in GenerateHeaderValuesTuples(headerValueStrings))
            {

                context.Request.Headers.TryAdd(hTuple.Item1, hTuple.Item2);
            }

            context.Request.Headers.TryAdd("x-recording-upstream-base-uri", new string[] { "https://hello-world" });
            context.Request.Method = "POST";

            return context;
        }

        private IEnumerable<Tuple<string, StringValues>> GenerateHeaderValuesTuples(string[] headerValueStrings)
        {
            var returnedTuples = new List<Tuple<string, StringValues>>();
            foreach (var headString in headerValueStrings)
            {
                var splitLocation = headString.IndexOf(':');
                var headerKey = headString.Substring(0, splitLocation);
                var headerValue = headString.Substring(splitLocation).Split(";").ToArray();
                returnedTuples.Add(new Tuple<string, StringValues>(headerKey, headerValue));
            }

            return returnedTuples;
        }

        private NullLoggerFactory _nullLogger = new NullLoggerFactory();

        [Flags]
        enum CheckSkips
        {
            None = 0,
            IncludeTransforms = 1,
            IncludeSanitizers = 2,
            IncludeMatcher = 4,

            Default = IncludeTransforms | IncludeSanitizers | IncludeMatcher
        }

        private void _checkDefaultExtensions(RecordingHandler handlerForTest, CheckSkips skipsToCheck = CheckSkips.Default)
        {
            if (skipsToCheck.HasFlag(CheckSkips.IncludeTransforms))
            {
                Assert.Equal(3, handlerForTest.Transforms.Count);
                Assert.IsType<StorageRequestIdTransform>(handlerForTest.Transforms[0]);
                Assert.IsType<ClientIdTransform>(handlerForTest.Transforms[1]);
                Assert.IsType<HeaderTransform>(handlerForTest.Transforms[2]);
            }

            if (skipsToCheck.HasFlag(CheckSkips.IncludeMatcher))
            {
                Assert.NotNull(handlerForTest.Matcher);
                Assert.IsType<RecordMatcher>(handlerForTest.Matcher);
            }

            if (skipsToCheck.HasFlag(CheckSkips.IncludeSanitizers))
            {
                Assert.Equal(3, handlerForTest.Sanitizers.Count);
                Assert.IsType<RecordedTestSanitizer>(handlerForTest.Sanitizers[0]);
                Assert.IsType<BodyKeySanitizer>(handlerForTest.Sanitizers[1]);
                Assert.IsType<BodyKeySanitizer>(handlerForTest.Sanitizers[2]);
            }
        }

        [Fact]
        public void TestGetHeader()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-test-presence"] = "This header has a value";

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            RecordingHandler.GetHeader(httpContext.Request, "x-test-presence");
        }

        [Fact]
        public void TestGetHeaderThrowsOnMissingHeader()
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

            var assertion = Assert.Throws<HttpException>(
               () => RecordingHandler.GetHeader(httpContext.Request, "x-test-presence")
            );
        }

        [Fact]
        public void TestGetHeaderSilentOnAcceptableHeaderMiss()
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

            var value = RecordingHandler.GetHeader(httpContext.Request, "x-test-presence", allowNulls: true);
            Assert.Null(value);
        }

        [Fact]
        public void TestResetAfterAddition()
        {
            // arrange
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());

            // act
            testRecordingHandler.Sanitizers.Add(new BodyRegexSanitizer("sanitized", ".*"));
            testRecordingHandler.Matcher = new BodilessMatcher();
            testRecordingHandler.Transforms.Add(new ApiVersionTransform());
            testRecordingHandler.SetDefaultExtensions();

            //assert
            _checkDefaultExtensions(testRecordingHandler);
        }

        [Fact]
        public void TestResetAfterRemoval()
        {
            // arrange
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());

            // act
            testRecordingHandler.Sanitizers.Clear();
            testRecordingHandler.Matcher = null;
            testRecordingHandler.Transforms.Clear();
            testRecordingHandler.SetDefaultExtensions();

            //assert
            _checkDefaultExtensions(testRecordingHandler);
        }

        [Fact]
        public void TestResetTargetsRecordingOnly()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            testRecordingHandler.StartRecording("recordingings/cool.json", httpContext.Response);
            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();


            testRecordingHandler.Sanitizers.Clear();
            testRecordingHandler.Sanitizers.Add(new BodyRegexSanitizer("sanitized", ".*"));
            testRecordingHandler.AddSanitizerToRecording(recordingId, new GeneralRegexSanitizer("sanitized", ".*"));
            testRecordingHandler.SetDefaultExtensions(recordingId);
            var session = testRecordingHandler.RecordingSessions.First().Value;

            // session sanitizer is still set to a single one
            Assert.Single(testRecordingHandler.Sanitizers);
            Assert.IsType<BodyRegexSanitizer>(testRecordingHandler.Sanitizers[0]);
            _checkDefaultExtensions(testRecordingHandler, CheckSkips.IncludeMatcher | CheckSkips.IncludeTransforms);
            Assert.Empty(session.ModifiableSession.AdditionalSanitizers);
            Assert.Empty(session.ModifiableSession.AdditionalTransforms);
            Assert.Null(session.ModifiableSession.CustomMatcher);
        }

        [Fact]
        public void TestResetTargetsSessionOnly()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            testRecordingHandler.StartRecording("recordingings/cool.json", httpContext.Response);
            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();

            testRecordingHandler.Sanitizers.Clear();
            testRecordingHandler.Sanitizers.Add(new BodyRegexSanitizer("sanitized", ".*"));
            testRecordingHandler.Transforms.Clear();
            testRecordingHandler.AddSanitizerToRecording(recordingId, new GeneralRegexSanitizer("sanitized", ".*"));
            testRecordingHandler.SetDefaultExtensions(recordingId);
            var session = testRecordingHandler.RecordingSessions.First().Value;

            // check that the individual session had reset sanitizers
            Assert.Empty(session.ModifiableSession.AdditionalSanitizers);

            // stop the recording to clear out the session cache
            testRecordingHandler.StopRecording(recordingId);

            // then verify that the session level is NOT reset.
            Assert.Single(testRecordingHandler.Sanitizers);
            Assert.IsType<BodyRegexSanitizer>(testRecordingHandler.Sanitizers.First());
        }

        [Fact]
        public void TestResetExtensionsFailsWithActiveSessions()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            testRecordingHandler.StartRecording("recordingings/cool.json", httpContext.Response);
            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();

            var assertion = Assert.Throws<HttpException>(
                () => testRecordingHandler.SetDefaultExtensions()
            );

            Assert.StartsWith("There are a total of 1 active sessions. Remove these sessions before hitting Admin/Reset.", assertion.Message);
        }

        [Fact]
        public async Task TestInMemoryPurgesSucessfully()
        {
            var recordingHandler = TestHelpers.LoadRecordSessionIntoInMemoryStore("Test.RecordEntries/post_delete_get_content.json");
            var httpContext = new DefaultHttpContext();
            var key = recordingHandler.InMemorySessions.Keys.First();

            await recordingHandler.StartPlaybackAsync(key, httpContext.Response, Common.RecordingType.InMemory);
            var playbackSession = httpContext.Response.Headers["x-recording-id"];
            recordingHandler.StopPlayback(playbackSession, true);

            Assert.True(0 == recordingHandler.InMemorySessions.Count);
        }

        [Fact]
        public async Task TestInMemoryDoesntPurgeErroneously()
        {
            var recordingHandler = TestHelpers.LoadRecordSessionIntoInMemoryStore("Test.RecordEntries/post_delete_get_content.json");
            var httpContext = new DefaultHttpContext();
            var key = recordingHandler.InMemorySessions.Keys.First();

            await recordingHandler.StartPlaybackAsync(key, httpContext.Response, Common.RecordingType.InMemory);
            var playbackSession = httpContext.Response.Headers["x-recording-id"];
            recordingHandler.StopPlayback(playbackSession, false);

            Assert.True(1 == recordingHandler.InMemorySessions.Count);
        }

        [Fact]
        public async Task TestLoadOfAbsoluteRecording()
        {
            var tmpPath = Path.GetTempPath();
            var currentPath = Directory.GetCurrentDirectory();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = Path.Combine(currentPath, "Test.RecordEntries/oauth_request.json");

            var recordingHandler = new RecordingHandler(tmpPath);

            await recordingHandler.StartPlaybackAsync(pathToRecording, httpContext.Response);

            var playbackSession = recordingHandler.PlaybackSessions.First();
            var entry = playbackSession.Value.Session.Entries.First();

            Assert.Equal("https://login.microsoftonline.com/12345678-1234-1234-1234-123456789012/oauth2/v2.0/token", entry.RequestUri);
        }

        [Fact]
        public async Task TestLoadOfRelativeRecording()
        {
            var currentPath = Directory.GetCurrentDirectory();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = "Test.RecordEntries/oauth_request.json";
            var recordingHandler = new RecordingHandler(currentPath);

            await recordingHandler.StartPlaybackAsync(pathToRecording, httpContext.Response);

            var playbackSession = recordingHandler.PlaybackSessions.First();
            var entry = playbackSession.Value.Session.Entries.First();

            Assert.Equal("https://login.microsoftonline.com/12345678-1234-1234-1234-123456789012/oauth2/v2.0/token", entry.RequestUri);
        }

        [Fact]
        public void TestWriteAbsoluteRecording()
        {
            var tmpPath = Path.GetTempPath();
            var currentPath = Directory.GetCurrentDirectory();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = Path.Combine(currentPath, "recordings/oauth_request_new.json");
            var recordingHandler = new RecordingHandler(tmpPath);

            recordingHandler.StartRecording(pathToRecording, httpContext.Response);
            var sessionId = httpContext.Response.Headers["x-recording-id"].ToString();
            recordingHandler.StopRecording(sessionId);

            try
            {
                Assert.True(File.Exists(pathToRecording));
            }
            finally
            {
                File.Delete(pathToRecording);
            }
        }

        [Fact]
        public void TestWriteRelativeRecording()
        {
            var currentPath = Directory.GetCurrentDirectory();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = "recordings/oauth_request_new";
            var recordingHandler = new RecordingHandler(currentPath);
            var fullPathToRecording = Path.Combine(currentPath, pathToRecording) + ".json";

            recordingHandler.StartRecording(pathToRecording, httpContext.Response);
            var sessionId = httpContext.Response.Headers["x-recording-id"].ToString();
            recordingHandler.StopRecording(sessionId);


            try
            {
                Assert.True(File.Exists(fullPathToRecording));
            }
            finally
            {
                File.Delete(fullPathToRecording);
            }
        }

        [Fact]
        public async Task TestCanSkipRecordingRequestBody()
        {
            var currentPath = Directory.GetCurrentDirectory();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = "recordings/skip_body";
            var recordingHandler = new RecordingHandler(currentPath);
            var fullPathToRecording = Path.Combine(currentPath, pathToRecording) + ".json";

            recordingHandler.StartRecording(pathToRecording, httpContext.Response);
            var sessionId = httpContext.Response.Headers["x-recording-id"].ToString();

            CreateRecordModeRequest(httpContext, "request-body");

            var mockClient = new HttpClient(new MockHttpHandler());
            await recordingHandler.HandleRecordRequestAsync(sessionId, httpContext.Request, httpContext.Response, mockClient, mockClient);
            recordingHandler.StopRecording(sessionId);

            try
            {
                using var fileStream = File.Open(fullPathToRecording, FileMode.Open);
                using var doc = JsonDocument.Parse(fileStream);
                var record = RecordSession.Deserialize(doc.RootElement);
                var entry = record.Entries.First();
                Assert.Null(entry.Request.Body);
                Assert.Equal(MockHttpHandler.DefaultResponse, Encoding.UTF8.GetString(entry.Response.Body));
            }
            finally
            {
                File.Delete(fullPathToRecording);
            }
        }

        [Fact]
        public async Task TestCanSkipRecordingEntireRequestResponse()
        {
            var currentPath = Directory.GetCurrentDirectory();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = "recordings/skip_entry";
            var recordingHandler = new RecordingHandler(currentPath);
            var fullPathToRecording = Path.Combine(currentPath, pathToRecording) + ".json";

            recordingHandler.StartRecording(pathToRecording, httpContext.Response);
            var sessionId = httpContext.Response.Headers["x-recording-id"].ToString();

            CreateRecordModeRequest(httpContext, "request-response");

            var mockClient = new HttpClient(new MockHttpHandler());
            await recordingHandler.HandleRecordRequestAsync(sessionId, httpContext.Request, httpContext.Response, mockClient, mockClient);

            httpContext = new DefaultHttpContext();
            // send a second request that SHOULD be recorded
            CreateRecordModeRequest(httpContext);
            httpContext.Request.Headers.Remove("x-recording-skip");
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody("{ \"key\": \"value\" }");
            await recordingHandler.HandleRecordRequestAsync(sessionId, httpContext.Request, httpContext.Response, mockClient, mockClient);

            recordingHandler.StopRecording(sessionId);

            try
            {
                using var fileStream = File.Open(fullPathToRecording, FileMode.Open);
                using var doc = JsonDocument.Parse(fileStream);
                var record = RecordSession.Deserialize(doc.RootElement);
                Assert.Single(record.Entries);
                var entry = record.Entries.First();
                Assert.Equal("value", JsonDocument.Parse(entry.Request.Body).RootElement.GetProperty("key").GetString());
                Assert.Equal(MockHttpHandler.DefaultResponse, Encoding.UTF8.GetString(entry.Response.Body));
            }
            finally
            {
                File.Delete(fullPathToRecording);
            }
        }

        [Theory]
        [InlineData("invalid value")]
        [InlineData("")]
        [InlineData("request-body", "request-response")]
        public async Task TestInvalidRecordModeThrows(params string[] values)
        {
            var currentPath = Directory.GetCurrentDirectory();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = "recordings/invalid_record_mode";
            var recordingHandler = new RecordingHandler(currentPath);

            recordingHandler.StartRecording(pathToRecording, httpContext.Response);
            var sessionId = httpContext.Response.Headers["x-recording-id"].ToString();

            CreateRecordModeRequest(httpContext, new StringValues(values));

            var mockClient = new HttpClient(new MockHttpHandler());
            HttpException resultingException = await Assert.ThrowsAsync<HttpException>(
                async () => await recordingHandler.HandleRecordRequestAsync(sessionId, httpContext.Request, httpContext.Response, mockClient, mockClient)
            );
            Assert.Equal(HttpStatusCode.BadRequest, resultingException.StatusCode);
        }

        private static void CreateRecordModeRequest(DefaultHttpContext context, StringValues mode = default)
        {
            context.Request.Headers["x-recording-skip"] = mode;
            context.Request.Headers["x-recording-upstream-base-uri"] = "https://contoso.net";
            context.Request.ContentType = "application/json";
            context.Request.Method = "PUT";
            context.Request.Body = TestHelpers.GenerateStreamRequestBody("{ \"key\": \"value\" }");
            // content length must be set for the body to be parsed in SetMatcher
            context.Request.ContentLength = context.Request.Body.Length;
        }

        [Fact]
        public async Task TestLoadNonexistentAbsoluteRecording()
        {
            var tmpPath = Path.GetTempPath();
            var currentPath = Directory.GetCurrentDirectory();
            var httpContext = new DefaultHttpContext();
            var recordingPath = "Test.RecordEntries/oauth_request_wrong.json";
            var pathToRecording = Path.Combine(currentPath, recordingPath);

            var recordingHandler = new RecordingHandler(tmpPath);

            var resultingException = await Assert.ThrowsAsync<TestRecordingMismatchException>(
               async () => await recordingHandler.StartPlaybackAsync(pathToRecording, httpContext.Response)
            );
            Assert.Contains($"{recordingPath} does not exist", resultingException.Message);
        }

        [Fact]
        public async Task TestLoadNonexistentRelativeRecording()
        {
            var currentPath = Directory.GetCurrentDirectory();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = "Test.RecordEntries/oauth_request_wrong.json";

            var recordingHandler = new RecordingHandler(currentPath);

            var resultingException = await Assert.ThrowsAsync<TestRecordingMismatchException>(
               async () => await recordingHandler.StartPlaybackAsync(pathToRecording, httpContext.Response)
            );
            Assert.Contains($"{pathToRecording} does not exist", resultingException.Message);
        }

        [Fact]
        public void TestStopRecordingWithVariables()
        {
            var tmpPath = Path.GetTempPath();
            var startHttpContext = new DefaultHttpContext();
            var pathToRecording = "recordings/oauth_request_new.json";
            var recordingHandler = new RecordingHandler(tmpPath);
            var dict = new Dictionary<string, string>{
                { "key1","valueabc123" },
                { "key2", "value123abc" }
            };
            var endHttpContext = new DefaultHttpContext();

            recordingHandler.StartRecording(pathToRecording, startHttpContext.Response);
            var sessionId = startHttpContext.Response.Headers["x-recording-id"].ToString();

            recordingHandler.StopRecording(sessionId, variables: new SortedDictionary<string, string>(dict));
            var storedVariables = TestHelpers.LoadRecordSession(Path.Combine(tmpPath, pathToRecording)).Session.Variables;

            Assert.Equal(dict.Count, storedVariables.Count);

            foreach (var kvp in dict)
            {
                Assert.Equal(kvp.Value, storedVariables[kvp.Key]);
            }
        }

        [Fact]
        public void TestStopRecordingWithoutVariables()
        {
            var tmpPath = Path.GetTempPath();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = "recordings/oauth_request_new.json";
            var recordingHandler = new RecordingHandler(tmpPath);

            recordingHandler.StartRecording(pathToRecording, httpContext.Response);
            var sessionId = httpContext.Response.Headers["x-recording-id"].ToString();
            recordingHandler.StopRecording(sessionId, variables: new SortedDictionary<string, string>());

            var storedVariables = TestHelpers.LoadRecordSession(Path.Combine(tmpPath, pathToRecording)).Session.Variables;

            Assert.Empty(storedVariables);
        }

        [Fact]
        public void TestStopRecordingNullVariables()
        {
            var tmpPath = Path.GetTempPath();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = "recordings/oauth_request_new.json";
            var recordingHandler = new RecordingHandler(tmpPath);

            recordingHandler.StartRecording(pathToRecording, httpContext.Response);
            var sessionId = httpContext.Response.Headers["x-recording-id"].ToString();
            recordingHandler.StopRecording(sessionId, variables: null);

            var storedVariables = TestHelpers.LoadRecordSession(Path.Combine(tmpPath, pathToRecording)).Session.Variables;

            Assert.Empty(storedVariables);
        }

        [Fact]
        public async Task TestStartPlaybackWithVariables()
        {
            var httpContext = new DefaultHttpContext();
            var recordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            httpContext.Response.Body = new MemoryStream();

            await recordingHandler.StartPlaybackAsync("Test.RecordEntries/oauth_request_with_variables.json", httpContext.Response);

            Dictionary<string, string> results = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                TestHelpers.GenerateStringFromStream(httpContext.Response.Body)
            );

            Assert.Equal(2, results.Count);
            Assert.Equal("value1", results["key1"]);
            Assert.Equal("value2", results["key2"]);
        }

        [Fact]
        public async Task TestStartPlaybackWithoutVariables()
        {
            var startHttpContext = new DefaultHttpContext();
            var recordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());

            await recordingHandler.StartPlaybackAsync("Test.RecordEntries/oauth_request.json", startHttpContext.Response);
        }

        [Fact]
        public async Task CreateEntryUsesAbsoluteUri()
        {
            var request = new DefaultHttpContext().Request;
            var uri = new Uri("http://contoso.net/my cool directory");
            request.Host = new HostString(uri.Host);
            request.Path = uri.PathAndQuery;
            request.Headers["x-recording-upstream-base-uri"] = uri.AbsoluteUri;

            var entry = await RecordingHandler.CreateEntryAsync(request);
            Assert.Equal(uri.AbsoluteUri, entry.RequestUri);
        }


        [Theory]
        [InlineData("Content-Type:application/json; odata=minimalmetadata; streaming=true", "Accept:application/json;odata=minimalmetadata")]
        [InlineData("Content-MD5:<ContentHash>", "x-ms-version:2019-02-02", "RequestMethod:POST", "Connection:keep-alive")]
        [InlineData("Content-Encoding:utf-8", "x-ms-version:2019-02-02", "RequestMethod:POST", "Content-Length:50")]
        public void TestCreateUpstreamRequestIncludesExpectedHeaders(params string[] incomingHeaders)
        {
            var requestContext = GenerateHttpRequestContext(incomingHeaders);
            var recordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var upstreamRequestContext = GenerateHttpRequestContext(incomingHeaders);

            var output = recordingHandler.CreateUpstreamRequest(upstreamRequestContext.Request, new byte[] { });

            // iterate across the set we know about and confirm that GenerateUpstreamRequest worked properly!
            var setOfHeaders = GenerateHeaderValuesTuples(incomingHeaders);
            {
                foreach (var headerTuple in setOfHeaders)
                {
                    var inContent = false;
                    var inStandard = false;

                    try
                    {
                        inContent = output.Headers.Contains(headerTuple.Item1);
                    }
                    catch (Exception) { }

                    try
                    {
                        inStandard = output.Content.Headers.Contains(headerTuple.Item1);
                    }
                    catch (Exception) { }

                    Assert.True(inContent || inStandard);
                }
            }
        }

        [Theory]
        [InlineData("{ \"HandleRedirects\": \"true\"}", true)]
        [InlineData("{ \"HandleRedirects\": \"false\"}", false)]
        [InlineData("{ \"HandleRedirects\": \"1\"}", true)]
        [InlineData("{ \"HandleRedirects\": \"0\"}", false)]
        [InlineData("{ \"HandleRedirects\": \"True\"}", true)]
        [InlineData("{ \"HandleRedirects\": \"False\"}", false)]
        [InlineData("{ \"HandleRedirects\": \"TRUE\"}", true)]
        [InlineData("{ \"HandleRedirects\": \"FALSE\"}", false)]
        [InlineData("{ \"HandleRedirects\": true }", true)]
        [InlineData("{ \"HandleRedirects\": false }", false)]
        [InlineData("{ \"HandleRedirects\": 1 }", true)]
        [InlineData("{ \"HandleRedirects\": 0 }", false)]
        public void TestSetRecordingOptionsHandlesValidInputs(string body, bool expectedSetting)
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            Dictionary<string, object> inputBody = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);

            testRecordingHandler.SetRecordingOptions(inputBody);

            Assert.Equal(expectedSetting, testRecordingHandler.HandleRedirects);
        }

        [Theory]
        [InlineData("{ \"HandleRedirects\": \"anotherkey\"}", "The value of key \"HandleRedirects\" MUST be castable to a valid boolean value.")]
        [InlineData("{ \"HandleRedirects\": \"true2\"}", "The value of key \"HandleRedirects\" MUST be castable to a valid boolean value.")]
        [InlineData("{}", "At least one key is expected in the body being passed to SetRecordingOptions.")]
        [InlineData(null, "When setting recording options, the request body is expected to be non-null and of type Dictionary<string, string>.")]
        public void TestSetRecordingOptionsThrowsOnInvalidInputs(string body, string errorText)
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();

            Dictionary<string, object> inputBody = null;
            if (!string.IsNullOrWhiteSpace(body))
            {
                inputBody = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
            }

            var assertion = Assert.Throws<HttpException>(
               () => testRecordingHandler.SetRecordingOptions(inputBody)
            );

            Assert.True(assertion.StatusCode.Equals(HttpStatusCode.BadRequest));
            Assert.Contains(errorText, assertion.Message);
        }

        [Theory]
        [InlineData("awesomehost.com")]
        [InlineData("")]
        public void TestRecordMaintainsUpstreamOverrideHostHeader(string upstreamHostHeaderValue)
        {
            var httpContext = new DefaultHttpContext();
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());

            testRecordingHandler.StartRecording("hello.json", httpContext.Response);

            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();

            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(String.Empty);
            httpContext.Request.ContentLength = 0;
            httpContext.Request.Headers["x-recording-id"] = recordingId;
            httpContext.Request.Headers["x-recording-upstream-base-uri"] = "http://example.org";

            if (!String.IsNullOrWhiteSpace(upstreamHostHeaderValue))
            {
                httpContext.Request.Headers["x-recording-upstream-host-header"] = upstreamHostHeaderValue;
            }

            httpContext.Request.Method = "GET";

            var upstreamRequest = testRecordingHandler.CreateUpstreamRequest(httpContext.Request, new byte[] { });

            if (!String.IsNullOrWhiteSpace(upstreamHostHeaderValue))
            {
                Assert.Equal(upstreamHostHeaderValue, upstreamRequest.Headers.Host);
            }
            else
            {
                Assert.Null(upstreamRequest.Headers.Host);
            }
        }
    }

    internal class MockHttpHandler : HttpMessageHandler
    {
        public const string DefaultResponse = "default response";

        public MockHttpHandler()
        {
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(DefaultResponse)
            });
        }
    }
}
