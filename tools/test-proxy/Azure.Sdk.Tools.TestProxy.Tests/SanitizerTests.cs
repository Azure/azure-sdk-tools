using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class SanitizerTests
    {
        public OAuthResponseSanitizer OAuthResponseSanitizer = new OAuthResponseSanitizer();
        private NullLoggerFactory _nullLogger = new NullLoggerFactory();


        public string oauthRegex = "\"/oauth2(?:/v2.0)?/token\"";
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

        [Theory]
        [InlineData("uri", "\"/oauth2(?:/v2.0)?/token\"")]
        [InlineData("body", "\"/oauth2(?:/v2.0)?/token\"")]
        [InlineData("header", "\"/oauth2(?:/v2.0)?/token\"")]
        public void RegexEntrySanitizerNoOpsOnNonMatch(string target, string regex)
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var sanitizer = new RegexEntrySanitizer(target, regex);
            var expectedCount = session.Session.Entries.Count;

            session.Session.Sanitize(sanitizer);

            Assert.Equal(expectedCount, session.Session.Entries.Count);
        }

        [Theory]
        [InlineData("body", "(listtable09bf2a3d|listtable19bf2a3d)", 9)]
        [InlineData("uri", "fakeazsdktestaccount", 0)]
        [InlineData("body", "listtable09bf2a3d", 10)]
        [InlineData("header", "a50f2f9c-b830-11eb-b8c8-10e7c6392c5a", 10)]
        public void RegexEntrySanitizerCorrectlySanitizes(string target, string regex, int endCount)
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var sanitizer = new RegexEntrySanitizer(target, regex);
            var expectedCount = session.Session.Entries.Count;

            session.Session.Sanitize(sanitizer);

            Assert.Equal(endCount, session.Session.Entries.Count);
        }

        [Fact]
        public void RegexEntrySanitizerCorrectlySanitizesSpecific()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/response_with_xml_body.json");
            var sanitizer = new RegexEntrySanitizer("header", "b24f75a9-b830-11eb-b949-10e7c6392c5a");
            var expectedCount = session.Session.Entries.Count;

            session.Session.Sanitize(sanitizer);

            Assert.Equal(2, session.Session.Entries.Count);
            Assert.Equal("b25bf92a-b830-11eb-947a-10e7c6392c5a", session.Session.Entries[0].Request.Headers["x-ms-client-request-id"][0].ToString());
        }

        [Theory]
        [InlineData("wrong_name", "", "When defining which section of a request the regex should target, only values")]
        [InlineData("", ".+", "When defining which section of a request the regex should target, only values")]
        [InlineData("uri", "\"[\"", "Expression of value")]
        public void RegexEntrySanitizerThrowsProperExceptions(string target, string regex, string exceptionMessage)
        {
            var assertion = Assert.Throws<HttpException>(
               () => new RegexEntrySanitizer(target, regex)
            );

            Assert.Contains(exceptionMessage, assertion.Message);
        }

        [Theory]
        [InlineData("{ \"target\": \"URI\", \"regex\": \"/oauth2(?:/v2.0)?/token\" }")]
        [InlineData("{ \"target\": \"uRi\", \"regex\": \"/login\\\\.microsoftonline.com\" }")]
        [InlineData("{ \"target\": \"bodY\", \"regex\": \"/oauth2(?:/v2.0)?/token\" }")]
        [InlineData("{ \"target\": \"HEADER\", \"regex\": \"/login\\\\.microsoftonline.com\" }")]
        public async Task RegexEntrySanitizerCreatesOverAPI(string body)
        {

            RecordingHandler testRecordingHandler = new RecordingHandler(Directory.GetCurrentDirectory());
            testRecordingHandler.Sanitizers.Clear();
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["x-abstraction-identifier"] = "RegexEntrySanitizer";
            httpContext.Request.Body = TestHelpers.GenerateStreamRequestBody(body);

            // content length must be set for the body to be parsed in SetMatcher
            httpContext.Request.ContentLength = httpContext.Request.Body.Length;

            var controller = new Admin(testRecordingHandler, _nullLogger)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = httpContext
                }
            };

            await controller.AddSanitizer();
            var sanitizer = testRecordingHandler.Sanitizers[0];
            Assert.True(sanitizer is RegexEntrySanitizer);


            var sanitizerTarget = (string)typeof(RegexEntrySanitizer).GetField("section", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sanitizer);
            var regex = (Regex)typeof(RegexEntrySanitizer).GetField("rx", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sanitizer);
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
        public void HeaderRegexSanitizerMultipartReplace()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/multipart_header.json");
            var targetEntry = session.Session.Entries[0];
            var targetKey = "Cookie";

            var headerRegexSanitizer = new HeaderRegexSanitizer(targetKey, value: "REDACTED", regex: "SuperDifferent");
            session.Session.Sanitize(headerRegexSanitizer);

            Assert.Equal("REDACTEDCookie", targetEntry.Request.Headers[targetKey][0]);
            Assert.Equal("KindaDifferentCookie", targetEntry.Request.Headers[targetKey][1]);
        }

        [Fact]
        public void HeaderRegexSanitizerMultipartReplaceLatterOnly()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/multipart_header.json");
            var targetEntry = session.Session.Entries[0];
            var targetKey = "Cookie";

            var headerRegexSanitizer = new HeaderRegexSanitizer(targetKey, value: "REDACTED", regex: "KindaDifferent");
            session.Session.Sanitize(headerRegexSanitizer);

            Assert.Equal("SuperDifferentCookie", targetEntry.Request.Headers[targetKey][0]);
            Assert.Equal("REDACTEDCookie", targetEntry.Request.Headers[targetKey][1]);
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
        public void BodyRegexSanitizerIgnoresNonTextualBodies()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/request_with_binary_content.json");
            var targetEntry = session.Session.Entries[0];
            var content = Encoding.UTF8.GetString(targetEntry.Request.Body);

            var bodyRegexSanitizer = new BodyRegexSanitizer(regex: ".*");
            session.Session.Sanitize(bodyRegexSanitizer);

            Assert.Equal(content, Encoding.UTF8.GetString(targetEntry.Request.Body));
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

            var newBody = Encoding.UTF8.GetString(targetEntry.Request.Body);
            Assert.Contains(replacementValue, newBody);
            Assert.Equal("{\"TableName\":\"sanitized.tablename\"}", newBody);
        }

        [Fact]
        public void BodyKeySanitizerIgnoresNulls()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/response_with_null_secrets.json");
            var targetEntry = session.Session.Entries[0];
            var replacementValue = "sanitized.tablename";
            var originalBody = Encoding.UTF8.GetString(targetEntry.Request.Body);
            var bodyKeySanitizer = new BodyKeySanitizer(jsonPath: "$.connectionString", value: replacementValue);
            session.Session.Sanitize(bodyKeySanitizer);

            var newBody = Encoding.UTF8.GetString(targetEntry.Request.Body);
            Assert.Equal(originalBody, newBody);
        }

        [Fact]
        public void BodyKeySanitizerHandlesNonJSON()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");
            var targetEntry = session.Session.Entries[0];
            var replacementValue = "sanitized.tablename";

            var bodyKeySanitizer = new BodyKeySanitizer(jsonPath: "$.TableName", value: replacementValue);
            session.Session.Sanitize(bodyKeySanitizer);

            Assert.DoesNotContain(replacementValue, Encoding.UTF8.GetString(targetEntry.Request.Body));
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
            var originalValue = Encoding.UTF8.GetString(targetEntry.Request.Body);
            session.Session.Sanitize(bodyKeySanitizer);
            var newValue = Encoding.UTF8.GetString(targetEntry.Request.Body);

            Assert.DoesNotContain(replacementValue, newValue);
            Assert.Equal(originalValue, newValue);
        }


        [Fact]
        public void ContinuationSanitizerSingleReplace()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/requests_with_continuation.json");
            var continueSanitizer = new ContinuationSanitizer("correlationId", "guid", resetAfterFirst: "false");
            var targetKey = "correlationId";
            var originalRequestGuid = session.Session.Entries[0].Response.Headers[targetKey].First();

            session.Session.Sanitize(continueSanitizer);

            var firstRequest = session.Session.Entries[0].Response.Headers[targetKey].First();
            var firstResponse = session.Session.Entries[1].Request.Headers[targetKey].First();
            var secondRequest = session.Session.Entries[2].Response.Headers[targetKey].First();
            var secondResponse = session.Session.Entries[3].Request.Headers[targetKey].First();

            Assert.NotEqual(originalRequestGuid, firstRequest);
            Assert.Equal(firstRequest, firstResponse);
            Assert.NotEqual(firstRequest, secondRequest);
            Assert.Equal(firstResponse, secondResponse);
        }

        [Fact]
        public void ContinuationSanitizerMultipleReplace()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/requests_with_continuation.json");
            var continueSanitizer = new ContinuationSanitizer("correlationId", "guid", resetAfterFirst: "true");
            var targetKey = "correlationId";
            var originalSendGuid = session.Session.Entries[0].Response.Headers[targetKey].First();

            session.Session.Sanitize(continueSanitizer);

            var firstRequest = session.Session.Entries[0].Response.Headers[targetKey].First();
            var firstResponse = session.Session.Entries[1].Request.Headers[targetKey].First();
            var secondRequest = session.Session.Entries[2].Response.Headers[targetKey].First();
            var secondResponse = session.Session.Entries[3].Request.Headers[targetKey].First();

            Assert.NotEqual(originalSendGuid, firstRequest);
            Assert.Equal(firstRequest, firstResponse);
            Assert.NotEqual(firstResponse, secondRequest);
            Assert.Equal(secondRequest, secondResponse);
        }

        [Fact]
        public void ContinuationSanitizerNonExistentKey()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/requests_with_continuation.json");
            var continueSanitizer = new ContinuationSanitizer("non-existent-key", "guid", resetAfterFirst: "true");
            var targetKey = "correlationId";
            var originalSendGuid = session.Session.Entries[0].Response.Headers[targetKey].First();

            session.Session.Sanitize(continueSanitizer);

            var firstRequest = session.Session.Entries[0].Response.Headers[targetKey].First();
            var firstResponse = session.Session.Entries[1].Request.Headers[targetKey].First();
            var secondRequest = session.Session.Entries[2].Response.Headers[targetKey].First();
            var secondResponse = session.Session.Entries[3].Request.Headers[targetKey].First();

            Assert.Equal(originalSendGuid, firstRequest);
            Assert.Equal(firstRequest, firstResponse);
            Assert.NotEqual(firstResponse, secondRequest);
            Assert.Equal(secondRequest, secondResponse);
        }

        [Fact]
        public void ConditionalSanitizeUriRegexAppliesForRegex()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/response_with_xml_body.json");
            var targetHeader = "x-ms-version";

            var removeHeadersSanitizer = new RemoveHeaderSanitizer(targetHeader, condition: new ApplyCondition() { UriRegex = @".+/Tables.*" });
            session.Session.Sanitize(removeHeadersSanitizer);
            var firstEntry = session.Session.Entries[0];
            // this entry should be untouched by sanitization, it's request URI should not match the regex above
            var secondEntry = session.Session.Entries[1];
            var thirdEntry = session.Session.Entries[2];

            Assert.False(firstEntry.Request.Headers.ContainsKey(targetHeader));
            Assert.True(secondEntry.Request.Headers.ContainsKey(targetHeader));
            Assert.False(thirdEntry.Request.Headers.ContainsKey(targetHeader));
        }

        [Fact]
        public void ConditionalSanitizeUriRegexProperlySkips()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/response_with_xml_body.json");
            var targetHeader = "x-ms-version";

            var removeHeadersSanitizer = new RemoveHeaderSanitizer(targetHeader, condition: new ApplyCondition() { UriRegex = @".+/token" });
            session.Session.Sanitize(removeHeadersSanitizer);
            Assert.DoesNotContain<bool>(false, session.Session.Entries.Select(x => x.Request.Headers.ContainsKey(targetHeader)));
        }

        [Fact]
        public void GenStringSanitizerAppliesForMultipleComponents()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetString = "listtable09bf2a3d";
            var replacementString = "<thistablehasbeenreplaced!>";
            var targetEntry = session.Session.Entries[0];

            var originalRequestBody = Encoding.UTF8.GetString(targetEntry.Request.Body);
            var originalResponseBody = Encoding.UTF8.GetString(targetEntry.Response.Body);
            var originalLocation = targetEntry.Response.Headers["Location"].First().ToString();

            var sanitizer = new GeneralStringSanitizer(targetString, replacementString);
            session.Session.Sanitize(sanitizer);

            var resultRequestBody = Encoding.UTF8.GetString(targetEntry.Request.Body);
            var resultResponseBody = Encoding.UTF8.GetString(targetEntry.Response.Body);
            var resultLocation = targetEntry.Response.Headers["Location"].First().ToString();


            // request body
            Assert.NotEqual(originalRequestBody, resultRequestBody);
            Assert.DoesNotContain(targetString, resultRequestBody);
            Assert.Contains(replacementString, resultRequestBody);
            
            // result body
            Assert.NotEqual(originalResponseBody, resultResponseBody);
            Assert.DoesNotContain(targetString, resultResponseBody);
            Assert.Contains(replacementString, resultResponseBody);
            
            // uri
            Assert.NotEqual(originalLocation, resultLocation);
            Assert.DoesNotContain(targetString, resultLocation);
            Assert.Contains(replacementString, resultLocation);
        }

        [Fact]
        public void GenStringSanitizerQuietExitForAllHttpComponents()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var untouchedSession = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");

            var targetString = ".*";
            var replacementString = "<thistablehasbeenreplaced!>";
            var targetEntry = session.Session.Entries[0];
            var targetUntouchedEntry = untouchedSession.Session.Entries[0];
            var matcher = new RecordMatcher();

            var sanitizer = new GeneralStringSanitizer(targetString, replacementString);
            session.Session.Sanitize(sanitizer);

            var resultRequestBody = Encoding.UTF8.GetString(targetEntry.Request.Body);
            var resultResponseBody = Encoding.UTF8.GetString(targetEntry.Response.Body);
            var resultLocation = targetEntry.Response.Headers["Location"].First().ToString();

            Assert.DoesNotContain(targetString, resultRequestBody);
            Assert.DoesNotContain(targetString, resultResponseBody);
            Assert.DoesNotContain(targetString, resultLocation);

            Assert.Equal(0, matcher.CompareHeaderDictionaries(targetUntouchedEntry.Request.Headers, targetEntry.Request.Headers, new HashSet<string>(), new HashSet<string>()));
            Assert.Equal(0, matcher.CompareHeaderDictionaries(targetUntouchedEntry.Response.Headers, targetEntry.Response.Headers, new HashSet<string>(), new HashSet<string>()));
            Assert.Equal(0, matcher.CompareBodies(targetUntouchedEntry.Request.Body, targetEntry.Request.Body));
            Assert.Equal(0, matcher.CompareBodies(targetUntouchedEntry.Response.Body, targetEntry.Response.Body));
            Assert.Equal(targetUntouchedEntry.RequestUri, targetEntry.RequestUri);
        }

        [Theory]
        [InlineData("Accept-Encoding", ",", "<comma>", "Test.RecordEntries/post_delete_get_content.json")]
        [InlineData("Accept", "*/*", "<starslashstar>", "Test.RecordEntries/oauth_request_with_variables.json")]
        [InlineData("User-Agent", ".19041-SP0", "<useragent>", "Test.RecordEntries/post_delete_get_content.json")]
        public void HeaderStringSanitizerApplies(string targetKey, string targetValue, string replacementValue, string recordingFile)
        {
            var session = TestHelpers.LoadRecordSession(recordingFile);
            var targetEntry = session.Session.Entries[0];
            var originalHeaderValue = targetEntry.Request.Headers[targetKey].First().ToString();
            
            var sanitizer = new HeaderStringSanitizer(targetKey, targetValue, value: replacementValue);
            session.Session.Sanitize(sanitizer);
            
            var resultHeaderValue = targetEntry.Request.Headers[targetKey].First().ToString();

            Assert.NotEqual(resultHeaderValue, originalHeaderValue);
            Assert.Contains(replacementValue, resultHeaderValue);
            Assert.DoesNotContain(targetValue, resultHeaderValue);
        }

        [Theory]
        [InlineData("DataServiceVersion", "application/json", "<replacedString>", "Test.RecordEntries/post_delete_get_content.json")]
        public void HeaderStringSanitizerQuietlyExits(string targetKey, string targetValue, string replacementValue, string recordingFile)
        {
            var session = TestHelpers.LoadRecordSession(recordingFile);
            var untouchedSession = TestHelpers.LoadRecordSession(recordingFile);
            var targetUntouchedEntry = untouchedSession.Session.Entries[0];
            var targetEntry = session.Session.Entries[0];
            var matcher = new RecordMatcher();

            var sanitizer = new HeaderStringSanitizer(targetKey, targetValue, value: replacementValue);
            session.Session.Sanitize(sanitizer);

            Assert.Equal(0, matcher.CompareHeaderDictionaries(targetUntouchedEntry.Request.Headers, targetEntry.Request.Headers, new HashSet<string>(), new HashSet<string>()));
            Assert.Equal(0, matcher.CompareHeaderDictionaries(targetUntouchedEntry.Response.Headers, targetEntry.Response.Headers, new HashSet<string>(), new HashSet<string>()));
        }

        [Theory]
        [InlineData("listtable09bf2a3d", "<replacedtablename>", "Test.RecordEntries/post_delete_get_content.json")]
        [InlineData("%20profile%20offline", "<profilereplaced>", "Test.RecordEntries/oauth_request.json")]
        [InlineData("|,&x-client-last-telemetry=2|0|", "<client>", "Test.RecordEntries/oauth_request.json")]
        [InlineData("}", "<bracket>", "Test.RecordEntries/response_with_null_secrets.json")]
        public void BodyStringSanitizerApplies(string targetValue, string replacementValue, string recordingFile)
        {
            var session = TestHelpers.LoadRecordSession(recordingFile);
            var targetEntry = session.Session.Entries[0];
            var originalBodyValue = Encoding.UTF8.GetString(targetEntry.Request.Body);

            var sanitizer = new BodyStringSanitizer(targetValue, value: replacementValue);
            session.Session.Sanitize(sanitizer);

            var resultBodyValue = Encoding.UTF8.GetString(targetEntry.Request.Body);

            Assert.NotEqual(originalBodyValue, resultBodyValue);
            Assert.Contains(replacementValue, resultBodyValue);
            Assert.DoesNotContain(targetValue, resultBodyValue);
        }

        [Theory]
        [InlineData("TableNames", "<tablename>", "Test.RecordEntries/response_with_null_secrets.json")]
        [InlineData("d2270777-c002-0072-313d-4ce19f000000", "<targetId>", "Test.RecordEntries/response_with_null_secrets.json")]
        [InlineData(".19041-SP0", "<useragent>", "Test.RecordEntries/response_with_null_secrets.json")]
        public void BodyStringSanitizerQuietlyExits(string targetValue, string replacementValue, string recordingFile)
        {
            var session = TestHelpers.LoadRecordSession(recordingFile);
            var untouchedSession = TestHelpers.LoadRecordSession(recordingFile);
            var targetEntry = session.Session.Entries[0];
            var originalBodyValue = Encoding.UTF8.GetString(targetEntry.Request.Body);
            var targetUntouchedEntry = untouchedSession.Session.Entries[0];
            var matcher = new RecordMatcher();

            var sanitizer = new BodyStringSanitizer(targetValue, value: replacementValue);
            session.Session.Sanitize(sanitizer);

            var resultBodyValue = Encoding.UTF8.GetString(targetEntry.Request.Body);
            Assert.Equal(0, matcher.CompareBodies(targetUntouchedEntry.Request.Body, targetEntry.Request.Body));
            Assert.Equal(0, matcher.CompareBodies(targetUntouchedEntry.Response.Body, targetEntry.Response.Body));
        }

        [Fact]
        public void BodyStringSanitizerIgnoresNonTextualBodies()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/request_with_binary_content.json");
            var targetEntry = session.Session.Entries[0];
            var content = Encoding.UTF8.GetString(targetEntry.Request.Body);

            var bodyStringSanitizer = new BodyStringSanitizer("content");
            session.Session.Sanitize(bodyStringSanitizer);

            Assert.Equal(content, Encoding.UTF8.GetString(targetEntry.Request.Body));
        }

        [Theory]
        [InlineData("/v2.0/", "<oath-v2>", "Test.RecordEntries/oauth_request.json")]
        [InlineData("https://management.azure.com/subscriptions/12345678-1234-1234-5678-123456789010", "<partofpath>", "Test.RecordEntries/request_with_subscriptionid.json")]
        [InlineData("?api-version=2019-05-01", "<api-version>", "Test.RecordEntries/request_with_subscriptionid.json")]
        public void UriStringSanitizerApplies(string targetValue, string replacementValue, string recordingFile)
        {
            var session = TestHelpers.LoadRecordSession(recordingFile);
            var untouchedSession = TestHelpers.LoadRecordSession(recordingFile);

            var targetEntry = session.Session.Entries[0];
            var targetUntouchedEntry = untouchedSession.Session.Entries[0];
            var matcher = new RecordMatcher();

            var sanitizer = new UriStringSanitizer(targetValue, replacementValue);
            session.Session.Sanitize(sanitizer);

            var originalUriValue = targetUntouchedEntry.RequestUri.ToString();
            var resultUriValue = targetEntry.RequestUri.ToString();

            Assert.NotEqual(originalUriValue, resultUriValue);
            Assert.Contains(replacementValue, resultUriValue);
            Assert.DoesNotContain(targetValue, resultUriValue);
        }

        [Theory]
        [InlineData("fakeazsdktestaccount2", "<replacementValue!", "Test.RecordEntries/post_delete_get_content.json")]
        public void UriStringSanitizerQuietlyExits(string targetValue, string replacementValue, string targetFile)
        {
            var session = TestHelpers.LoadRecordSession(targetFile);
            var untouchedSession = TestHelpers.LoadRecordSession(targetFile);

            var targetEntry = session.Session.Entries[0];
            var targetUntouchedEntry = untouchedSession.Session.Entries[0];
            var matcher = new RecordMatcher();

            var sanitizer = new UriStringSanitizer(targetValue, replacementValue);
            session.Session.Sanitize(sanitizer);

            Assert.Equal(targetUntouchedEntry.RequestUri, targetEntry.RequestUri);
        }


        // Re-enable w/ Azure/azure-sdk-tools#2900
        //[Theory]
        //[InlineData("batchresponse_00000000-0000-0000-0000-000000000000", "batchresponse_boundary", "Test.RecordEntries/multipart_request.json")]
        //[InlineData("changesetresponse_955358ab-62b1-4d6c-804b-41cebb7c5e42", "changeset_boundry", "Test.RecordEntries/multipart_request.json")]
        //public void GeneralRegexSanitizerAffectsMultipartRequest(string regex, string replacementValue, string targetFile)
        //{
        //    var session = TestHelpers.LoadRecordSession(targetFile);
            
        //    var targetEntry = session.Session.Entries[0];
        //    var matcher = new RecordMatcher();

        //    var sanitizer = new GeneralRegexSanitizer(value: replacementValue, regex: regex);
        //    session.Session.Sanitize(sanitizer);

        //    var bodyString = Encoding.UTF8.GetString(session.Session.Entries[0].Response.Body);

        //    Assert.DoesNotContain(regex, bodyString);
        //    Assert.Contains(replacementValue, bodyString);
        //}
    }
}
