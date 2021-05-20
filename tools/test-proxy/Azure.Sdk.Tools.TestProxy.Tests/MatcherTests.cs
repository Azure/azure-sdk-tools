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
        [Fact]
        public void TestBodilessMatcher()
        {
            // load our session
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");
            
            // now load a bunch of identical sessions, but break them in various ways that should NOT break retrieval of an entry
            var identicalRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json").Session.Entries[0];

            var bodilessRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json").Session.Entries[0];
            bodilessRequest.Request.Body = new byte[] { };

            var differentBodyRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json").Session.Entries[0];
            differentBodyRequest.Request.Body = TestHelpers.GenerateByteRequestBody("This is a test body :)");

            // cases that shouldn't still match
            var identicalRequestDiffURI = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json").Session.Entries[0];
            identicalRequestDiffURI.RequestUri = identicalRequestDiffURI.RequestUri + "2";

            var identicalBodyDiffHeaders = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json").Session.Entries[0];
            identicalBodyDiffHeaders.Request.Headers.Remove(identicalBodyDiffHeaders.Request.Headers.Keys.First());

            var bodilessMatcher = new BodilessMatcher();

            // we expect there to be a match in every case here. if we get one (out of a set of exactly 1) we know we matched correctly.
            var expectedIdenticalMatch = sessionForRetrieval.Session.Lookup(identicalRequest, bodilessMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
            var expectedBodilessMatch = sessionForRetrieval.Session.Lookup(bodilessRequest, bodilessMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);
            var expectedDiffBodyMatch = sessionForRetrieval.Session.Lookup(differentBodyRequest, bodilessMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false);

            Assert.Throws<TestRecordingMismatchException>(() => { 
                sessionForRetrieval.Session.Lookup(identicalRequestDiffURI, bodilessMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false); 
            });
            
            Assert.Throws<TestRecordingMismatchException>(() => { 
                sessionForRetrieval.Session.Lookup(identicalBodyDiffHeaders, bodilessMatcher, sanitizers: new List<RecordedTestSanitizer>(), remove: false); 
            });
        }
    }
}

