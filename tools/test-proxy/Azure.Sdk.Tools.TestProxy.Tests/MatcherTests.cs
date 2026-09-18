using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Matchers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class MatcherTests
    {
        public BodilessMatcher BodilessMatcher = new BodilessMatcher();
        public HeaderlessMatcher HeaderlessMatcher = new HeaderlessMatcher();
        public RecordMatcher RecordMatcher = new RecordMatcher();
        private readonly ITestOutputHelper _output;

        public MatcherTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void FindMatchOnlyComparesPlausibleCandidates()
        {
            var matcher = new CountingRecordMatcher();
            var request = CreateMatchEntry("https://example.org/match", Core.RequestMethod.Get);
            var wrongMethod = CreateMatchEntry(request.RequestUri, Core.RequestMethod.Post);
            var wrongUri = CreateMatchEntry("https://example.org/other", request.RequestMethod);
            var wrongHeader = CreateMatchEntry(request.RequestUri, request.RequestMethod);
            wrongHeader.Request.Headers["x-match"] = new[] { "different" };
            var wrongBody = CreateMatchEntry(request.RequestUri, request.RequestMethod);
            wrongBody.Request.Body = Encoding.UTF8.GetBytes("different");
            var matched = CreateMatchEntry(request.RequestUri, request.RequestMethod);
            var duplicate = CreateMatchEntry(request.RequestUri, request.RequestMethod);

            Assert.Same(matched, matcher.FindMatch(request, new[] { wrongMethod, wrongUri, wrongHeader, wrongBody, matched, duplicate }));
            Assert.Equal(3, matcher.HeaderComparisons);
            Assert.Equal(2, matcher.BodyComparisons);
        }

        [Theory]
        [InlineData("http")]
        [InlineData("https")]
        public void FindMatchPreservesTrack1Matching(string scheme)
        {
            var matcher = new CountingRecordMatcher();
            matcher.IgnoredQueryParameters.Add("signature");
            var request = CreateMatchEntry($"{scheme}://example.org/match?signature=current", Core.RequestMethod.Get);
            var recorded = new RecordEntry
            {
                IsTrack1Recording = true,
                RequestUri = "/match?signature=recorded",
                RequestMethod = request.RequestMethod
            };

            Assert.Same(recorded, matcher.FindMatch(request, new[] { recorded }));
            Assert.Equal(0, matcher.HeaderComparisons);
            Assert.Equal(0, matcher.BodyComparisons);
        }

        [Fact]
        public void FindMatchRanksAllEntriesWhenNoExactMatch()
        {
            var request = CreateMatchEntry("https://example.org/match", Core.RequestMethod.Get);
            var sameUri = CreateMatchEntry(request.RequestUri, request.RequestMethod);
            sameUri.Request.Headers["x-match"] = new[] { "different" };
            sameUri.Request.Body = Encoding.UTF8.GetBytes("different");
            var closest = CreateMatchEntry("https://example.org/closest", request.RequestMethod);
            var tied = CreateMatchEntry("https://example.org/tied", request.RequestMethod);

            var exception = Assert.Throws<TestRecordingMismatchException>(() =>
                RecordMatcher.FindMatch(request, new[] { sameUri, closest, tied }));

            Assert.Contains("record  <https://example.org/closest>", exception.Message);
            Assert.DoesNotContain("values differ", exception.Message);
            Assert.DoesNotContain("bodies do not match", exception.Message);
            Assert.Contains("0: https://example.org/match", exception.Message);
            Assert.Contains("2: https://example.org/tied", exception.Message);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void HeaderComparisonMatchesDiagnosticScores(bool differentComparers, bool caseSensitiveExclusions)
        {
            var headers = new SortedDictionary<string, string[]>(differentComparers ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase)
            {
                ["Accept"] = new[] { "application/json,text/plain" },
                ["Content-Type"] = new[] { "multipart/mixed;boundary=request" },
                ["x-value"] = new[] { "request" },
                ["x-ignored"] = new[] { "request" },
                ["x-excluded"] = new[] { "request" },
                ["x-request-only"] = new[] { "request" }
            };
            var recorded = new SortedDictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Accept"] = new[] { "application/json, text/plain" },
                ["Content-Type"] = new[] { "multipart/mixed; boundary=record" },
                ["X-Value"] = new[] { "record" },
                ["X-Ignored"] = new[] { "record" },
                ["X-Excluded"] = new[] { "record" },
                ["x-record-only"] = new[] { "record" }
            };
            var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "x-ignored" };
            var excluded = new HashSet<string>(caseSensitiveExclusions ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase) { "x-excluded" };
            var description = new StringBuilder();

            int diagnosticScore = RecordMatcher.CompareHeaderDictionaries(headers, recorded, ignored, excluded, description);
            int score = RecordMatcher.CompareHeaderDictionaries(headers, recorded, ignored, excluded);

            Assert.Equal(caseSensitiveExclusions ? 4 : 3, score);
            Assert.Equal(diagnosticScore, score);
            Assert.Equal(6, headers.Count);
            Assert.Equal(6, recorded.Count);
            Assert.Equal("application/json,text/plain", headers["Accept"][0]);
        }

        [Theory]
        [InlineData("{\"first\":1,\"second\":2}", "{\"second\":2,\"first\":1}", true)]
        [InlineData("{\"number\":1}", "{\"number\":1.0}", true)]
        [InlineData("{\"number\":1e1}", "{\"number\":10}", true)]
        [InlineData("{\"value\":1,\"value\":2}", "{\"value\":2}", true)]
        [InlineData("{\"value\":1,\"value\":2}", "{\"value\":1}", false)]
        [InlineData("{\"Value\":1}", "{\"value\":1}", false)]
        [InlineData("[1,{\"values\":[true,null,\"text\"]}]", "[1.0,{\"values\":[true,null,\"text\"]}]", true)]
        [InlineData("[1,2]", "[2,1]", false)]
        [InlineData("[1,2]", "[1]", false)]
        [InlineData("[1]", "[1,2]", false)]
        [InlineData("[null]", "[]", false)]
        [InlineData("[]", "[null]", false)]
        [InlineData("{\"values\":[1,2]}", "{\"values\":[1]}", false)]
        [InlineData("{\"extra\":true}", "{}", false)]
        [InlineData("{}", "{\"extra\":true}", false)]
        [InlineData("null", " null ", true)]
        [InlineData("false", "true", false)]
        [InlineData("\"a\"", "\"\\u0061\"", true)]
        [InlineData("\"1\"", "1", false)]
        [InlineData("invalid", "{}", false)]
        [InlineData("{}", "invalid", false)]
        public void JsonBodyComparisonMatchesDiagnosticResult(string requestBody, string recordedBody, bool equal)
        {
            byte[] request = Encoding.UTF8.GetBytes(requestBody);
            byte[] recorded = Encoding.UTF8.GetBytes(recordedBody);
            var description = new StringBuilder();

            Assert.Equal(equal, JsonComparer.AreEqual(request, recorded));
            Assert.Equal(equal, JsonComparer.CompareJson(request, recorded).Count == 0);
            Assert.Equal(equal ? 0 : 1, RecordMatcher.CompareBodies(request, recorded, "application/json", "application/json", Encoding.UTF8));
            Assert.Equal(equal ? 0 : 1, RecordMatcher.CompareBodies(request, recorded, "application/json", "application/json", Encoding.UTF8, description));
            Assert.Equal(equal, description.Length == 0);
        }

        [Fact]
        public void JsonDiagnosticsIncludeEveryDifference()
        {
            var request = Encoding.UTF8.GetBytes("{\"name\":\"request\",\"values\":[1,2,3],\"requestOnly\":true}");
            var recorded = Encoding.UTF8.GetBytes("{\"name\":\"record\",\"values\":[1],\"recordOnly\":false}");

            Assert.Equal(new[]
            {
                ".name: \"request\" != \"record\"",
                ".values[1]: Extra element in request JSON",
                ".values[2]: Extra element in request JSON",
                ".requestOnly: Missing in request JSON",
                ".recordOnly: Missing in record JSON"
            }, JsonComparer.CompareJson(request, recorded));
        }

        [Fact]
        public void JsonEqualityAvoidsDiagnosticAllocations()
        {
            var request = Encoding.UTF8.GetBytes("[" + string.Join(",", Enumerable.Repeat("1", 512)) + "]");
            var recorded = Encoding.UTF8.GetBytes("[" + string.Join(",", Enumerable.Repeat("2", 512)) + "]");
            const int iterations = 50;

            (long AllocatedBytes, double Milliseconds) Measure(bool diagnostics)
            {
                var stopwatch = new Stopwatch();
                bool foundEqual = false;
                long before = GC.GetAllocatedBytesForCurrentThread();
                stopwatch.Start();
                for (int iteration = 0; iteration < iterations; iteration++)
                {
                    foundEqual |= diagnostics
                        ? JsonComparer.CompareJson(request, recorded).Count == 0
                        : JsonComparer.AreEqual(request, recorded);
                }
                stopwatch.Stop();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.False(foundEqual);
                return (allocated, stopwatch.Elapsed.TotalMilliseconds);
            }

            Measure(diagnostics: true);
            Measure(diagnostics: false);
            var diagnosticResult = Measure(diagnostics: true);
            var equalityResult = Measure(diagnostics: false);

            _output.WriteLine($"512-element JSON arrays, first element differs, {iterations} iterations:");
            _output.WriteLine($"Diagnostics: {diagnosticResult.AllocatedBytes / iterations:N0} bytes/op, {diagnosticResult.Milliseconds / iterations:F3} ms/op");
            _output.WriteLine($"Equality: {equalityResult.AllocatedBytes / iterations:N0} bytes/op, {equalityResult.Milliseconds / iterations:F3} ms/op");
            Assert.True(equalityResult.AllocatedBytes < diagnosticResult.AllocatedBytes / 4,
                $"Expected at least 75% fewer allocations; diagnostics: {diagnosticResult.AllocatedBytes}, equality: {equalityResult.AllocatedBytes}.");
        }

        private static RecordEntry CreateMatchEntry(string uri, Core.RequestMethod method)
        {
            var entry = new RecordEntry { RequestUri = uri, RequestMethod = method };
            entry.Request.Headers.Add("x-match", new[] { "expected" });
            entry.Request.Body = Encoding.UTF8.GetBytes("body");
            return entry;
        }

        private sealed class CountingRecordMatcher : RecordMatcher
        {
            public int HeaderComparisons { get; private set; }
            public int BodyComparisons { get; private set; }

            public override int CompareHeaderDictionaries(SortedDictionary<string, string[]> headers, SortedDictionary<string, string[]> entryHeaders,
                HashSet<string> ignoredHeaders, HashSet<string> excludedHeaders, StringBuilder descriptionBuilder = null)
            {
                HeaderComparisons++;
                return base.CompareHeaderDictionaries(headers, entryHeaders, ignoredHeaders, excludedHeaders, descriptionBuilder);
            }

            public override int CompareBodies(byte[] requestBody, byte[] recordBody, string requestContentType, string recordContentType,
                Encoding encoding, StringBuilder descriptionBuilder = null)
            {
                BodyComparisons++;
                return base.CompareBodies(requestBody, recordBody, requestContentType, recordContentType, encoding, descriptionBuilder);
            }
        }

        [Theory]
        [InlineData("Test.RecordEntries/response_with_xml_body.json", "Content-Type", "application/json;     odata=nometadata")]
        [InlineData("Test.RecordEntries/response_with_xml_body.json", "Content-Type", "application/json;odata=nometadata")]
        [InlineData("Test.RecordEntries/request_with_accept_commas.json", "Accept", "application/vnd.oci.image.manifest.v1\u002Bjson,    application/json")]
        [InlineData("Test.RecordEntries/request_with_accept_commas.json", "Accept", "application/vnd.oci.image.manifest.v1\u002Bjson,application/json")]
        public void MatchesBadlyNormalizedHeader(string file, string targetHeader, string overrideValue)
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession(file);
            var identicalRequest = TestHelpers.LoadRecordSession(file).Session.Entries[0];
            identicalRequest.Request.Headers[targetHeader][0] = overrideValue;

            var expectedIdenticalMatch = sessionForRetrieval.Session.Lookup(identicalRequest, RecordMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
        }

        [Fact]
        public void BodilessMatcherMatchesIdenticalRequest()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");

            var identicalRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json").Session.Entries[0];

            var expectedIdenticalMatch = sessionForRetrieval.Session.Lookup(identicalRequest, BodilessMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
        }

        [Fact]
        public void BodilessMatcherMatchesBodilessRequest()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");

            var bodilessRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json").Session.Entries[0];
            bodilessRequest.Request.Body = new byte[] { };
            bodilessRequest.Request.Headers["Content-Length"] = new string[] { "0" };

            var expectedBodilessMatch = sessionForRetrieval.Session.Lookup(bodilessRequest, BodilessMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
        }

        [Fact]
        public void BodilessMatcherMatchesDifferentBodyRequest()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");

            var differentBodyRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json").Session.Entries[0];
            differentBodyRequest.Request.Body = TestHelpers.GenerateByteRequestBody("This is a test body :)");
            differentBodyRequest.Request.Headers["Content-Length"] = new string[] { "15" };

            var expectedDiffBodyMatch = sessionForRetrieval.Session.Lookup(differentBodyRequest, BodilessMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
        }

        [Fact]
        public void BodilessMatcherThrowsOnDiffUri()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");

            var identicalRequestDiffURI = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json").Session.Entries[0];
            identicalRequestDiffURI.RequestUri = identicalRequestDiffURI.RequestUri + "2";

            Assert.Throws<TestRecordingMismatchException>(() =>
            {
                sessionForRetrieval.Session.Lookup(identicalRequestDiffURI, BodilessMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
            });
        }

        [Fact]
        public void BodilessMatcherThrowsOnDiffHeaders()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");

            var identicalBodyDiffHeaders = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json").Session.Entries[0];
            identicalBodyDiffHeaders.Request.Headers.Remove(identicalBodyDiffHeaders.Request.Headers.Keys.First());

            Assert.Throws<TestRecordingMismatchException>(() =>
            {
                sessionForRetrieval.Session.Lookup(identicalBodyDiffHeaders, BodilessMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
            });
        }


        [Fact]
        public void HeaderlessMatcherMatchesHeaderlessRequest()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json");
            var headerlessRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json").Session.Entries[0];
            headerlessRequest.Request.Headers = new SortedDictionary<string, string[]>();

            var expectedDiffBodyMatch = sessionForRetrieval.Session.Lookup(headerlessRequest, HeaderlessMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
        }

        [Fact]
        public void HeaderlessMatcherMatchesDifferentHeadersRequest()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json");
            var differentHeadersRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json").Session.Entries[0];
            differentHeadersRequest.Request.Headers.Remove(differentHeadersRequest.Request.Headers.Keys.Last());

            var expectedDiffBodyMatch = sessionForRetrieval.Session.Lookup(differentHeadersRequest, HeaderlessMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
        }

        [Fact]
        public void HeaderlessMatcherMatchesIdenticalHeadersRequest()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json");
            var identicalHeaders = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json").Session.Entries[0];

            var expectedDiffBodyMatch = sessionForRetrieval.Session.Lookup(identicalHeaders, HeaderlessMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
        }

        [Fact]
        public void HeaderlessMatcherThrowsOnDiffBody()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json");
            var diffBodyRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json").Session.Entries[0];
            diffBodyRequest.Request.Body = Encoding.UTF8.GetBytes("A Different Request Body");

            Assert.Throws<TestRecordingMismatchException>(() =>
            {
                sessionForRetrieval.Session.Lookup(diffBodyRequest, HeaderlessMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
            });
        }

        [Fact]
        public void HeaderlessMatcherThrowsOnDiffUri()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json");
            var differenUriRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json").Session.Entries[0];
            differenUriRequest.RequestUri = "https://shouldntmatch.com";

            Assert.Throws<TestRecordingMismatchException>(() =>
            {
                sessionForRetrieval.Session.Lookup(differenUriRequest, HeaderlessMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
            });
        }

        [Fact]
        public void CustomMatcherDefaultArgumentsMatch()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");
            var identicalRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json").Session.Entries[0];
            var matcher = new CustomDefaultMatcher();

            sessionForRetrieval.Session.Lookup(identicalRequest, matcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
        }

        [Fact]
        public void CustomMatcherDisableBodyMatches()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");
            var differenBodyRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json").Session.Entries[0];
            differenBodyRequest.Request.Body = Encoding.UTF8.GetBytes("Definitely not the same body");

            var matcher = new CustomDefaultMatcher(compareBodies: false);

            sessionForRetrieval.Session.Lookup(differenBodyRequest, matcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
        }

        [Fact]
        public void CustomMatcherSpecifyExcludedHeadersMatches()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json");
            var differentHeadersRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json").Session.Entries[0];
            differentHeadersRequest.Request.Headers["Accept-Encoding"] = new string[] { "a-test-header-that-shouldn't-match" };

            var matcher = new CustomDefaultMatcher(excludedHeaders: "Accept-Encoding");

            sessionForRetrieval.Session.Lookup(differentHeadersRequest, matcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
        }

        [Fact]
        public void CustomMatcherSpecifyIgnoredHeadersMatches()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json");
            var differentHeadersRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json").Session.Entries[0];
            differentHeadersRequest.Request.Headers["Accept-Encoding"] = new string[] { "a-test-header-that-shouldn't-match" };

            var matcher = new CustomDefaultMatcher(ignoredHeaders: "Accept-Encoding");

            sessionForRetrieval.Session.Lookup(differentHeadersRequest, matcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
        }

        [Fact]
        public void CustomMatcherSpecifyIgnoredThrowsOnRequestNonPresence()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json");
            var differentHeadersRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json").Session.Entries[0];
            differentHeadersRequest.Request.Headers.Remove("Accept-Encoding");

            var matcher = new CustomDefaultMatcher(ignoredHeaders: "Accept-Encoding");

            var assertion = Assert.Throws<TestRecordingMismatchException>(() =>
            {
                sessionForRetrieval.Session.Lookup(differentHeadersRequest, matcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
            });

            Assert.Contains("<Accept-Encoding> is absent in request", assertion.Message);
        }

        [Fact]
        public void CustomMatcherSpecifyIgnoredThrowsOnRecordNonPresence()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json");
            sessionForRetrieval.Session.Entries[0].Request.Headers.Remove("Accept-Encoding");
            var sameOriginalHeadersRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json").Session.Entries[0];

            var matcher = new CustomDefaultMatcher(ignoredHeaders: "Accept-Encoding");

            var assertion = Assert.Throws<TestRecordingMismatchException>(() =>
            {
                sessionForRetrieval.Session.Lookup(sameOriginalHeadersRequest, matcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
            });

            Assert.Contains("<Accept-Encoding> is absent in record", assertion.Message);
        }

        [Fact]
        public void CustomMatcherDefaultMatches()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json");
            var identicalRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json").Session.Entries[0];
            var matcher = new CustomDefaultMatcher();

            sessionForRetrieval.Session.Lookup(identicalRequest, matcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
        }

        [Fact]
        public void CustomMatcherThrowsOnUnmatched()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json");
            var differentRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json").Session.Entries[0];
            var matcher = new CustomDefaultMatcher(excludedHeaders: "Accept-Encoding");

            differentRequest.Request.Headers["Accept-Encoding"] = new string[] { "a-test-header-that-shouldn't-match" };
            differentRequest.RequestUri = "https://shouldntmatch.com";

            Assert.Throws<TestRecordingMismatchException>(() =>
            {
                sessionForRetrieval.Session.Lookup(differentRequest, HeaderlessMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
            });
        }

        [Fact]
        public async Task CustomMatcherMatchesDifferentUriOrder()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            testRecordingHandler.Matcher = new CustomDefaultMatcher(ignoreQueryOrdering: true);
            var playbackContext = new DefaultHttpContext();
            var targetFile = "Test.RecordEntries/request_with_subscriptionid.json";
            var body = "{\"x-recording-file\":\"" + targetFile + "\"}";
            playbackContext.Request.Body = TestHelpers.GenerateStreamRequestBody(body);
            playbackContext.Request.ContentLength = body.Length;

            var controller = new Playback(testRecordingHandler, new NullLoggerFactory())
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = playbackContext
                }
            };
            await controller.Start();
            var recordingId = playbackContext.Response.Headers["x-recording-id"].ToString();

            // prepare recording context
            playbackContext.Request.Headers.Clear();
            playbackContext.Response.Headers.Clear();
            var requestHeaders = new Dictionary<string, string>(){
                { ":authority", "localhost:5001" },
                { ":method", "POST" },
                { ":path", "/" },
                { ":scheme", "https" },
                { "Accept-Encoding", "gzip" },
                { "Content-Length", "0" },
                { "User-Agent", "Go-http-client/2.0" },
                { "x-recording-id", recordingId },
                { "x-recording-upstream-base-uri", "https://management.azure.com/" }
            };
            foreach (var kvp in requestHeaders)
            {
                playbackContext.Request.Headers.Append(kvp.Key, kvp.Value);
            }
            playbackContext.Request.Method = "POST";

            // the query parameters are in reversed order from the recording deliberately.
            var queryString = "?uselessUriAddition=hellothere&api-version=2019-05-01";
            var path = "/subscriptions/12345678-1234-1234-5678-123456789010/providers/Microsoft.ContainerRegistry/checkNameAvailability";
            playbackContext.Request.Host = new HostString("https://localhost:5001");
            playbackContext.Features.Get<IHttpRequestFeature>().RawTarget = path + queryString;
            await testRecordingHandler.HandlePlaybackRequest(recordingId, playbackContext.Request, playbackContext.Response);
            Assert.Equal("WESTUS:20210909T204819Z:f9a33867-6efc-4748-b322-303b2b933466", playbackContext.Response.Headers["x-ms-routing-request-id"].ToString());
        }


        [Fact]
        public async Task EncodedUriAmpersandWorksCrossplat()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            testRecordingHandler.Matcher = new CustomDefaultMatcher(ignoreQueryOrdering: true);
            var playbackContext = new DefaultHttpContext();
            var targetFile = "Test.RecordEntries/request_with_encoded_ampersand.json";
            var body = "{\"x-recording-file\":\"" + targetFile + "\"}";
            playbackContext.Request.Body = TestHelpers.GenerateStreamRequestBody(body);
            playbackContext.Request.ContentLength = body.Length;

            var controller = new Playback(testRecordingHandler, new NullLoggerFactory())
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = playbackContext
                }
            };
            await controller.Start();
            var recordingId = playbackContext.Response.Headers["x-recording-id"].ToString();

            playbackContext.Request.Headers.Clear();
            playbackContext.Response.Headers.Clear();
            playbackContext.Request.Method = "GET";

            var requestHeaders = new Dictionary<string, string>(){
                { "x-recording-id", recordingId },
                { "x-recording-upstream-base-uri", "https://REDACTED" }
            };
            foreach (var kvp in requestHeaders)
            {
                playbackContext.Request.Headers.Append(kvp.Key, kvp.Value);
            }
            var queryString = "?api-version=1.0&year=2023&basinId=AL&govId=5";
            var path = "/weather/tropical/storms/json";
            playbackContext.Request.Host = new HostString("https://localhost:5001");
            playbackContext.Features.Get<IHttpRequestFeature>().RawTarget = path + queryString;
            await testRecordingHandler.HandlePlaybackRequest(recordingId, playbackContext.Request, playbackContext.Response);

            Assert.Equal("Ref A: 980665086A12483993E2782EDFC9F29A Ref B: STBEDGE0106 Ref C: 2023-07-19T22:52:17Z", playbackContext.Response.Headers["X-MSEdge-Ref"].ToString());
            Assert.Equal(200, playbackContext.Response.StatusCode);
        }


    }
}

