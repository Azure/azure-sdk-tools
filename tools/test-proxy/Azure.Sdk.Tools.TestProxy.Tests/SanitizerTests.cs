using Azure.Sdk.Tools.TestProxy.Sanitizers;
using System.Linq;
using System.Text;
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

            var uriSanitizer = new UriRegexSanitizer(value: "fakeaccount", regex: lookaheadReplaceRegex);
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

            var uriSanitizer = new UriRegexSanitizer(value: "fakeaccount", regex: lookaheadReplaceRegex);
            session.Session.Sanitize(uriSanitizer);

            var testValue = session.Session.Entries[0].RequestUri;

            Assert.Equal(originalValue, testValue);
        }


        [Fact]
        public void GeneralRegexSanitizerAppliesToAllSets()
        {
            // arrange
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries.First();

            var originalUri = targetEntry.RequestUri.ToString();
            var originalBody = targetEntry.Response.Body.Clone();
            var originalLocationHeader = targetEntry.Response.Headers["Location"].First().ToString();
            var genericTValue = "generic_table_name";
            var genericAValue = "generic_account_name";
            var realTValue = "listtable09bf2a3d";
            var realAValue = "fakeazsdktestaccount";

            // shows up in requestBody, responseBody, responseHeader Location
            var tableNameSanitizer = new GeneralRegexSanitizer(value: genericTValue, regex: realTValue);
            // shows up in requestUri, responseHeader Location
            var accountNameSanitizer = new GeneralRegexSanitizer(value: genericAValue, regex: realAValue);

            // act
            session.Session.Sanitize(tableNameSanitizer);
            session.Session.Sanitize(accountNameSanitizer);
            var locationHeaderValue = targetEntry.Response.Headers["Location"].First();

            // assert that we successfully changed a header, the body, and the uri
            Assert.NotEqual(originalUri, targetEntry.RequestUri);
            Assert.NotEqual(originalBody, targetEntry.Response.Body);
            Assert.NotEqual(originalLocationHeader, locationHeaderValue);

            var requestBody = Encoding.UTF8.GetString(targetEntry.Request.Body);
            var responseBody = Encoding.UTF8.GetString(targetEntry.Response.Body);

            // assert that body doesn't contain anything we don't expect it to
            Assert.DoesNotContain(realTValue, responseBody);
            Assert.DoesNotContain(realAValue, responseBody);
            Assert.DoesNotContain(realTValue, requestBody);
            Assert.DoesNotContain(realTValue, requestBody);

            // assert that the new value has been dropped in where we expect it
            Assert.Contains(genericAValue, targetEntry.RequestUri);
            Assert.Contains(genericTValue, responseBody);
            Assert.Contains(genericAValue, locationHeaderValue);
            Assert.Contains(genericTValue, locationHeaderValue);
        }

        [Fact]
        public void ReplaceRequestSubscriptionId()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/request_with_subscriptionid.json");
            var targetEntry = session.Session.Entries.First();
            var originalUri = targetEntry.RequestUri.ToString();
            var subscriptionIdReplaceSanitizer = new UriSubscriptionIdSanitizer();

            session.Session.Sanitize(subscriptionIdReplaceSanitizer);
            var sanitizedUri = targetEntry.RequestUri;

            Assert.NotEqual(originalUri, sanitizedUri);
            Assert.StartsWith("/subscriptions/00000000-0000-0000-0000-000000000000/", sanitizedUri.Replace("https://management.azure.com", ""));
            Assert.DoesNotContain("12345678-1234-1234-5678-123456789010", sanitizedUri);
            Assert.Equal(originalUri.Length, sanitizedUri.Length);
        }

        [Fact]
        public void ReplaceRequestSubscriptionIdNoAction()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");
            var targetEntry = session.Session.Entries.First();
            var originalUri = targetEntry.RequestUri.ToString();
            var subscriptionIdReplaceSanitizer = new UriSubscriptionIdSanitizer();

            session.Session.Sanitize(subscriptionIdReplaceSanitizer);
            var sanitizedUri = targetEntry.RequestUri;

            // no action should have taken place here.
            Assert.Equal(originalUri, sanitizedUri);
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
            var requestHeaderCountBefore = targetEntry.Request.Headers.Count;

            var removeHeaderSanitizer = new RemoveHeaderSanitizer(headersForRemoval: "fakeaccount");
            session.Session.Sanitize(removeHeaderSanitizer);

            Assert.Equal(requestHeaderCountBefore, targetEntry.Request.Headers.Count);
        }

        [Fact]
        public void RemoveHeaderSanitizerRemovesSingleHeader()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];
            var headerForRemoval = "DataServiceVersion";

            var removeHeaderSanitizer = new RemoveHeaderSanitizer(headersForRemoval: headerForRemoval);
            session.Session.Sanitize(removeHeaderSanitizer);

            Assert.False(targetEntry.Request.Headers.ContainsKey(headerForRemoval));
        }

        [Fact]
        public void RemoveHeaderSanitizerRemovesMultipleHeaders()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];
            var headerForRemoval = "DataServiceVersion, Date,User-Agent"; // please note the wonky spacing is intentional

            var removeHeaderSanitizer = new RemoveHeaderSanitizer(headersForRemoval: headerForRemoval);
            session.Session.Sanitize(removeHeaderSanitizer);

            foreach(var header in headerForRemoval.Split(",").Select(x => x.Trim()))
            {
                Assert.False(targetEntry.Request.Headers.ContainsKey(header));
            }
        }

        [Fact]
        public void BodyKeySanitizerKeyReplace()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];
            var replacementValue = "sanitized.tablename";

            var bodyKeySanitizer = new BodyKeySanitizer(jsonPath: "$.TableName", value: replacementValue);
            session.Session.Sanitize(bodyKeySanitizer);

            Assert.Contains(replacementValue, Encoding.UTF8.GetString(targetEntry.Request.Body));
        }

        [Fact]
        public void BodyKeySanitizerRegexReplace()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];

            var bodyKeySanitizer = new BodyKeySanitizer(jsonPath: "$.TableName", value: "TABLE_ID_IS_SANITIZED", regex: @"(?<=listtable)(?<tableid>[a-z0-9]+)", groupForReplace: "tableid");
            session.Session.Sanitize(bodyKeySanitizer);

            Assert.Contains("listtableTABLE_ID_IS_SANITIZED", Encoding.UTF8.GetString(targetEntry.Response.Body));
        }

        [Fact]
        public void BodyKeySanitizerQuietlyExits()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];
            var replacementValue = "BodyIsSanitized";

            var bodyKeySanitizer = new BodyKeySanitizer(jsonPath: "$.Location", value: replacementValue);
            session.Session.Sanitize(bodyKeySanitizer);

            Assert.DoesNotContain(replacementValue, Encoding.UTF8.GetString(targetEntry.Request.Body));
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
