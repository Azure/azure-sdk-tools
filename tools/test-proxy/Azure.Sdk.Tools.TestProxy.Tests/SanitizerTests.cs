using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class SanitizerTests
    {
        public OAuthResponseSanitizer OAuthResponseSanitizer = new OAuthResponseSanitizer();

        public string lookaheadReplaceRegex = @"[a-z]+(?=\.(?:table|blob|queue)\.core\.windows\.net)";
        public string capturingGroupReplaceRegex = @"https\:\/\/(?<account>[a-z]+)\.(?:table|blob|queue)\.core\.windows\.net";
        public string scopeClean = @"scope\=(?<scope>[^&]*)";

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
            var headerRegexSanitizer = new HeaderRegexSanitizer(targetKey, value: "fakeaccount", regex: lookaheadReplaceRegex);
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
            var headerRegexSanitizer = new HeaderRegexSanitizer(targetKey, value: "fakeaccount", regex: capturingGroupReplaceRegex, groupForReplace: "account");
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
            var headerRegexSanitizer = new HeaderRegexSanitizer(targetKey, value: "fakeaccount", regex: capturingGroupReplaceRegex, groupForReplace: "account");
            session.Session.Sanitize(headerRegexSanitizer);

            var newResult = targetEntry.Response.Headers[targetKey].First();

            Assert.Equal(originalHeaderValue, newResult);
        }

        [Fact]
        public void BodyRegexSanitizerCleansJSON()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];

            var replaceTableNameRegex = "TableName\"\\s*:\\s*\"(?<tablename>[a-z0-9]+)\"";

            var bodyRegexSanitizer = new BodyRegexSanitizer(value: "afaketable", regex: replaceTableNameRegex, groupForReplace: "tablename");
            session.Session.Sanitize(bodyRegexSanitizer);

            Assert.Contains("\"TableName\":\"afaketable\"", Encoding.UTF8.GetString(targetEntry.Response.Body));
        }

        [Fact]
        public void BodyRegexSanitizerCleansText()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");
            var targetEntry = session.Session.Entries[0];

            var bodyRegexSanitizer = new BodyRegexSanitizer(value: "sanitized.scope", regex: scopeClean, groupForReplace: "scope");
            session.Session.Sanitize(bodyRegexSanitizer);

            var expectedBodyStartsWith = "scope=sanitized.scope&client_id";

            Assert.StartsWith(expectedBodyStartsWith, Encoding.UTF8.GetString(targetEntry.Request.Body));
        }

        [Fact]
        public void BodyRegexSanitizerQuietlyExits()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];

            var beforeUpdate = targetEntry.Request.Body;
            var bodyRegexSanitizer = new BodyRegexSanitizer(value: "fakeaccount", regex: capturingGroupReplaceRegex, groupForReplace: "account");
            session.Session.Sanitize(bodyRegexSanitizer);

            Assert.Equal(beforeUpdate, targetEntry.Request.Body);
        }

        [Fact]
        public void RemoveHeaderSanitizerQuietlyExits()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];

        }

        [Fact]
        public void RemoveHeaderSanitizerRemovesSingleHeader()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];

        }

        [Fact]
        public void RemoveHeaderSanitizerRemovesMultipleHeaders()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];


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
