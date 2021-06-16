using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using System;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class SanitizerTests
    {
        public OAuthResponseSanitizer OAuthResponseSanitizer = new OAuthResponseSanitizer();

        public string lookaheadReplaceRegex = @"[a-z]+(?=\.(?:table|blob|queue)\.core\.windows\.net)";
        public string capturingGroupReplaceRegex = @"https\:\/\/(?<account>[a-z]+)\.(?:table|blob|queue)\.core\.windows\.net";

        [Fact]
        public void OauthResponseSanitizerCleansV2AuthRequest()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");
            
            session.Session.Sanitize(OAuthResponseSanitizer);

            Assert.Empty(session.Session.Entries);
        }

        [Fact]
        public void OauthResponseSanitizerCleansNonV2AuthRequest()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");
            session.Session.Entries[0].RequestUri = "https://login.microsoftonline.com/12345678-1234-1234-1234-123456789012/oauth2/token";

            session.Session.Sanitize(OAuthResponseSanitizer);

            Assert.Empty(session.Session.Entries);
        }

        [Fact]
        public void OauthResponseSanitizerNotAggressive()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");

            var expectedCount = session.Session.Entries.Count;

            session.Session.Sanitize(OAuthResponseSanitizer);

            Assert.Equal(expectedCount, session.Session.Entries.Count);
        }

        [Fact]
        public void UriRegexSanitizerReplacesTableName()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var originalValue = session.Session.Entries[0].RequestUri;

            var uriSanitizer = new UriRegexSanitizer(lookaheadReplaceRegex, "fakeaccount");
            session.Session.Sanitize(uriSanitizer);

            var testValue = session.Session.Entries[0].RequestUri;

            Assert.True(originalValue != testValue);
            Assert.StartsWith("https://fakeaccount.table.core.windows.net", testValue);
        }

        [Fact]
        public void UriRegexSanitizerAggressivenessCheck()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");
            var originalValue = session.Session.Entries[0].RequestUri;

            var uriSanitizer = new UriRegexSanitizer(lookaheadReplaceRegex, "fakeaccount");
            session.Session.Sanitize(uriSanitizer);

            var testValue = session.Session.Entries[0].RequestUri;

            Assert.Equal(originalValue, testValue);
        }

        [Fact]
        public void ReplaceRequestSubscriptionId()
        {
            // tests successfully replacement
        }

        [Fact]
        public void ReplaceRequestSubscriptionIdNoAction()
        {
            // successful sanitize, no action necessary
        }

        [Fact]
        public void HeaderRegexSanitizerSimpleReplace()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];
            var targetKey = "Location";
            var originalHeaderValue = targetEntry.Response.Headers[targetKey].First();

            // where we have a key, a regex, and no groupname.
            var headerRegexSanitizer = new HeaderRegexSanitizer(targetKey, "fakeaccount", regex: lookaheadReplaceRegex);
            session.Session.Sanitize(headerRegexSanitizer);

            var testValue = targetEntry.Response.Headers[targetKey].First();

            Assert.NotEqual(originalHeaderValue, testValue);
            Assert.StartsWith("https://fakeaccount.table.core.windows.net", testValue);
        }

        [Fact]
        public void HeaderRegexSanitizerGroupedRegexReplace()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetKey = "Location";
            var targetEntry = session.Session.Entries[0];
            var originalHeaderValue = targetEntry.Response.Headers[targetKey].First();

            // where we have a key, a regex, and a groupname to replace with value Y
            var headerRegexSanitizer = new HeaderRegexSanitizer(targetKey, "fakeaccount", regex: capturingGroupReplaceRegex, groupForReplace: "account");
            session.Session.Sanitize(headerRegexSanitizer);

            var testValue = targetEntry.Response.Headers[targetKey].First();

            Assert.NotEqual(originalHeaderValue, testValue);
            Assert.StartsWith("https://fakeaccount.table.core.windows.net", testValue);
        }

        [Fact]
        public void HeaderRegexSanitizerAggressivenessCheck()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];
            var targetKey = "Content-Type";
            var originalHeaderValue = targetEntry.Response.Headers[targetKey].First();

            // where we find a key, but there is nothing to be done by the sanitizer
            var headerRegexSanitizer = new HeaderRegexSanitizer(targetKey, "fakeaccount", regex: capturingGroupReplaceRegex, groupForReplace: "account");
            session.Session.Sanitize(headerRegexSanitizer);

            var newResult = targetEntry.Response.Headers[targetKey].First();

            Assert.Equal(originalHeaderValue, newResult);
        }

        [Fact]
        public void ContinuationSanitizerMultipleStepsNoKey()
        {

        }

        [Fact]
        public void ContinuationSanitizerSimpleRequestResponse()
        {

        }

        [Fact]
        public void ContinuationSanitizerMultipleSteps()
        {

        }
    }
}
