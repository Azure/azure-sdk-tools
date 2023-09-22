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
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using Xunit;
using Azure.Core;
using System.Runtime.InteropServices;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Store;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class RecordingHandlerTests
    {
        #region helpers and private test fields
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

        public static JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

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
        #endregion

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
        public async Task TestResetTargetsRecordingOnly()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            await testRecordingHandler.StartRecordingAsync("recordingings/cool.json", httpContext.Response);
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
            Assert.Empty(session.AdditionalSanitizers);
            Assert.Empty(session.AdditionalTransforms);
            Assert.Null(session.CustomMatcher);
        }

        [Fact]
        public async Task TestResetTargetsSessionOnly()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            await testRecordingHandler.StartRecordingAsync("recordingings/cool.json", httpContext.Response);
            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();

            testRecordingHandler.Sanitizers.Clear();
            testRecordingHandler.Sanitizers.Add(new BodyRegexSanitizer("sanitized", ".*"));
            testRecordingHandler.Transforms.Clear();
            testRecordingHandler.AddSanitizerToRecording(recordingId, new GeneralRegexSanitizer("sanitized", ".*"));
            testRecordingHandler.SetDefaultExtensions(recordingId);
            var session = testRecordingHandler.RecordingSessions.First().Value;

            // check that the individual session had reset sanitizers
            Assert.Empty(session.AdditionalSanitizers);

            // stop the recording to clear out the session cache
            testRecordingHandler.StopRecording(recordingId);

            // then verify that the session level is NOT reset.
            Assert.Single(testRecordingHandler.Sanitizers);
            Assert.IsType<BodyRegexSanitizer>(testRecordingHandler.Sanitizers.First());
        }

        [Fact]
        public async Task TestResetExtensionsFailsWithActiveSessions()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var httpContext = new DefaultHttpContext();
            await testRecordingHandler.StartRecordingAsync("recordingings/cool.json", httpContext.Response);
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
        public async Task TestWriteAbsoluteRecording()
        {
            var tmpPath = Path.GetTempPath();
            var currentPath = Directory.GetCurrentDirectory();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = Path.Combine(currentPath, "recordings/oauth_request_new.json");
            var recordingHandler = new RecordingHandler(tmpPath);

            await recordingHandler.StartRecordingAsync(pathToRecording, httpContext.Response);
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
        public async Task TestWriteRelativeRecording()
        {
            var currentPath = Directory.GetCurrentDirectory();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = "recordings/oauth_request_new";
            var recordingHandler = new RecordingHandler(currentPath);
            var fullPathToRecording = Path.Combine(currentPath, pathToRecording) + ".json";

            await recordingHandler.StartRecordingAsync(pathToRecording, httpContext.Response);
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
            var mockClient = new HttpClient(new MockHttpHandler());
            var recordingHandler = new RecordingHandler(currentPath)
            {
                RedirectableClient = mockClient,
                RedirectlessClient = mockClient
            };
            var fullPathToRecording = Path.Combine(currentPath, pathToRecording) + ".json";

            await recordingHandler.StartRecordingAsync(pathToRecording, httpContext.Response);
            var sessionId = httpContext.Response.Headers["x-recording-id"].ToString();

            CreateRecordModeRequest(httpContext, "request-body");

            await recordingHandler.HandleRecordRequestAsync(sessionId, httpContext.Request, httpContext.Response);
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

            var mockClient = new HttpClient(new MockHttpHandler());
            var recordingHandler = new RecordingHandler(currentPath)
            {
                RedirectableClient = mockClient,
                RedirectlessClient = mockClient
            };


            var fullPathToRecording = Path.Combine(currentPath, pathToRecording) + ".json";

            await recordingHandler.StartRecordingAsync(pathToRecording, httpContext.Response);
            var sessionId = httpContext.Response.Headers["x-recording-id"].ToString();

            CreateRecordModeRequest(httpContext, "request-response");

            await recordingHandler.HandleRecordRequestAsync(sessionId, httpContext.Request, httpContext.Response);

            httpContext = new DefaultHttpContext();
            // send a second request that SHOULD be recorded
            CreateRecordModeRequest(httpContext);
            httpContext.Request.Headers.Remove("x-recording-skip");
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody("{ \"key\": \"value\" }");
            await recordingHandler.HandleRecordRequestAsync(sessionId, httpContext.Request, httpContext.Response);

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
            var mockClient = new HttpClient(new MockHttpHandler());
            var recordingHandler = new RecordingHandler(currentPath)
            {
                RedirectableClient = mockClient,
                RedirectlessClient = mockClient
            };

            await recordingHandler.StartRecordingAsync(pathToRecording, httpContext.Response);
            var sessionId = httpContext.Response.Headers["x-recording-id"].ToString();

            CreateRecordModeRequest(httpContext, new StringValues(values));

            HttpException resultingException = await Assert.ThrowsAsync<HttpException>(
                async () => await recordingHandler.HandleRecordRequestAsync(sessionId, httpContext.Request, httpContext.Response)
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
        public async Task TestStopRecordingWithVariables()
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

            await recordingHandler.StartRecordingAsync(pathToRecording, startHttpContext.Response);
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
        public async Task TestStopRecordingWithoutVariables()
        {
            var tmpPath = Path.GetTempPath();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = "recordings/oauth_request_new.json";
            var recordingHandler = new RecordingHandler(tmpPath);

            await recordingHandler.StartRecordingAsync(pathToRecording, httpContext.Response);
            var sessionId = httpContext.Response.Headers["x-recording-id"].ToString();
            recordingHandler.StopRecording(sessionId, variables: new SortedDictionary<string, string>());

            var storedVariables = TestHelpers.LoadRecordSession(Path.Combine(tmpPath, pathToRecording)).Session.Variables;

            Assert.Empty(storedVariables);
        }

        [Fact]
        public async Task TestStopRecordingNullVariables()
        {
            var tmpPath = Path.GetTempPath();
            var httpContext = new DefaultHttpContext();
            var pathToRecording = "recordings/oauth_request_new.json";
            var recordingHandler = new RecordingHandler(tmpPath);

            await recordingHandler.StartRecordingAsync(pathToRecording, httpContext.Response);
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

            var entry = (await RecordingHandler.CreateEntryAsync(request)).Item1;
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
        [InlineData("awesomehost.com")]
        [InlineData("")]
        public async Task TestRecordMaintainsUpstreamOverrideHostHeader(string upstreamHostHeaderValue)
        {
            var httpContext = new DefaultHttpContext();
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());

            await testRecordingHandler.StartRecordingAsync("hello.json", httpContext.Response);

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

        [Fact]
        public async Task TestRecordWithGZippedContent()
        {
            var httpContext = new DefaultHttpContext();
            var bodyBytes = Encoding.UTF8.GetBytes("{\"hello\":\"world\"}");
            var mockClient = new HttpClient(new MockHttpHandler(bodyBytes, "application/json", "gzip"));
            var path = Directory.GetCurrentDirectory();
            var recordingHandler = new RecordingHandler(path)
            {
                RedirectableClient = mockClient,
                RedirectlessClient = mockClient
            };

            var relativePath = "recordings/gzip";
            var fullPathToRecording = Path.Combine(path, relativePath) + ".json";

            await recordingHandler.StartRecordingAsync(relativePath, httpContext.Response);

            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();

            httpContext.Request.ContentType = "application/json";
            httpContext.Request.Headers["Content-Encoding"] = "gzip";
            httpContext.Request.ContentLength = 0;
            httpContext.Request.Headers["x-recording-id"] = recordingId;
            httpContext.Request.Headers["x-recording-upstream-base-uri"] = "http://example.org";
            httpContext.Request.Method = "GET";
            httpContext.Request.Body = new MemoryStream(CompressionUtilities.CompressBody(bodyBytes, httpContext.Request.Headers));

            await recordingHandler.HandleRecordRequestAsync(recordingId, httpContext.Request, httpContext.Response);
            recordingHandler.StopRecording(recordingId);

            try
            {
                using var fileStream = File.Open(fullPathToRecording, FileMode.Open);
                using var doc = JsonDocument.Parse(fileStream);
                var record = RecordSession.Deserialize(doc.RootElement);
                var entry = record.Entries.First();
                Assert.Equal("{\"hello\":\"world\"}", Encoding.UTF8.GetString(entry.Request.Body));
                Assert.Equal("{\"hello\":\"world\"}", Encoding.UTF8.GetString(entry.Response.Body));
            }
            finally
            {
                File.Delete(fullPathToRecording);
            }
        }

        [Fact]
        public async Task RecordingHandlerIsThreadSafe()
        {
            var bodyBytes = Encoding.UTF8.GetBytes("{\"hello\":\"world\"}");
            var mockClient = new HttpClient(new MockHttpHandler(bodyBytes));
            var path = Directory.GetCurrentDirectory();
            var recordingHandler = new RecordingHandler(path)
            {
                RedirectableClient = mockClient,
                RedirectlessClient = mockClient
            };

            var httpContext = new DefaultHttpContext();
            await recordingHandler.StartRecordingAsync("threadSafe", httpContext.Response);
            var recordingId = httpContext.Response.Headers["x-recording-id"].ToString();

            var requests = new List<Task>();
            var requestCount = 100;

            for (int i = 0; i < requestCount; i++)
            {
                httpContext = new DefaultHttpContext();
                httpContext.Request.ContentType = "application/json";
                httpContext.Request.ContentLength = 0;
                httpContext.Request.Headers["x-recording-id"] = recordingId;
                httpContext.Request.Headers["x-recording-upstream-base-uri"] = "http://example.org";
                httpContext.Request.Method = "GET";
                httpContext.Request.Body = new MemoryStream(bodyBytes);
                requests.Add(recordingHandler.HandleRecordRequestAsync(recordingId, httpContext.Request, httpContext.Response));
            }

            await Task.WhenAll(requests);
            var session = recordingHandler.RecordingSessions.First().Value;
            Assert.Equal(requestCount, session.Session.Entries.Count);
        }

        #region ByteManipulation

        private const string longBody = @"Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt
            ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut
            aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore
            eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt
            mollit anim id est laborum.";

        [Theory]
        [InlineData("", 1)]
        [InlineData("small body", 5)]
        [InlineData("this is a body", 3)]
        [InlineData("This is a little bit longer of a body that we are dividing in 2", 2)]
        [InlineData(longBody, 5)]
        [InlineData(longBody, 1)]
        [InlineData(longBody, 10)]
        public void TestGetBatches(string input, int batchCount)
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var bodyData = Encoding.UTF8.GetBytes(input);

            var chunks = testRecordingHandler.GetBatches(bodyData, batchCount);

            int bodyPosition = 0;

            // ensure that all bytes are accounted for across the batches
            foreach(var chunk in chunks)
            {
                for (int j = 0; j < chunk.Length; j++)
                {
                    Assert.Equal(chunk[j], bodyData[bodyPosition]);
                    bodyPosition++;
                }
            }

            Assert.Equal(bodyPosition, bodyData.Length);
        }

        #endregion

        #region SetRecordingOptions
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
        public void TestSetRecordingOptionsHandlesValidRedirectSetting(string body, bool expectedSetting)
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            Dictionary<string, object> inputBody = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);

            testRecordingHandler.SetRecordingOptions(inputBody);

            Assert.Equal(expectedSetting, testRecordingHandler.HandleRedirects);
        }

        [Theory]
        [InlineData("{ \"HandleRedirects\": \"anotherkey\"}", "The value of key \"HandleRedirects\" MUST be castable to a valid boolean value.")]
        [InlineData("{ \"HandleRedirects\": \"true2\"}", "The value of key \"HandleRedirects\" MUST be castable to a valid boolean value.")]
        [InlineData("{}", "At least one key is expected in the body being passed to SetRecordingOptions.")]
        [InlineData(null, "When setting recording options, the request body is expected to be non-null and of type Dictionary<string, string>.")]
        public void TestSetRecordingOptionsThrowsOnInvalidRedirectSetting(string body, string errorText)
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());

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
        [InlineData("hellothere", "generalkenobi")]
        [InlineData("", "")]
        public void TestSetRecordingOptionsHandlesValidContextDirectory(params string[] relativePaths)
        {
            var relativePath = Path.Combine(relativePaths);
            var testDirectory = Path.GetTempPath();

            RecordingHandler testRecordingHandler = new RecordingHandler(testDirectory);
            testDirectory = Path.Combine(testDirectory, relativePath);
            var body = $"{{ \"ContextDirectory\": \"{testDirectory.Replace("\\", "/")}\"}}";

            var httpContext = new DefaultHttpContext();
            Dictionary<string, object> inputBody = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);

            testRecordingHandler.SetRecordingOptions(inputBody);

            Assert.Equal(new Uri(testDirectory), new Uri(testRecordingHandler.ContextDirectory));
        }

        [Theory]
        [InlineData("{ \"ContextDirectory\": \":/repo/\0\"}", "Unable set proxy context to target directory")]
        public void TestSetRecordingOptionsThrowsOnInvalidContextDirectory(string body, string errorText)
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
            Assert.StartsWith(errorText, assertion.Message);
        }


        [Theory]
        [InlineData("{ \"AssetsStore\": \"NullStore\"}")]
        [InlineData("{ \"AssetsStore\": \"GitStore\"}")]
        [InlineData("{ \"AssetsStore\": \"Azure.Sdk.Tools.TestProxy.Store.GitStore\"}")]
        [InlineData("{ \"AssetsStore\": \"Azure.Sdk.Tools.TestProxy.Store.NullStore\"}")]
        public void TestSetRecordingOptionsHandlesValidStoreTypes(string body)
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            testRecordingHandler.Store = null;
            Dictionary<string, object> inputBody = null;
            if (!string.IsNullOrWhiteSpace(body))
            {
                inputBody = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
            }

            testRecordingHandler.SetRecordingOptions(inputBody);

            Assert.NotNull(testRecordingHandler.Store);
        }

        [Theory]
        [InlineData("{ \"AssetsStore\": \"NonExistent\"}", "Unable to load the specified IAssetStore class NonExistent.")]
        [InlineData("{ \"AssetsStore\": \"\"}", "Users must provide a valid value when providing the key \"AssetsStore\"")]
        [InlineData("{ \"AssetsStore\": \"GitAssetsConfiguration\"}", "Unable to create an instance of type GitAssetsConfiguration")]
        public void TestSetRecordingOptionsThrowsOnInvalidStoreTypes(string body, string errorText)
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            Dictionary<string, object> inputBody = null;
            if (!string.IsNullOrWhiteSpace(body))
            {
                inputBody = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
            }

            var assertion = Assert.Throws<HttpException>(
               () => testRecordingHandler.SetRecordingOptions(inputBody)
            );

            Assert.True(assertion.StatusCode.Equals(HttpStatusCode.BadRequest));
            Assert.StartsWith(errorText, assertion.Message);
        }

        [Fact]
        public void TestSetRecordingOptionsValidTlsCert()
        {
            var certValue = TestHelpers.GetValueFromCertificateFile("test_public-key-only_pem").Replace(Environment.NewLine, "");
            var inputObj = string.Format("{{\"Transport\": {{\"TLSValidationCert\": \"{0}\"}}}}", certValue);
            var testRecordingHandler = new RecordingHandler(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString()));
            var inputBody = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(inputObj, SerializerOptions);

            testRecordingHandler.SetRecordingOptions(inputBody, null);
        }

        [Fact]
        public void TestSetRecordingOptionsMultipleCertOptions()
        {
            var certValue = TestHelpers.GetValueFromCertificateFile("test_public-key-only_pem").Replace(Environment.NewLine, "");
            var pemKey = TestHelpers.GetValueFromCertificateFile("test_pem_key").Replace(Environment.NewLine, "");
            var pemValue = TestHelpers.GetValueFromCertificateFile("test_pem_value").Replace(Environment.NewLine, "");
            var inputObj = string.Format("{{\"Transport\": {{\"TLSValidationCert\": \"{0}\", \"Certificates\": [ {{ \"PemValue\": \"{1}\", \"PemKey\": \"{2}\" }}]}}}}", certValue, pemValue, pemKey);
            var testRecordingHandler = new RecordingHandler(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString()));
            var inputBody = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(inputObj, SerializerOptions);

            testRecordingHandler.SetRecordingOptions(inputBody, null);
        }

        [Theory]
        [InlineData("{{\"Transport\": {{\"Certificates\": [ {{ \"PemValue\": \"{0}\", \"PemKey\": \"{1}\" }}, {{ \"PemValue\": \"{0}\", \"PemKey\": \"{1}\" }}]}}}}")]
        [InlineData("{{\"Transport\": {{\"Certificates\": [ {{ \"PemValue\": \"{0}\", \"PemKey\": \"{1}\" }}]}}}}")]
        [InlineData("{{\"Transport\": {{\"Certificates\": []}}}}")]
        public void TestSetRecordingOptionsValidTransportSessionLevel(string body)
        {
            var pemKey = TestHelpers.GetValueFromCertificateFile("test_pem_key").Replace(Environment.NewLine, "");
            var pemValue = TestHelpers.GetValueFromCertificateFile("test_pem_value").Replace(Environment.NewLine, "");
            var inputObj = string.Format(body, pemValue, pemKey);
            var inputBody = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(inputObj, SerializerOptions);

            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            testRecordingHandler.SetRecordingOptions(inputBody, null);
        }

        [Theory]
        [InlineData("{{\"Transport\": {{\"Certificates\": [ {{ \"PemValue\": \"{0}\", \"PemKey\": \"{1}\" }}, {{ \"PemValue\": \"{0}\", \"PemKey\": \"{1}\" }}]}}}}")]
        [InlineData("{{\"Transport\": {{\"Certificates\": [ {{ \"PemValue\": \"{0}\", \"PemKey\": \"{1}\" }}]}}}}")]
        [InlineData("{{\"Transport\": {{\"Certificates\": []}}}}")]
        public async Task TestSetRecordingOptionsValidTransportRecordingLevel(string body)
        {
            var pemKey = TestHelpers.GetValueFromCertificateFile("test_pem_key").Replace(Environment.NewLine, "");
            var pemValue = TestHelpers.GetValueFromCertificateFile("test_pem_value").Replace(Environment.NewLine, "");
            var testRecordingHandler = new RecordingHandler(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString()));
            var inputObj = string.Format(body, pemValue, pemKey);
            var inputBody = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(inputObj, SerializerOptions);

            HttpContext context = new DefaultHttpContext();
            await testRecordingHandler.StartRecordingAsync("TestSetRecordingOptionsInValidTransportRecordingLevel.json", context.Response);
            var recordingId = context.Response.Headers["x-recording-id"].ToString();

            testRecordingHandler.SetRecordingOptions(inputBody, recordingId);
        }

        [Theory]
        [InlineData("{{\"Transport\": {{\"Certificates\": [ {{ \"PemValue\": \"{0}\", \"PemKey\": \"badkey\" }}]}}}}")]
        [InlineData("{{\"Transport\": {{\"Certificates\": [ {{ \"PemValue\": \"badvalue\", \"PemKey\": \"{1}\" }}]}}}}")]
        [InlineData("{{\"Transport\": {{\"Certificates\": [ {{ \"PemValue\": \"badvalue\" }}]}}}}")]
        [InlineData("{{\"Transport\": {{\"Certificates\": [ {{ \"PemKey\": \"{1}\" }}]}}}}")]
        public void TestSetRecordingOptionsInValidTransportSessionLevel(string body)
        {
            var pemKey = TestHelpers.GetValueFromCertificateFile("test_pem_key").Replace(Environment.NewLine, "");
            var pemValue = TestHelpers.GetValueFromCertificateFile("test_pem_value").Replace(Environment.NewLine, "");
            var testRecordingHandler = new RecordingHandler(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString()));
            var inputObj = string.Format(body, pemValue, pemKey);
            var inputBody = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(inputObj, SerializerOptions);

            var assertion = Assert.Throws<HttpException>(
               () => testRecordingHandler.SetRecordingOptions(inputBody)
            );
            Assert.Contains("Unable to instantiate a new X509 certificate from the provided value and key.", assertion.Message);
        }

        [Theory]
        [InlineData("{{\"Transport\": {{\"Certificates\": [ {{ \"PemValue\": \"{0}\", \"PemKey\": \"badkey\" }}]}}}}")]
        [InlineData("{{\"Transport\": {{\"Certificates\": [ {{ \"PemValue\": \"badvalue\", \"PemKey\": \"{1}\" }}]}}}}")]
        [InlineData("{{\"Transport\": {{\"Certificates\": [ {{ \"PemValue\": \"badvalue\" }}]}}}}")]
        [InlineData("{{\"Transport\": {{\"Certificates\": [ {{ \"PemKey\": \"{1}\" }}]}}}}")]
        public async Task TestSetRecordingOptionsInvalidTransportRecordingLevel(string body)
        {
            var pemKey = TestHelpers.GetValueFromCertificateFile("test_pem_key").Replace(Environment.NewLine, "");
            var pemValue = TestHelpers.GetValueFromCertificateFile("test_pem_value").Replace(Environment.NewLine, "");
            var testRecordingHandler = new RecordingHandler(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString()));
            var inputObj = string.Format(body, pemValue, pemKey);
            var inputBody = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(inputObj, SerializerOptions);

            HttpContext context = new DefaultHttpContext();
            await testRecordingHandler.StartRecordingAsync("TestSetRecordingOptionsInValidTransportRecordingLevel.json", context.Response);
            var recordingId = context.Response.Headers["x-recording-id"].ToString();

            var assertion = Assert.Throws<HttpException>(
               () => testRecordingHandler.SetRecordingOptions(inputBody, recordingId)
            );
            Assert.Contains("Unable to instantiate a new X509 certificate from the provided value and key.", assertion.Message);
        }


        [Fact]
        public void TestSetRecordingOptionsTransportWithTLSCert()
        {
            var certValue = TestHelpers.GetValueFromCertificateFile("test_public-key-only_pem").Replace(Environment.NewLine, "");
            var pemKey = TestHelpers.GetValueFromCertificateFile("test_pem_key").Replace(Environment.NewLine, "");
            var pemValue = TestHelpers.GetValueFromCertificateFile("test_pem_value").Replace(Environment.NewLine, "");
            var inputObj = string.Format("{{\"Transport\": {{\"TLSValidationCert\": \"{0}\", \"TLSValidationCertHost\":\"azure.blobs.windows.net\", \"Certificates\": [ {{ \"PemValue\": \"{1}\", \"PemKey\": \"{2}\" }}]}}}}", certValue, pemValue, pemKey);

            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var inputBody = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(inputObj, SerializerOptions);

            testRecordingHandler.SetRecordingOptions(inputBody);
        }

        [Fact]
        public void TestSetRecordingOptionsInValidTransportWithTLSCert()
        {
            var certValue = TestHelpers.GetValueFromCertificateFile("test_public-key-only_pem").Replace(Environment.NewLine, "");
            var pemKey = TestHelpers.GetValueFromCertificateFile("test_pem_key").Replace(Environment.NewLine, "");
            var pemValue = TestHelpers.GetValueFromCertificateFile("test_pem_value").Replace(Environment.NewLine, "");
            var inputObj = string.Format("{{\"Transport\": {{\"TLSValidationCert\": \"hello-there\", \"Certificates\": [ {{ \"PemValue\": \"{0}\", \"PemKey\": \"{1}\" }}]}}}}", pemValue, pemKey);

            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var inputBody = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(inputObj, SerializerOptions);

            var assertion = Assert.Throws<HttpException>(
               () => testRecordingHandler.SetRecordingOptions(inputBody)
            );

            Assert.StartsWith("Unable to instantiate a valid cert from the value provided in Transport settings key", assertion.Message);
            Assert.Contains("The certificate is missing the public key", assertion.Message);
        }
        #endregion
    }

    public sealed class IgnoreOnLinuxFact : FactAttribute
    {
        public IgnoreOnLinuxFact()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Skip = "Ignore on Linux.";
            }
        }
    }


    internal class MockHttpHandler : HttpMessageHandler
    {
        public const string DefaultResponse = "default response";
        private readonly byte[] _responseContent;
        private readonly string _contentType;
        private readonly string _contentEncoding;

        public MockHttpHandler(byte[] responseContent = default, string contentType = default, string contentEncoding = default)
        {
            _responseContent = responseContent ?? Encoding.UTF8.GetBytes(DefaultResponse);
            _contentType = contentType ?? "application/json";
            _contentEncoding = contentEncoding;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // should not throw as stream should not be disposed
            var content = await request.Content.ReadAsStringAsync();
            Assert.NotEmpty(content);
            var response = new HttpResponseMessage(HttpStatusCode.OK);

            // simulate some IO
            await Task.Delay(100, cancellationToken);

            // we need to set the content before the content headers as otherwise they will be cleared out after setting content.
            if (_contentEncoding == "gzip")
            {
                response.Content = new ByteArrayContent(CompressionUtilities.CompressBodyCore(_responseContent, _contentEncoding));
            }
            else
            {
                response.Content = new ByteArrayContent(_responseContent);
            }

            response.Content.Headers.ContentType = new MediaTypeHeaderValue(_contentType);
            if (_contentEncoding != null)
            {
                response.Content.Headers.ContentEncoding.Add(_contentEncoding);
            }

            return response;
        }
    }
}
