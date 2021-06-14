using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using System;
using System.Text.Json;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class SanitizerTests
    {
        public OAuthResponseSanitizer OAuthResponseSanitizer = new OAuthResponseSanitizer();

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
        public void ReplaceRequestSubscriptionId()
        {

        }

        [Fact]
        public void ReplaceRequestSubscriptionIdNoAction()
        {

        }

        [Fact]
        public void HeaderRegexSanitizerSimpleReplace()
        {

        }

        [Fact]
        public void HeaderRegexSanitizerGroupedRegexReplace()
        {

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
