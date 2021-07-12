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

            var expectedBodilessMatch = sessionForRetrieval.Session.Lookup(bodilessRequest, BodilessMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
        }

        [Fact]
        public void BodilessMatcherMatchesDifferentBodyRequest()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");

            var differentBodyRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json").Session.Entries[0];
            differentBodyRequest.Request.Body = TestHelpers.GenerateByteRequestBody("This is a test body :)");

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
    }
}

