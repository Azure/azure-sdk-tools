using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Matchers;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class MultipartTests
    {
        private NullLoggerFactory _nullLogger = new NullLoggerFactory();

        #region Sanitizer Tests

        [Fact]
        public async void HeaderRegexSanitizerMultipartReplace()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/multipart_header.json");
            var targetEntry = session.Session.Entries[0];
            var targetKey = "Cookie";

            var headerRegexSanitizer = new HeaderRegexSanitizer(targetKey, value: "REDACTED", regex: "SuperDifferent");
            await session.Session.Sanitize(headerRegexSanitizer);

            Assert.Equal("REDACTEDCookie", targetEntry.Request.Headers[targetKey][0]);
            Assert.Equal("KindaDifferentCookie", targetEntry.Request.Headers[targetKey][1]);
        }

        [Fact]
        public async void HeaderRegexSanitizerMultipartReplaceLatterOnly()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/multipart_header.json");
            var targetEntry = session.Session.Entries[0];
            var targetKey = "Cookie";

            var headerRegexSanitizer = new HeaderRegexSanitizer(targetKey, value: "REDACTED", regex: "KindaDifferent");
            await session.Session.Sanitize(headerRegexSanitizer);

            Assert.Equal("SuperDifferentCookie", targetEntry.Request.Headers[targetKey][0]);
            Assert.Equal("REDACTEDCookie", targetEntry.Request.Headers[targetKey][1]);
        }

        [Fact]
        public async Task MultipartRequestsCanSanitizeWithoutChangingBytes()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/multipart_request.json");
            var worklessSanitizer = new BodyRegexSanitizer(regex: "abc123");
            var requestRef = session.Session.Entries[0].Request;
            var responseRef = session.Session.Entries[0].Response;
            var requestBodyBytesBefore = Encoding.UTF8.GetString(requestRef.Body);
            var responseBodyBytesBefore = Encoding.UTF8.GetString(responseRef.Body);

            await session.Session.Sanitize(worklessSanitizer);

            var requestBodyBytesAfter = Encoding.UTF8.GetString(requestRef.Body);
            var responseBodyBytesAfter = Encoding.UTF8.GetString(responseRef.Body);

            Assert.Equal(requestBodyBytesBefore, requestBodyBytesAfter);
            Assert.Equal(responseBodyBytesBefore, responseBodyBytesAfter);
        }

        [Fact]
        public void CanDeserializeFromOriginalMultipartMixedRecording()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/old_multipart_request.json");

            Assert.NotNull(session.Session.Entries.First().Request.Body);
        }

        [Fact]
        public async Task CanSanitizeComplexRequest()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/failing_multipart_body.json");
            var breakingSanitizer = new GeneralRegexSanitizer(value: "00000000-0000-0000-0000-000000000000", regex: "batch[a-z]*_([0-9a-f]{8}\\b-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-\\b[0-9a-f]{12}\\b)", groupForReplace: "1");
            await session.Session.Sanitize(breakingSanitizer);
        }

        [Fact]
        public async Task ContentDispositionFilePathSanitizerNormalizesWindowsEndings()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/multipart_form_data_windows.json");
            var sanitizer = new ContentDispositionFilePathSanitizer();
            await session.Session.Sanitize(sanitizer);
            var requestBody = session.Session.Entries[0].Request.Body;
            var requestBodyString = Encoding.UTF8.GetString(requestBody);

            Assert.DoesNotContain("\\", requestBodyString);
            Assert.DoesNotContain("%5C", requestBodyString);
        }

        #endregion

        #region Matcher Tests

        [Fact]
        public void MultiPartMatcherMatchesDifferentBoundary()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/multipart_request.json");
            var differentBoundaryRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/multipart_request_diff_boundary.json").Session.Entries[0];
            var expectedDiffBodyMatch = sessionForRetrieval.Session.Lookup(differentBoundaryRequest, new RecordMatcher(), sanitizers: new List<RecordedTestSanitizer>(), remove: false);
        }

        [Fact]
        public void MultiPartMultiLayerMatcherMatchesDifferentBoundary()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/multipart_request_two_layers.json");
            var differentBoundaryRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/multipart_request_two_layers_diff_boundary.json").Session.Entries[0];
            var expectedDiffBodyMatch = sessionForRetrieval.Session.Lookup(differentBoundaryRequest, new RecordMatcher(), sanitizers: new List<RecordedTestSanitizer>(), remove: false);
        }

        [Fact]
        public void MultiPartMatcherThrowsOnDiffBody()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/multipart_request.json");
            var diffBodyRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/multipart_request_diff_body.json").Session.Entries[0];
            diffBodyRequest.Request.Body = Encoding.UTF8.GetBytes("A Different Request Body");
            Assert.Throws<TestRecordingMismatchException>(() =>
            {
                sessionForRetrieval.Session.Lookup(diffBodyRequest, new RecordMatcher(), sanitizers: new List<RecordedTestSanitizer>(), remove: false);
            });
        }

        [Fact]
        public void MultiPartMatcherMatchesIdenticalBoundary()
        {
            var sessionForRetrieval = TestHelpers.LoadRecordSession("Test.RecordEntries/multipart_request.json");
            var identicalRequest = TestHelpers.LoadRecordSession("Test.RecordEntries/multipart_request.json").Session.Entries[0];
            var expectedIdenticalMatch = sessionForRetrieval.Session.Lookup(identicalRequest, new RecordMatcher(), sanitizers: new List<RecordedTestSanitizer>(), remove: false);
        }

        #endregion

        #region Record Tests

        [Fact]
        public void TestMultipartMixedCanRoundTrip()
        {
            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            var targetPath = "multipartroundtrip.json";
            var recordingSession = new RecordSession();

            // build up a raw request from what we we would see in a real recording
            RecordEntry rawEntry = new RecordEntry();
            rawEntry.Request.Headers["Content-Type"] = ["multipart/mixed; boundary=batch_dbed2534-1685-4042-8992-9f259d8c24c7"];
            using var stream = System.IO.File.OpenRead("Test.RecordEntries/raw_multipart_request_body.txt");
            using var reader = new StreamReader(stream);
            string rawbase64content = reader.ReadToEnd().Trim();
            rawEntry.Request.Body = Convert.FromBase64String(rawbase64content);
            rawEntry.RequestMethod = Core.RequestMethod.Post;
            recordingSession.Entries.Add(rawEntry);

            // write the raw recording to disk
            var sessionForDisk = new ModifiableRecordSession(recordingSession, new SanitizerDictionary(), "abc123");
            sessionForDisk.Path = targetPath;
            testRecordingHandler.WriteToDisk(sessionForDisk);

            var loadedFromDisk = TestHelpers.LoadRecordSession(targetPath);
            Assert.Equal(rawEntry.Request.Body, loadedFromDisk.Session.Entries[0].Request.Body);
        }

        #endregion
    }
}
