using Azure.Core;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Matchers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class MatcherTests
    {
        public BodilessMatcher BodilessMatcher = new BodilessMatcher();
        public HeaderlessMatcher HeaderlessMatcher = new HeaderlessMatcher();

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
        public void CustomMatcherDisableBodyMatches()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");
            var differenBodyRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json").Session.Entries[0];
            differenBodyRequest.Request.Body = Encoding.UTF8.GetBytes("Definitely not the same body");

            var matcher = new CustomDefaultMatcher(compareBodies: false);

            sessionForRetrieval.Session.Lookup(differenBodyRequest, matcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
        }

        [Fact]
        public void CustomerMatcherSpecifyExcludedHeadersMatches()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json");
            var differentHeadersRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/if_none_match_present.json").Session.Entries[0];
            differentHeadersRequest.Request.Headers["Accept-Encoding"] = new string[] { "a-test-header-that-shouldn't-match" };

            var matcher = new CustomDefaultMatcher(nonDefaultHeaderExclusions: "Accept-Encoding");

            sessionForRetrieval.Session.Lookup(differentHeadersRequest, matcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
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
            var matcher = new CustomDefaultMatcher(nonDefaultHeaderExclusions: "Accept-Encoding");

            differentRequest.Request.Headers["Accept-Encoding"] = new string[] { "a-test-header-that-shouldn't-match" };
            differentRequest.RequestUri = "https://shouldntmatch.com";

            Assert.Throws<TestRecordingMismatchException>(() => {
                sessionForRetrieval.Session.Lookup(differentRequest, HeaderlessMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
            });
        }
    }
}

