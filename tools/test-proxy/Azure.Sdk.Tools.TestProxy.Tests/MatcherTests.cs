using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Matchers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class MatcherTests
    {
        public BodilessMatcher BodilessMatcher = new BodilessMatcher();
        public HeaderlessMatcher HeaderlessMatcher = new HeaderlessMatcher();
        public RecordMatcher RecordMatcher = new RecordMatcher();

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

            Assert.Throws<TestRecordingMismatchException>(() => {
                sessionForRetrieval.Session.Lookup(identicalRequestDiffURI, BodilessMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
            });
        }

        [Fact]
        public void BodilessMatcherThrowsOnDiffHeaders()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");

            var identicalBodyDiffHeaders = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json").Session.Entries[0];
            identicalBodyDiffHeaders.Request.Headers.Remove(identicalBodyDiffHeaders.Request.Headers.Keys.First());

            Assert.Throws<TestRecordingMismatchException>(() => {
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

            Assert.Throws<TestRecordingMismatchException>(() => {
                sessionForRetrieval.Session.Lookup(diffBodyRequest, HeaderlessMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
            });
        }

        [Fact]
        public void HeaderlessMatcherThrowsOnDiffUri()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json");
            var differenUriRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json").Session.Entries[0];
            differenUriRequest.RequestUri = "https://shouldntmatch.com";

            Assert.Throws<TestRecordingMismatchException>(() => {
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

            var assertion = Assert.Throws<TestRecordingMismatchException>(() => {
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

            var assertion = Assert.Throws<TestRecordingMismatchException>(() => {
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

            Assert.Throws<TestRecordingMismatchException>(() => {
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
                playbackContext.Request.Headers.Add(kvp.Key, kvp.Value);
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
    }
}

