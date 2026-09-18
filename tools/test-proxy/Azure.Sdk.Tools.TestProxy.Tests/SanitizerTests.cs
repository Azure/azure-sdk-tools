using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Azure.Sdk.Tools.TestProxy.Sanitizers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Xunit;
using Xunit.Abstractions;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class SanitizerTests
    {
        public OAuthResponseSanitizer OAuthResponseSanitizer = new OAuthResponseSanitizer();
        private NullLoggerFactory _nullLogger = new NullLoggerFactory();
        private readonly ITestOutputHelper _output;

        public SanitizerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public string oauthRegex = "\"/oauth2(?:/v2.0)?/token\"";
        public string lookaheadReplaceRegex = @"[a-z]+(?=\.(?:table|blob|queue)\.core\.windows\.net)";
        public string capturingGroupReplaceRegex = @"https\:\/\/(?<account>[a-z]+)\.(?:table|blob|queue)\.core\.windows\.net";
        public string scopeClean = @"scope\=(?<scope>[^&]*)";

        [Theory]
        [InlineData("application/json", "{ \"value\": \"unchanged\" }", "missing", "Sanitized")]
        [InlineData("text/plain", "unchanged", "unchanged", "unchanged")]
        [InlineData("application/xml", "<root>unchanged</root>", "missing", "Sanitized")]
        public void UnchangedTextSanitizationKeepsOriginalBuffer(string contentType, string body, string pattern, string replacement)
        {
            byte[] original = Encoding.UTF8.GetBytes(body);
            var message = new RequestOrResponse { Body = original };
            message.Headers["Content-Type"] = new[] { contentType };
            message.Headers["Content-Length"] = new[] { original.Length.ToString() };

            new BodyRegexSanitizer(regex: pattern, value: replacement).SanitizeBody(message);

            Assert.Same(original, message.Body);
            Assert.Equal(original.Length.ToString(), message.Headers["Content-Length"][0]);
        }

        [Fact]
        public void TextSanitizerBatchAvoidsRepeatedDecoding()
        {
            var sanitizers = Enumerable.Range(0, 21)
                .Select(index => new BodyRegexSanitizer(regex: $"missing{index}"))
                .ToArray();
            var batched = SanitizerBatch.Create(sanitizers).ToArray();
            Assert.Single(batched);
            byte[] body = Encoding.UTF8.GetBytes(new string('a', 65536));
            var entry = new RecordEntry();
            entry.Request.Headers["Content-Type"] = new[] { "application/json" };
            entry.Request.Body = body;
            const int iterations = 20;

            long Measure(RecordedTestSanitizer[] pipeline)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int iteration = 0; iteration < iterations; iteration++)
                {
                    foreach (var sanitizer in pipeline)
                    {
                        sanitizer.Sanitize(entry);
                    }
                }
                return (GC.GetAllocatedBytesForCurrentThread() - before) / iterations;
            }

            Measure(sanitizers);
            Measure(batched);
            long sequentialBytes = Measure(sanitizers);
            long batchedBytes = Measure(batched);

            _output.WriteLine($"21 no-op body regexes, 64 KiB body: sequential {sequentialBytes:N0} bytes/op; batched {batchedBytes:N0} bytes/op.");
            Assert.True(batchedBytes < sequentialBytes / 4);
            Assert.Same(body, entry.Request.Body);
        }

        [Fact]
        public async Task TextSanitizerBatchPreservesHeaderAndBodyOrder()
        {
            var entry = new RecordEntry { RequestUri = "https://example.org/initial" };
            entry.Request.Body = Encoding.UTF8.GetBytes("{\"secret\":\"initial\"}");
            entry.Request.Headers["Content-Type"] = new[] { "application/json" };
            entry.Request.Headers["Content-Length"] = new[] { entry.Request.Body.Length.ToString() };
            entry.Response.Body = (byte[])entry.Request.Body.Clone();
            entry.Response.Headers["Content-Type"] = new[] { "application/json" };
            var sequential = entry.Clone();
            var sanitizers = new RecordedTestSanitizer[]
            {
                new GeneralRegexSanitizer(regex: "initial", value: "first"),
                new BodyKeySanitizer("$.secret", value: "second"),
                new BodyKeySanitizer("$.missing"),
                new HeaderRegexSanitizer("Content-Type", value: "text/plain"),
                new BodyKeySanitizer("$.secret", value: "incorrect"),
                new BodyRegexSanitizer(regex: "second", value: "final"),
                new BodyRegexSanitizer(regex: "final", value: "conditional", condition: new ApplyCondition { UriRegex = "first" })
            };
            foreach (var sanitizer in sanitizers)
            {
                sanitizer.Sanitize(sequential);
            }
            var session = new RecordSession();
            session.Entries.Add(entry);
            await session.Sanitize(sanitizers);

            Assert.Equal(sequential.RequestUri, entry.RequestUri);
            Assert.Equal(sequential.Request.Body, entry.Request.Body);
            Assert.Equal(sequential.Response.Body, entry.Response.Body);
            Assert.Equal(sequential.Request.Headers["Content-Length"], entry.Request.Headers["Content-Length"]);
            Assert.Contains("conditional", Encoding.UTF8.GetString(entry.Request.Body));
        }

        [Fact]
        public void KnownRegexesAreSharedAndPreserveMatching()
        {
            var registry = new SanitizerDictionary();
            var patterns = registry.DefaultSanitizerList
                .SelectMany(item => GetSanitizerRegexes(item.Sanitizer))
                .Select(regex => regex.ToString())
                .Distinct()
                .Where(pattern => KnownSanitizerRegexes.Get(pattern) != null)
                .ToArray();
            Assert.Equal(18, patterns.Length);
            string[] inputs =
            {
                "",
                "ordinary text\nwith a second line",
                "SharedAccessKey=sample;AccountKey=sample;accesskey=sample;Accesskey=sample;Secret=sample;Password=sample;User ID=sample;",
                "https://account.example.org/common/userrealm/realm/identities/identity?sig=sample&sv=sample&token=sample",
                "client_id=sample&client_secret=sample&client_assertion=sample",
                "-----BEGIN PRIVATE KEY-----\nline-one\nline-two\n-----END PRIVATE KEY-----\n"
            };

            foreach (var pattern in patterns)
            {
                var generated = RecordedTestSanitizer.GetRegex(pattern);
                var compiled = new Regex(pattern, RegexOptions.Compiled);
                Assert.Same(generated, RecordedTestSanitizer.GetRegex(pattern));
                Assert.Equal(compiled.GetGroupNames(), generated.GetGroupNames());
                foreach (var input in inputs)
                {
                    Assert.Equal(compiled.Replace(input, "Sanitized"), generated.Replace(input, "Sanitized"));
                    foreach (var group in compiled.GetGroupNames())
                    {
                        Assert.Equal(
                            StringSanitizer.SanitizeValue(input, "Sanitized", compiled, group),
                            StringSanitizer.SanitizeValue(input, "Sanitized", generated, group));
                    }
                }
            }
        }

        [Fact]
        public void DynamicRegexesKeepExistingConstructionAndValidation()
        {
            const string pattern = "custom-(?<value>[0-9]+)";
            var regex = RecordedTestSanitizer.GetRegex(pattern);
            Assert.NotSame(regex, RecordedTestSanitizer.GetRegex(pattern));
            Assert.True(regex.Options.HasFlag(RegexOptions.Compiled));
            Assert.Throws<HttpException>(() => RecordedTestSanitizer.GetRegex("["));
            Assert.Throws<HttpException>(() => RecordedTestSanitizer.GetRegex(null));
        }

        private static IEnumerable<Regex> GetSanitizerRegexes(RecordedTestSanitizer sanitizer)
        {
            foreach (var field in sanitizer.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var value = field.GetValue(sanitizer);
                if (value is Regex regex)
                {
                    yield return regex;
                }
                else if (value is RecordedTestSanitizer child)
                {
                    foreach (var nested in GetSanitizerRegexes(child))
                    {
                        yield return nested;
                    }
                }
            }
        }

        [Theory]
        [InlineData("application/xml", "<root><secret>first</secret><secret>second</secret></root>", "//secret", "<root><secret>Sanitized</secret><secret>Sanitized</secret></root>")]
        [InlineData("text/xml; charset=utf-8", "<root>\n  <secret>first\nsecond</secret>\n</root>", "//secret", "<root>\n  <secret>Sanitized</secret>\n</root>")]
        [InlineData("APPLICATION/SERVICE+XML", "<root xmlns='urn:test'><secret>value</secret></root>", "//*[local-name()='secret']", "<root xmlns=\"urn:test\"><secret>Sanitized</secret></root>")]
        [InlineData("application/xml", "<root secret='value'/>", "/root/@secret", "<root secret=\"Sanitized\" />")]
        [InlineData("application/xml", "<root><secret>value</secret></root>", "/root/secret/text()", "<root><secret>Sanitized</secret></root>")]
        [InlineData("application/xml", "<root><secret><![CDATA[value]]></secret></root>", "//secret", "<root><secret>Sanitized</secret></root>")]
        [InlineData("application/xml", "<root><secret/></root>", "//secret", "<root><secret>Sanitized</secret></root>")]
        [InlineData("application/xml", "<root><secret><child>value</child></secret></root>", "//secret", "<root><secret><child>value</child></secret></root>")]
        [InlineData("application/xml", "<root>unchanged</root>", "//missing", "<root>unchanged</root>")]
        [InlineData("application/xml", "\uFEFF<root>unchanged</root>", "//missing", "\uFEFF<root>unchanged</root>")]
        [InlineData("application/xml", "\uFEFF<root><secret>value</secret></root>", "//secret", "\uFEFF<root><secret>Sanitized</secret></root>")]
        [InlineData("application/json", "{\"secret\":\"value\"}", "//secret", "{\"secret\":\"value\"}")]
        [InlineData("text/plain", "<secret>value</secret>", "//secret", "<secret>value</secret>")]
        [InlineData("application/xml", "", "//secret", "")]
        public void BodyXmlSanitizerSelectsXmlValues(string contentType, string body, string xmlPath, string expected)
        {
            var sanitizer = new BodyXmlSanitizer(xmlPath);
            Assert.Equal(expected, sanitizer.SanitizeTextBody(contentType, body));
        }

        [Theory]
        [InlineData("//secret[")]
        [InlineData("count(//secret)")]
        [InlineData("//unknown:secret")]
        [InlineData(null)]
        public void BodyXmlSanitizerRejectsInvalidPaths(string xmlPath)
        {
            Assert.Throws<HttpException>(() => new BodyXmlSanitizer(xmlPath));
        }

        [Theory]
        [InlineData("<root><secret>unfinished</root>")]
        [InlineData("<!DOCTYPE root [<!ENTITY value 'secret'>]><root>&value;</root>")]
        [InlineData("<!DOCTYPE root SYSTEM 'file:///not-accessed'><root/>")]
        public void BodyXmlSanitizerRejectsUnsafeOrMalformedXml(string body)
        {
            var sanitizer = new BodyXmlSanitizer("//secret");
            Assert.Throws<HttpException>(() => sanitizer.SanitizeTextBody("application/xml", body));
        }

        [Fact]
        public void BodyXmlSanitizerEscapesReplacementValues()
        {
            const string replacement = "<&\"'>]]>";
            var sanitizer = new BodyXmlSanitizer("//secret | //@secret", replacement);
            var sanitized = sanitizer.SanitizeTextBody("application/xml", "<root secret='value'><secret>value</secret></root>");
            var document = new XmlDocument { XmlResolver = null };
            document.LoadXml(sanitized);

            Assert.Equal(replacement, document.DocumentElement.GetAttribute("secret"));
            Assert.Equal(replacement, document.SelectSingleNode("//secret").InnerText);
        }

        [Fact]
        public async Task DefaultXmlSanitizersKeepIdsAndHandleNamespaces()
        {
            var registry = new SanitizerDictionary();
            string[] ids = { "AZSDK3005", "AZSDK3006", "AZSDK3007", "AZSDK3010", "AZSDK3011", "AZSDK3012" };
            var sanitizers = ids.Select(id => registry.Sanitizers[id].Sanitizer).ToArray();
            Assert.All(sanitizers, sanitizer => Assert.IsType<BodyXmlSanitizer>(sanitizer));
            var entry = new RecordEntry();
            entry.Request.Headers["Content-Type"] = new[] { "application/xml" };
            entry.Request.Headers["Content-Length"] = new[] { "0" };
            entry.Request.Body = Encoding.UTF8.GetBytes(
                "<root xmlns='urn:test'>\n<UserDelegationKey><Value>secret\nvalue</Value><SignedTid>tenant</SignedTid><SignedOid>owner</SignedOid></UserDelegationKey>" +
                "<Value>unchanged</Value><PrimaryKey>first</PrimaryKey><PrimaryKey>second</PrimaryKey><SecondaryKey>second</SecondaryKey><ClientIp>127.0.0.1</ClientIp></root>");
            var session = new RecordSession();
            session.Entries.Add(entry);

            await session.Sanitize(sanitizers);

            string body = Encoding.UTF8.GetString(entry.Request.Body);
            Assert.Contains("<Value>MA==</Value>", body);
            Assert.Contains("<Value>unchanged</Value>", body);
            Assert.Contains("<SignedTid>00000000-0000-0000-0000-000000000000</SignedTid>", body);
            Assert.Contains("<SignedOid>00000000-0000-0000-0000-000000000000</SignedOid>", body);
            Assert.Contains("<PrimaryKey>Sanitized</PrimaryKey><PrimaryKey>Sanitized</PrimaryKey>", body);
            Assert.Contains("<SecondaryKey>Sanitized</SecondaryKey><ClientIp>Sanitized</ClientIp>", body);
            Assert.Equal(entry.Request.Body.Length.ToString(), entry.Request.Headers["Content-Length"][0]);
        }

        [Theory]
        [InlineData("<root><secret>initial</secret><secret>other</secret></root>")]
        [InlineData("<?xml version='1.0' encoding='utf-8'?><root xmlns='urn:test'>\r\n  <secret>initial</secret>\r\n</root>")]
        [InlineData("<root><secret><![CDATA[initial]]></secret><!--keep--></root>")]
        [InlineData("<root><secret /></root>")]
        [InlineData("<root>unchanged</root>")]
        public void BodyXmlSanitizerBatchPreservesOrder(string body)
        {
            var sanitizers = new RecordedTestSanitizer[]
            {
                new BodyXmlSanitizer("//*[local-name()='secret']", "first"),
                new BodyXmlSanitizer("//*[local-name()='secret' and text()='first']", "final")
            };
            string expected = body;
            foreach (var sanitizer in sanitizers)
            {
                expected = sanitizer.SanitizeTextBody("application/xml", expected);
            }

            var batched = Assert.Single(BodyXmlSanitizer.Batch(sanitizers));
            Assert.Equal(expected, batched.SanitizeTextBody("application/xml", body));
        }

        [Fact]
        public async Task BodyXmlSanitizerCreatesOverApi()
        {
            var handler = new RecordingHandler(Directory.GetCurrentDirectory());
            await handler.SanitizerRegistry.Clear();
            var context = new DefaultHttpContext();
            context.Request.Headers["x-abstraction-identifier"] = "BodyXmlSanitizer";
            context.Request.Body = TestHelpers.GenerateStreamRequestBody(
                "{\"xmlPath\":\"//secret\",\"value\":\"redacted\",\"condition\":{\"uriRegex\":\"example\"}}");
            context.Request.ContentLength = context.Request.Body.Length;
            var controller = new Admin(handler, _nullLogger)
            {
                ControllerContext = new ControllerContext { HttpContext = context }
            };

            await controller.AddSanitizer();

            var sanitizer = Assert.IsType<BodyXmlSanitizer>(Assert.Single(await handler.SanitizerRegistry.GetSanitizers()));
            var entry = new RecordEntry { RequestUri = "https://example.org" };
            entry.Request.Headers["Content-Type"] = new[] { "application/xml" };
            entry.Request.Body = Encoding.UTF8.GetBytes("<root><secret>value</secret></root>");
            sanitizer.Sanitize(entry);
            Assert.Contains("<secret>redacted</secret>", Encoding.UTF8.GetString(entry.Request.Body));
            entry.RequestUri = "https://other.org";
            entry.Request.Body = Encoding.UTF8.GetBytes("<root><secret>value</secret></root>");
            sanitizer.Sanitize(entry);
            Assert.Contains("<secret>value</secret>", Encoding.UTF8.GetString(entry.Request.Body));
        }

        [Fact]
        public void DefaultRegexInstancesAreSharedAcrossRegistries()
        {
            var first = new SanitizerDictionary().DefaultSanitizerList.SelectMany(item => GetSanitizerRegexes(item.Sanitizer)).ToArray();
            var second = new SanitizerDictionary().DefaultSanitizerList.SelectMany(item => GetSanitizerRegexes(item.Sanitizer)).ToArray();

            Assert.Equal(162, first.Length);
            Assert.Equal(18, first.Distinct().Count());
            Assert.Equal(first.Length, second.Length);
            for (int index = 0; index < first.Length; index++)
            {
                Assert.Same(first[index], second[index]);
            }
        }

        [Fact]
        public void SharedBodyTextIsInvalidatedByRawByteChanges()
        {
            var message = new RequestOrResponse { Body = Encoding.UTF8.GetBytes("initial") };
            message.Headers["Content-Type"] = new[] { "text/plain" };
            message.BeginTextSanitization();
            try
            {
                Assert.True(message.TryGetBodyAsText(out var initial));
                Assert.True(message.TryGetBodyAsText(out var reused));
                Assert.Same(initial, reused);

                message.Body[0] = (byte)'I';
                Assert.True(message.TryGetBodyAsText(out var edited));
                Assert.Equal("Initial", edited);
                message.Body = Encoding.UTF8.GetBytes("replacement");
                Assert.True(message.TryGetBodyAsText(out var replaced));
                Assert.Equal("replacement", replaced);
                message.SetBodyText("final");
                Assert.True(message.TryGetBodyAsText(out var final));
                Assert.Equal("final", final);
            }
            finally
            {
                message.EndTextSanitization();
            }

            Assert.True(message.TryGetBodyAsText(out var after));
            Assert.True(message.TryGetBodyAsText(out var next));
            Assert.NotSame(after, next);
        }

        [Theory]
        [InlineData("post_delete_get_content.json")]
        [InlineData("response_with_xml_body.json")]
        [InlineData("xml_body_with_sas_present.json")]
        [InlineData("multipart_request.json")]
        public async Task DefaultSanitizerBatchMatchesSequentialOutput(string recording)
        {
            var sequential = TestHelpers.LoadRecordSession($"Test.RecordEntries/{recording}").Session;
            var batched = TestHelpers.LoadRecordSession($"Test.RecordEntries/{recording}").Session;
            var sanitizers = new SanitizerDictionary().DefaultSanitizerList.Select(item => item.Sanitizer).ToArray();
            foreach (var sanitizer in sanitizers)
            {
                await sequential.Sanitize(sanitizer);
            }
            await batched.Sanitize(sanitizers);

            Assert.Equal(sequential.Entries.Count, batched.Entries.Count);
            for (int index = 0; index < sequential.Entries.Count; index++)
            {
                var expected = sequential.Entries[index];
                var actual = batched.Entries[index];
                Assert.Equal(expected.RequestUri, actual.RequestUri);
                Assert.Equal(expected.Request.Body, actual.Request.Body);
                Assert.Equal(expected.Response.Body, actual.Response.Body);
                Assert.Equal(System.Text.Json.JsonSerializer.Serialize(expected.Request.Headers), System.Text.Json.JsonSerializer.Serialize(actual.Request.Headers));
                Assert.Equal(System.Text.Json.JsonSerializer.Serialize(expected.Response.Headers), System.Text.Json.JsonSerializer.Serialize(actual.Response.Headers));
            }
        }

        [Fact]
        public async Task BodyXmlSanitizerMatchesSanitizedRecording()
        {
            var request = new RecordEntry { RequestUri = "https://example.org/", RequestMethod = Core.RequestMethod.Post };
            request.Request.Headers["Content-Type"] = new[] { "application/xml" };
            request.Request.Body = Encoding.UTF8.GetBytes("<?xml version='1.0'?><root attribute='keep'>\r\n<PrimaryKey>first</PrimaryKey><Value>&#x41;</Value></root>");
            var recorded = request.Clone();
            recorded.RequestMethod = request.RequestMethod;
            recorded.Request.Body = Encoding.UTF8.GetBytes("<?xml version='1.0'?><root attribute='keep'>\r\n<PrimaryKey>second</PrimaryKey><Value>&#x41;</Value></root>");
            var sanitizers = new RecordedTestSanitizer[]
            {
                new BodyXmlSanitizer("//PrimaryKey"),
                new BodyXmlSanitizer("//missing")
            };
            var session = new RecordSession();
            session.Entries.Add(recorded);
            await session.Sanitize(sanitizers);
            byte[] firstPass = recorded.Request.Body;
            await session.Sanitize(sanitizers);

            Assert.Equal(firstPass, recorded.Request.Body);
            Assert.Same(recorded, session.Lookup(request, new RecordMatcher(), sanitizers, remove: false));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void BodyXmlSanitizerHonorsHeadAndBodyMatching(bool compareBodies)
        {
            var entry = new RecordEntry { RequestMethod = Core.RequestMethod.Head };
            entry.Request.Headers["Content-Type"] = new[] { "application/xml" };
            entry.Response.Headers["Content-Type"] = new[] { "application/xml" };
            entry.Response.Headers["Content-Length"] = new[] { "42" };
            entry.Request.Body = Encoding.UTF8.GetBytes("<secret>original</secret>");
            entry.Response.Body = Encoding.UTF8.GetBytes("<secret>original</secret>");
            var sanitizer = Assert.Single(SanitizerBatch.Create(new RecordedTestSanitizer[]
            {
                new BodyXmlSanitizer("//secret", "first"),
                new BodyXmlSanitizer("//secret", "second")
            }));

            sanitizer.Sanitize(entry, compareBodies);

            Assert.Equal(compareBodies ? "<secret>second</secret>" : "<secret>original</secret>", Encoding.UTF8.GetString(entry.Request.Body));
            Assert.Equal("<secret>original</secret>", Encoding.UTF8.GetString(entry.Response.Body));
            Assert.Equal("42", entry.Response.Headers["Content-Length"][0]);
        }

        [Theory]
        [InlineData("before<![CDATA[middle]]>after")]
        [InlineData("<![CDATA[value]]>")]
        [InlineData("   ")]
        public void BodyXmlSanitizerReplacesLogicalTextNodes(string text)
        {
            const string replacement = "<&>]]>";
            var sanitizer = new BodyXmlSanitizer("//secret/text()", replacement);
            string sanitized = sanitizer.SanitizeTextBody("application/xml", $"<root><secret>{text}</secret></root>");
            var document = new XmlDocument { XmlResolver = null };
            document.LoadXml(sanitized);
            Assert.Equal(replacement, document.SelectSingleNode("//secret").InnerText);
        }

        [Fact]
        public async Task BodyXmlSanitizerHandlesMultipartSections()
        {
            const string body = "--boundary\r\nContent-Type: application/xml\r\nContent-Length: 24\r\n\r\n<secret>initial</secret>\r\n--boundary--\r\n";
            var entry = new RecordEntry();
            entry.Request.Headers["Content-Type"] = new[] { "multipart/mixed; boundary=boundary" };
            entry.Request.Body = Encoding.UTF8.GetBytes(body);
            var session = new RecordSession();
            session.Entries.Add(entry);
            await session.Sanitize(new RecordedTestSanitizer[]
            {
                new BodyXmlSanitizer("//secret", "first"),
                new BodyXmlSanitizer("//secret[text()='first']", "final")
            });

            string sanitized = Encoding.UTF8.GetString(entry.Request.Body);
            Assert.Contains("<secret>final</secret>", sanitized);
            Assert.Contains("Content-Length: 22", sanitized);
            Assert.EndsWith("--boundary--\r\n", sanitized);
        }

        [Fact]
        public async void OauthResponseSanitizerCleansV2AuthRequest()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");

            await session.Session.Sanitize(OAuthResponseSanitizer);

            Assert.Empty(session.Session.Entries);
        }

        [Fact]
        public async void SanitizerDecodesUnicodeAmpersandSanitizesClientIdAndSecret()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/request_with_encoding.json");

            var clientSan = new BodyRegexSanitizer(regex: "(client_id=)(?<cid>[^&\\\"]+)", groupForReplace: "cid");
            var secretSan = new BodyRegexSanitizer(regex: "client_secret=(?<secret>[^&\\\"]+)", groupForReplace: "secret");

            await session.Session.Sanitize(clientSan);
            await session.Session.Sanitize(secretSan);

            Assert.Equal("client_id=Sanitized&grant_type=client_credentials&client_info=1&client_secret=Sanitized&claims=%7B%22access_token=blahblah", Encoding.UTF8.GetString(session.Session.Entries[0].Request.Body));
        }

        [Fact]
        public async void EnsureSASCleanupDoesntOverrunInXML()
        {
            var sanitizerDictionary = new SanitizerDictionary();
            var sessionwithXmlBody = TestHelpers.LoadRecordSession("Test.RecordEntries/xml_body_with_sas_present.json");

            Assert.True(sanitizerDictionary.Sanitizers.TryGetValue("AZSDK1007", out RegisteredSanitizer SASURISanitizer));

            await sessionwithXmlBody.Session.Sanitize(SASURISanitizer.Sanitizer);

            Assert.Contains("<CopyProgress>1024/1024</CopyProgress>", Encoding.UTF8.GetString(sessionwithXmlBody.Session.Entries[0].Response.Body));
        }

        [Fact]
        public async void OauthResponseSanitizerCleansNonV2AuthRequest()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");
            session.Session.Entries[0].RequestUri = "https://login.microsoftonline.com/12345678-1234-1234-1234-123456789012/oauth2/token";

            await session.Session.Sanitize(OAuthResponseSanitizer);

            Assert.Empty(session.Session.Entries);
        }

        [Fact]
        public async void OauthResponseSanitizerNotAggressive()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");

            var expectedCount = session.Session.Entries.Count;

            await session.Session.Sanitize(OAuthResponseSanitizer);

            Assert.Equal(expectedCount, session.Session.Entries.Count);
        }

        [Theory]
        [InlineData("uri", "\"/oauth2(?:/v2.0)?/token\"")]
        [InlineData("body", "\"/oauth2(?:/v2.0)?/token\"")]
        [InlineData("header", "\"/oauth2(?:/v2.0)?/token\"")]
        public async void RegexEntrySanitizerNoOpsOnNonMatch(string target, string regex)
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var sanitizer = new RegexEntrySanitizer(target, regex);
            var expectedCount = session.Session.Entries.Count;

            await session.Session.Sanitize(sanitizer);

            Assert.Equal(expectedCount, session.Session.Entries.Count);
        }

        [Theory]
        [InlineData("body", "(listtable09bf2a3d|listtable19bf2a3d)", 9)]
        [InlineData("uri", "fakeazsdktestaccount", 0)]
        [InlineData("body", "listtable09bf2a3d", 10)]
        [InlineData("header", "a50f2f9c-b830-11eb-b8c8-10e7c6392c5a", 10)]
        public async void RegexEntrySanitizerCorrectlySanitizes(string target, string regex, int endCount)
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var sanitizer = new RegexEntrySanitizer(target, regex);
            var expectedCount = session.Session.Entries.Count;

            await session.Session.Sanitize(sanitizer);

            Assert.Equal(endCount, session.Session.Entries.Count);
        }

        [Fact]
        public async void RegexEntrySanitizerCorrectlySanitizesSpecific()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/response_with_xml_body.json");
            var sanitizer = new RegexEntrySanitizer("header", "b24f75a9-b830-11eb-b949-10e7c6392c5a");
            var expectedCount = session.Session.Entries.Count;

            await session.Session.Sanitize(sanitizer);

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
            await testRecordingHandler.SanitizerRegistry.Clear();
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
            var sanitizer = (await testRecordingHandler.SanitizerRegistry.GetSanitizers())[0];
            Assert.True(sanitizer is RegexEntrySanitizer);


            var sanitizerTarget = (string)typeof(RegexEntrySanitizer).GetField("section", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sanitizer);
            var regex = (Regex)typeof(RegexEntrySanitizer).GetField("rx", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sanitizer);
        }


        [Fact]
        public async void UriRegexSanitizerReplacesTableName()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var originalValue = session.Session.Entries[0].RequestUri;

            var uriSanitizer = new UriRegexSanitizer(value: "fakeaccount", regex: lookaheadReplaceRegex);
            await session.Session.Sanitize(uriSanitizer);

            var testValue = session.Session.Entries[0].RequestUri;

            Assert.True(originalValue != testValue);
            Assert.StartsWith("https://fakeaccount.table.core.windows.net", testValue);
        }

        [Fact]
        public async void UriRegexSanitizerAggressivenessCheck()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");
            var originalValue = session.Session.Entries[0].RequestUri;

            var uriSanitizer = new UriRegexSanitizer(value: "fakeaccount", regex: lookaheadReplaceRegex);
            await session.Session.Sanitize(uriSanitizer);

            var testValue = session.Session.Entries[0].RequestUri;

            Assert.Equal(originalValue, testValue);
        }


        [Fact]
        public async void GeneralRegexSanitizerAppliesToAllSets()
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
            await session.Session.Sanitize(tableNameSanitizer);
            await session.Session.Sanitize(accountNameSanitizer);
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
        public async void voidReplaceRequestSubscriptionId()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/request_with_subscriptionid.json");
            var targetEntry = session.Session.Entries.First();
            var originalUri = targetEntry.RequestUri.ToString();
            var subscriptionIdReplaceSanitizer = new UriSubscriptionIdSanitizer();

            await session.Session.Sanitize(subscriptionIdReplaceSanitizer);
            var sanitizedUri = targetEntry.RequestUri;

            Assert.NotEqual(originalUri, sanitizedUri);
            Assert.StartsWith("/subscriptions/00000000-0000-0000-0000-000000000000/", sanitizedUri.Replace("https://management.azure.com", ""));
            Assert.DoesNotContain("12345678-1234-1234-5678-123456789010", sanitizedUri);
            Assert.Equal(originalUri.Length, sanitizedUri.Length);
        }

        [Fact]
        public async void ReplaceRequestSubscriptionIdNoAction()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");
            var targetEntry = session.Session.Entries.First();
            var originalUri = targetEntry.RequestUri.ToString();
            var subscriptionIdReplaceSanitizer = new UriSubscriptionIdSanitizer();

            await session.Session.Sanitize(subscriptionIdReplaceSanitizer);
            var sanitizedUri = targetEntry.RequestUri;

            // no action should have taken place here.
            Assert.Equal(originalUri, sanitizedUri);
        }

        [Fact]
        public async void HeaderRegexSanitizerSimpleReplace()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];
            var targetKey = "Location";
            var originalHeaderValue = targetEntry.Response.Headers[targetKey].First();

            // where we have a key, a regex, and no groupname.
            var headerRegexSanitizer = new HeaderRegexSanitizer(targetKey, value: "fakeaccount", regex: lookaheadReplaceRegex);
            await session.Session.Sanitize(headerRegexSanitizer);

            var testValue = targetEntry.Response.Headers[targetKey].First();

            Assert.NotEqual(originalHeaderValue, testValue);
            Assert.StartsWith("https://fakeaccount.table.core.windows.net", testValue);
        }




        [Fact]
        public async void HeaderRegexSanitizerGroupedRegexReplace()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetKey = "Location";
            var targetEntry = session.Session.Entries[0];
            var originalHeaderValue = targetEntry.Response.Headers[targetKey].First();

            // where we have a key, a regex, and a groupname to replace with value Y
            var headerRegexSanitizer = new HeaderRegexSanitizer(targetKey, value: "fakeaccount", regex: capturingGroupReplaceRegex, groupForReplace: "account");
            await session.Session.Sanitize(headerRegexSanitizer);

            var testValue = targetEntry.Response.Headers[targetKey].First();

            Assert.NotEqual(originalHeaderValue, testValue);
            Assert.StartsWith("https://fakeaccount.table.core.windows.net", testValue);
        }

        [Fact]
        public async void HeaderRegexSanitizerAggressivenessCheck()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];
            var targetKey = "Content-Type";
            var originalHeaderValue = targetEntry.Response.Headers[targetKey].First();

            // where we find a key, but there is nothing to be done by the sanitizer
            var headerRegexSanitizer = new HeaderRegexSanitizer(targetKey, value: "fakeaccount", regex: capturingGroupReplaceRegex, groupForReplace: "account");
            await session.Session.Sanitize(headerRegexSanitizer);

            var newResult = targetEntry.Response.Headers[targetKey].First();

            Assert.Equal(originalHeaderValue, newResult);
        }

        [Fact]
        public async void BodyRegexSanitizerCleansJSON()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];

            var replaceTableNameRegex = "TableName\"\\s*:\\s*\"(?<tablename>[a-z0-9]+)\"";

            var bodyRegexSanitizer = new BodyRegexSanitizer(value: "afaketable", regex: replaceTableNameRegex, groupForReplace: "tablename");
            await session.Session.Sanitize(bodyRegexSanitizer);

            Assert.Contains("\"TableName\":\"afaketable\"", Encoding.UTF8.GetString(targetEntry.Response.Body));
        }

        [Fact]
        public async void BodyRegexSanitizerCleansText()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");
            var targetEntry = session.Session.Entries[0];

            var bodyRegexSanitizer = new BodyRegexSanitizer(value: "sanitized.scope", regex: scopeClean, groupForReplace: "scope");
            await session.Session.Sanitize(bodyRegexSanitizer);

            var expectedBodyStartsWith = "scope=sanitized.scope&client_id";

            Assert.StartsWith(expectedBodyStartsWith, Encoding.UTF8.GetString(targetEntry.Request.Body));
        }

        [Fact]
        public async void BodyRegexSanitizerIgnoresNonTextualBodies()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/request_with_binary_content.json");
            var targetEntry = session.Session.Entries[0];
            var content = Encoding.UTF8.GetString(targetEntry.Request.Body);

            var bodyRegexSanitizer = new BodyRegexSanitizer(regex: ".*");
            await session.Session.Sanitize(bodyRegexSanitizer);

            Assert.Equal(content, Encoding.UTF8.GetString(targetEntry.Request.Body));
        }

        [Fact]
        public async void BodyRegexSanitizerQuietlyExits()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];

            var beforeUpdate = targetEntry.Request.Body;
            var bodyRegexSanitizer = new BodyRegexSanitizer(value: "fakeaccount", regex: capturingGroupReplaceRegex, groupForReplace: "account");
            await session.Session.Sanitize(bodyRegexSanitizer);

            Assert.Equal(beforeUpdate, targetEntry.Request.Body);
        }

        [Fact]
        public async void RemoveHeaderSanitizerQuietlyExits()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];
            var requestHeaderCountBefore = targetEntry.Request.Headers.Count;

            var removeHeaderSanitizer = new RemoveHeaderSanitizer(headersForRemoval: "fakeaccount");
            await session.Session.Sanitize(removeHeaderSanitizer);

            Assert.Equal(requestHeaderCountBefore, targetEntry.Request.Headers.Count);
        }

        [Fact]
        public async void RemoveHeaderSanitizerRemovesSingleHeader()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];
            var headerForRemoval = "DataServiceVersion";

            var removeHeaderSanitizer = new RemoveHeaderSanitizer(headersForRemoval: headerForRemoval);
            await session.Session.Sanitize(removeHeaderSanitizer);

            Assert.False(targetEntry.Request.Headers.ContainsKey(headerForRemoval));
        }

        [Fact]
        public async void RemoveHeaderSanitizerRemovesMultipleHeaders()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];
            var headerForRemoval = "DataServiceVersion, Date,User-Agent"; // please note the wonky spacing is intentional

            var removeHeaderSanitizer = new RemoveHeaderSanitizer(headersForRemoval: headerForRemoval);
            await session.Session.Sanitize(removeHeaderSanitizer);

            foreach (var header in headerForRemoval.Split(",").Select(x => x.Trim()))
            {
                Assert.False(targetEntry.Request.Headers.ContainsKey(header));
            }
        }

        [Fact]
        public async void BodyKeySanitizerKeyReplace()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];
            var replacementValue = "sanitized.tablename";

            var bodyKeySanitizer = new BodyKeySanitizer(jsonPath: "$.TableName", value: replacementValue);
            await session.Session.Sanitize(bodyKeySanitizer);

            var newBody = Encoding.UTF8.GetString(targetEntry.Request.Body);
            Assert.Contains(replacementValue, newBody);
            Assert.Equal("{\"TableName\":\"sanitized.tablename\"}", newBody);
        }

        [Fact]
        public async void BodyKeySanitizerIgnoresNulls()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/response_with_null_secrets.json");
            var targetEntry = session.Session.Entries[0];
            var replacementValue = "sanitized.tablename";
            var originalBody = Encoding.UTF8.GetString(targetEntry.Request.Body);
            var bodyKeySanitizer = new BodyKeySanitizer(jsonPath: "$.connectionString", value: replacementValue);
            await session.Session.Sanitize(bodyKeySanitizer);

            var newBody = Encoding.UTF8.GetString(targetEntry.Request.Body);
            Assert.Equal(originalBody, newBody);
        }

        [Fact]
        public async void BodyKeySanitizerHandlesNonJSON()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/oauth_request.json");
            var targetEntry = session.Session.Entries[0];
            var replacementValue = "sanitized.tablename";

            var bodyKeySanitizer = new BodyKeySanitizer(jsonPath: "$.TableName", value: replacementValue);
            await session.Session.Sanitize(bodyKeySanitizer);

            Assert.DoesNotContain(replacementValue, Encoding.UTF8.GetString(targetEntry.Request.Body));
        }

        [Fact]
        public async void BodyKeySanitizerRegexReplace()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];

            var bodyKeySanitizer = new BodyKeySanitizer(jsonPath: "$.TableName", value: "TABLE_ID_IS_SANITIZED", regex: @"(?<=listtable)(?<tableid>[a-z0-9]+)", groupForReplace: "tableid");
            await session.Session.Sanitize(bodyKeySanitizer);

            Assert.Contains("listtableTABLE_ID_IS_SANITIZED", Encoding.UTF8.GetString(targetEntry.Response.Body));
        }

        [Fact]
        public async void BodyKeySanitizerQuietlyExits()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetEntry = session.Session.Entries[0];
            var replacementValue = "BodyIsSanitized";

            var bodyKeySanitizer = new BodyKeySanitizer(jsonPath: "$.Location", value: replacementValue);
            var originalValue = Encoding.UTF8.GetString(targetEntry.Request.Body);
            await session.Session.Sanitize(bodyKeySanitizer);
            var newValue = Encoding.UTF8.GetString(targetEntry.Request.Body);

            Assert.DoesNotContain(replacementValue, newValue);
            Assert.Equal(originalValue, newValue);
        }

        [Theory]
        [InlineData("{\"number\":1,\"secret\":\"value\"}", "$.number", "1", "{\"number\":1,\"secret\":\"Sanitized\"}")]
        [InlineData("{\"number\":1,\"secret\":\"value\"}", "$.*", "1", "{\"number\":\"1\",\"secret\":\"Sanitized\"}")]
        [InlineData("{\"first\":\"value\",\"empty\":{},\"secret\":\"value\"}", "$.*", "changed", "{\"first\":\"value\",\"empty\":{},\"secret\":\"Sanitized\"}")]
        [InlineData("{\"secret\":\"value\"}", "$.secret", "intermediate", "{\"secret\":\"Sanitized\"}")]
        [InlineData("[{\"secret\":\"value\"},{\"secret\":null}]", "$..secret", "intermediate", "[{\"secret\":\"intermediate\"},{\"secret\":null}]")]
        [InlineData("{\"timestamp\":\"2026-09-18T10:00:00-07:00\",\"secret\":\"value\"}", "$.secret", "intermediate", "{\"timestamp\":\"2026-09-18T10:00:00-07:00\",\"secret\":\"Sanitized\"}")]
        [InlineData("{ \"other\": null }", "$.missing", "changed", "{ \"other\": null }")]
        [InlineData("null", "$", "changed", "null")]
        [InlineData("\"value\"", "$", "changed", "\"value\"")]
        [InlineData("not json", "$.missing", "changed", "not json")]
        public async Task BodyKeySanitizerSequencePreservesEachRuleSemantics(string body, string firstPath, string firstValue, string expectedBody)
        {
            var entry = new RecordEntry();
            entry.Request.Headers.Add("Content-Type", new[] { "application/json" });
            entry.Request.Headers.Add("Content-Length", new[] { Encoding.UTF8.GetByteCount(body).ToString() });
            entry.Request.Body = Encoding.UTF8.GetBytes(body);
            entry.Response.Headers.Add("Content-Type", new[] { "application/json" });
            entry.Response.Body = Encoding.UTF8.GetBytes(body);

            var sequentialEntry = entry.Clone();
            var sanitizers = new RecordedTestSanitizer[]
            {
                new BodyKeySanitizer(firstPath, value: firstValue),
                new BodyKeySanitizer("$.secret")
            };
            foreach (var sanitizer in sanitizers)
            {
                sanitizer.Sanitize(sequentialEntry);
            }

            var session = new RecordSession();
            session.Entries.Add(entry);
            await session.Sanitize(sanitizers);

            Assert.Equal(expectedBody, Encoding.UTF8.GetString(entry.Request.Body));
            Assert.Equal(sequentialEntry.Request.Body, entry.Request.Body);
            Assert.Equal(sequentialEntry.Response.Body, entry.Response.Body);
            Assert.Equal(sequentialEntry.Request.Headers["Content-Length"], entry.Request.Headers["Content-Length"]);
        }

        [Theory]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        [InlineData("-Infinity")]
        [InlineData("1e400")]
        public void BodyKeySanitizerBatchPreservesJsonRoundTrips(string number)
        {
            var body = $"[{{\"number\":{number},\"secret\":\"initial\"}}]";
            var sanitizers = new RecordedTestSanitizer[]
            {
                new BodyKeySanitizer("$..secret", value: "intermediate"),
                new BodyKeySanitizer("$[?(@.number === 'NaN' || @.number === 'Infinity' || @.number === '-Infinity')].secret", value: "final")
            };
            var expected = body;
            foreach (var sanitizer in sanitizers)
            {
                expected = sanitizer.SanitizeTextBody("application/json", expected);
            }

            var batched = Assert.Single(BodyKeySanitizer.Batch(sanitizers));
            Assert.Contains("\"secret\":\"final\"", expected);
            Assert.Equal(expected, batched.SanitizeTextBody("application/json", body));
        }

        [Fact]
        public void BodyKeySanitizerBatchPreservesMixedRuleOrder()
        {
            var conditional = new BodyKeySanitizer("$.other", value: "incorrect", condition: new ApplyCondition { UriRegex = "does-not-match" });
            var legacy = new BodyKeySanitizer("$.secret", value: "legacy") { LegacyConvertJsonDateTokens = true };
            var derived = new DerivedBodyKeySanitizer();
            var bodyRegex = new BodyRegexSanitizer(regex: "second", value: "after-regex");
            var sanitizers = new RecordedTestSanitizer[]
            {
                new BodyKeySanitizer("$.secret", value: "first"),
                new BodyKeySanitizer("$.secret", regex: "first", value: "second"),
                bodyRegex,
                conditional,
                legacy,
                derived,
                new BodyKeySanitizer("$.secret", regex: "derived", value: "final"),
                new BodyKeySanitizer("$.missing")
            };
            var batched = BodyKeySanitizer.Batch(sanitizers).ToArray();
            Assert.Equal(6, batched.Length);
            Assert.Same(bodyRegex, batched[1]);
            Assert.Same(conditional, batched[2]);
            Assert.Same(legacy, batched[3]);
            Assert.Same(derived, batched[4]);

            var entry = new RecordEntry { RequestUri = "https://localhost/" };
            entry.Request.Headers.Add("Content-Type", new[] { "application/json" });
            entry.Request.Body = Encoding.UTF8.GetBytes("{\"secret\":\"initial\",\"other\":\"unchanged\",\"date\":\"2026-09-18T10:00:00-07:00\"}");
            var sequential = entry.Clone();
            foreach (var sanitizer in sanitizers)
            {
                sanitizer.Sanitize(sequential);
            }
            foreach (var sanitizer in batched)
            {
                sanitizer.Sanitize(entry);
            }

            Assert.Equal(sequential.Request.Body, entry.Request.Body);
            Assert.Contains("\"secret\":\"final\"", Encoding.UTF8.GetString(entry.Request.Body));
            Assert.Contains("\"other\":\"unchanged\"", Encoding.UTF8.GetString(entry.Request.Body));
        }

        [Theory]
        [InlineData("post_delete_get_content.json")]
        [InlineData("request_with_binary_content.json")]
        [InlineData("response_with_xml_body.json")]
        [InlineData("multipart_request.json")]
        public async Task BodyKeySanitizerBatchMatchesSequentialRecordings(string recording)
        {
            var sequential = TestHelpers.LoadRecordSession($"Test.RecordEntries/{recording}").Session;
            var batched = TestHelpers.LoadRecordSession($"Test.RecordEntries/{recording}").Session;
            var sanitizers = new RecordedTestSanitizer[]
            {
                new BodyKeySanitizer("$..TableName", value: "SanitizedTable"),
                new BodyKeySanitizer("$..PartitionKey", value: "SanitizedPartition"),
                new BodyKeySanitizer("$..RowKey", value: "SanitizedRow")
            };
            foreach (var sanitizer in sanitizers)
            {
                await sequential.Sanitize(sanitizer);
            }
            await batched.Sanitize(sanitizers);

            Assert.Equal(sequential.Entries.Count, batched.Entries.Count);
            for (int index = 0; index < sequential.Entries.Count; index++)
            {
                Assert.Equal(sequential.Entries[index].Request.Body, batched.Entries[index].Request.Body);
                Assert.Equal(sequential.Entries[index].Response.Body, batched.Entries[index].Response.Body);
                Assert.Equal(
                    System.Text.Json.JsonSerializer.Serialize(sequential.Entries[index].Request.Headers),
                    System.Text.Json.JsonSerializer.Serialize(batched.Entries[index].Request.Headers));
                Assert.Equal(
                    System.Text.Json.JsonSerializer.Serialize(sequential.Entries[index].Response.Headers),
                    System.Text.Json.JsonSerializer.Serialize(batched.Entries[index].Response.Headers));
            }
        }

        [Fact]
        public async Task BodyKeySanitizerBatchPreservesNestedMultipart()
        {
            var body = string.Join("\r\n", new[]
            {
                "--outer",
                "Content-Type: multipart/mixed; boundary=inner",
                "",
                "--inner",
                "Content-Type: application/json",
                "Content-Length: 20",
                "",
                "{\"secret\":\"initial\"}",
                "--inner--",
                "",
                "--outer--",
                ""
            });
            var entry = new RecordEntry();
            entry.Request.Headers.Add("Content-Type", new[] { "multipart/mixed; boundary=outer" });
            entry.Request.Body = Encoding.UTF8.GetBytes(body);
            var sequential = entry.Clone();
            var sanitizers = new RecordedTestSanitizer[]
            {
                new BodyKeySanitizer("$.secret", value: "intermediate"),
                new BodyKeySanitizer("$.secret", regex: "intermediate", value: "final")
            };
            foreach (var sanitizer in sanitizers)
            {
                sanitizer.Sanitize(sequential);
            }

            var session = new RecordSession();
            session.Entries.Add(entry);
            await session.Sanitize(sanitizers);

            Assert.Equal(sequential.Request.Body, entry.Request.Body);
            Assert.Contains("{\"secret\":\"final\"}", Encoding.UTF8.GetString(entry.Request.Body));
            Assert.Contains("Content-Length: 18", Encoding.UTF8.GetString(entry.Request.Body));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void BodyKeySanitizerBatchRespectsBodyMatching(bool compareBodies)
        {
            var request = new RecordEntry { RequestUri = "https://localhost/", RequestMethod = Core.RequestMethod.Post };
            request.Request.Headers.Add("Content-Type", new[] { "application/json" });
            request.Request.Body = Encoding.UTF8.GetBytes("{\"secret\":\"original\"}");
            var recorded = request.Clone();
            recorded.RequestMethod = request.RequestMethod;
            recorded.Request.Body = Encoding.UTF8.GetBytes("{\"secret\":\"final\"}");
            var session = new RecordSession();
            session.Entries.Add(recorded);
            var sanitizers = new RecordedTestSanitizer[]
            {
                new BodyKeySanitizer("$.secret", value: "intermediate"),
                new BodyKeySanitizer("$.secret", regex: "intermediate", value: "final")
            };

            Assert.Same(recorded, session.Lookup(request, new RecordMatcher(compareBodies: compareBodies), sanitizers, remove: false));
            Assert.Equal(compareBodies ? "{\"secret\":\"final\"}" : "{\"secret\":\"original\"}", Encoding.UTF8.GetString(request.Request.Body));
        }

        [Fact]
        public void BodyKeySanitizerBatchReducesAllocations()
        {
            var sanitizers = Enumerable.Range(0, 96)
                .Select(index => new BodyKeySanitizer($"$..absent{index}"))
                .ToArray();
            var batched = BodyKeySanitizer.Batch(sanitizers).ToArray();
            Assert.Single(batched);
            var body = "{\"payload\":\"" + new string('a', 4096) + "\"}";
            var entry = new RecordEntry();
            entry.Request.Headers.Add("Content-Type", new[] { "application/json" });
            entry.Request.Body = Encoding.UTF8.GetBytes(body);
            const int iterations = 20;

            (long AllocatedBytes, double Milliseconds) Measure(RecordedTestSanitizer[] pipeline)
            {
                var stopwatch = new Stopwatch();
                long before = GC.GetAllocatedBytesForCurrentThread();
                stopwatch.Start();
                for (int iteration = 0; iteration < iterations; iteration++)
                {
                    foreach (var sanitizer in pipeline)
                    {
                        sanitizer.Sanitize(entry);
                    }
                }
                stopwatch.Stop();
                return (GC.GetAllocatedBytesForCurrentThread() - before, stopwatch.Elapsed.TotalMilliseconds);
            }

            Measure(sanitizers);
            Measure(batched);
            var sequentialResult = Measure(sanitizers);
            var batchedResult = Measure(batched);

            _output.WriteLine($"96 rules, {Encoding.UTF8.GetByteCount(body)}-byte JSON body, {iterations} iterations:");
            _output.WriteLine($"Sequential: {sequentialResult.AllocatedBytes / iterations:N0} bytes/op, {sequentialResult.Milliseconds / iterations:F3} ms/op");
            _output.WriteLine($"Batched: {batchedResult.AllocatedBytes / iterations:N0} bytes/op, {batchedResult.Milliseconds / iterations:F3} ms/op");
            Assert.Equal(body, Encoding.UTF8.GetString(entry.Request.Body));
            Assert.True(batchedResult.AllocatedBytes < sequentialResult.AllocatedBytes / 4,
                $"Expected batching to reduce allocations by at least 75%; sequential: {sequentialResult.AllocatedBytes}, batched: {batchedResult.AllocatedBytes}.");
        }

        private class DerivedBodyKeySanitizer : BodyKeySanitizer
        {
            public DerivedBodyKeySanitizer() : base("$.secret")
            {
            }

            public override string SanitizeTextBody(string contentType, string body)
            {
                return body.Replace("legacy", "derived");
            }
        }


        [Fact]
        public async void ContinuationSanitizerSingleReplace()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/requests_with_continuation.json");
            var continueSanitizer = new ContinuationSanitizer("correlationId", "guid", resetAfterFirst: "false");
            var targetKey = "correlationId";
            var originalRequestGuid = session.Session.Entries[0].Response.Headers[targetKey].First();

            await session.Session.Sanitize(continueSanitizer);

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
        public async void ContinuationSanitizerMultipleReplace()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/requests_with_continuation.json");
            var continueSanitizer = new ContinuationSanitizer("correlationId", "guid", resetAfterFirst: "true");
            var targetKey = "correlationId";
            var originalSendGuid = session.Session.Entries[0].Response.Headers[targetKey].First();

            await session.Session.Sanitize(continueSanitizer);

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
        public async void ContinuationSanitizerNonExistentKey()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/requests_with_continuation.json");
            var continueSanitizer = new ContinuationSanitizer("non-existent-key", "guid", resetAfterFirst: "true");
            var targetKey = "correlationId";
            var originalSendGuid = session.Session.Entries[0].Response.Headers[targetKey].First();

            await session.Session.Sanitize(continueSanitizer);

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
        public async void ConditionalSanitizeUriRegexAppliesForRegex()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/response_with_xml_body.json");
            var targetHeader = "x-ms-version";

            var removeHeadersSanitizer = new RemoveHeaderSanitizer(targetHeader, condition: new ApplyCondition() { UriRegex = @".+/Tables.*" });
            await session.Session.Sanitize(removeHeadersSanitizer);
            var firstEntry = session.Session.Entries[0];
            // this entry should be untouched by sanitization, it's request URI should not match the regex above
            var secondEntry = session.Session.Entries[1];
            var thirdEntry = session.Session.Entries[2];

            Assert.False(firstEntry.Request.Headers.ContainsKey(targetHeader));
            Assert.True(secondEntry.Request.Headers.ContainsKey(targetHeader));
            Assert.False(thirdEntry.Request.Headers.ContainsKey(targetHeader));
        }

        [Fact]
        public async void ConditionalSanitizeUriRegexProperlySkips()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/response_with_xml_body.json");
            var targetHeader = "x-ms-version";

            var removeHeadersSanitizer = new RemoveHeaderSanitizer(targetHeader, condition: new ApplyCondition() { UriRegex = @".+/token" });
            await session.Session.Sanitize(removeHeadersSanitizer);
            Assert.DoesNotContain<bool>(false, session.Session.Entries.Select(x => x.Request.Headers.ContainsKey(targetHeader)));
        }

        [Fact]
        public async void GenStringSanitizerAppliesForMultipleComponents()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var targetString = "listtable09bf2a3d";
            var replacementString = "<thistablehasbeenreplaced!>";
            var targetEntry = session.Session.Entries[0];

            var originalRequestBody = Encoding.UTF8.GetString(targetEntry.Request.Body);
            var originalResponseBody = Encoding.UTF8.GetString(targetEntry.Response.Body);
            var originalLocation = targetEntry.Response.Headers["Location"].First().ToString();

            var sanitizer = new GeneralStringSanitizer(targetString, replacementString);
            await session.Session.Sanitize(sanitizer);

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
        public async void GenStringSanitizerQuietExitForAllHttpComponents()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            var untouchedSession = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");

            var targetString = ".*";
            var replacementString = "<thistablehasbeenreplaced!>";
            var targetEntry = session.Session.Entries[0];
            var targetUntouchedEntry = untouchedSession.Session.Entries[0];
            var matcher = new RecordMatcher();

            var sanitizer = new GeneralStringSanitizer(targetString, replacementString);
            await session.Session.Sanitize(sanitizer);

            var resultRequestBody = Encoding.UTF8.GetString(targetEntry.Request.Body);
            var resultResponseBody = Encoding.UTF8.GetString(targetEntry.Response.Body);
            var resultLocation = targetEntry.Response.Headers["Location"].First().ToString();

            Assert.DoesNotContain(targetString, resultRequestBody);
            Assert.DoesNotContain(targetString, resultResponseBody);
            Assert.DoesNotContain(targetString, resultLocation);

            Assert.Equal(0, matcher.CompareHeaderDictionaries(targetUntouchedEntry.Request.Headers, targetEntry.Request.Headers, new HashSet<string>(), new HashSet<string>()));
            Assert.Equal(0, matcher.CompareHeaderDictionaries(targetUntouchedEntry.Response.Headers, targetEntry.Response.Headers, new HashSet<string>(), new HashSet<string>()));

            targetUntouchedEntry.Request.TryGetContentType(out var requestContentType);
            targetEntry.Request.TryGetContentType(out var recordContentType);
            ContentTypeUtilities.TryGetTextEncoding(requestContentType, out var encoding);
            Assert.Equal(0, matcher.CompareBodies(targetUntouchedEntry.Request.Body, targetEntry.Request.Body, requestContentType, recordContentType, encoding));
            Assert.Equal(0, matcher.CompareBodies(targetUntouchedEntry.Response.Body, targetEntry.Response.Body, requestContentType, recordContentType, encoding));
            Assert.Equal(targetUntouchedEntry.RequestUri, targetEntry.RequestUri);
        }

        [Theory]
        [InlineData("Accept-Encoding", ",", "<comma>", "Test.RecordEntries/post_delete_get_content.json")]
        [InlineData("Accept", "*/*", "<starslashstar>", "Test.RecordEntries/oauth_request_with_variables.json")]
        [InlineData("User-Agent", ".19041-SP0", "<useragent>", "Test.RecordEntries/post_delete_get_content.json")]
        public async void HeaderStringSanitizerApplies(string targetKey, string targetValue, string replacementValue, string recordingFile)
        {
            var session = TestHelpers.LoadRecordSession(recordingFile);
            var targetEntry = session.Session.Entries[0];
            var originalHeaderValue = targetEntry.Request.Headers[targetKey].First().ToString();

            var sanitizer = new HeaderStringSanitizer(targetKey, targetValue, value: replacementValue);
            await session.Session.Sanitize(sanitizer);

            var resultHeaderValue = targetEntry.Request.Headers[targetKey].First().ToString();

            Assert.NotEqual(resultHeaderValue, originalHeaderValue);
            Assert.Contains(replacementValue, resultHeaderValue);
            Assert.DoesNotContain(targetValue, resultHeaderValue);
        }

        [Theory]
        [InlineData("DataServiceVersion", "application/json", "<replacedString>", "Test.RecordEntries/post_delete_get_content.json")]
        public async void HeaderStringSanitizerQuietlyExits(string targetKey, string targetValue, string replacementValue, string recordingFile)
        {
            var session = TestHelpers.LoadRecordSession(recordingFile);
            var untouchedSession = TestHelpers.LoadRecordSession(recordingFile);
            var targetUntouchedEntry = untouchedSession.Session.Entries[0];
            var targetEntry = session.Session.Entries[0];
            var matcher = new RecordMatcher();

            var sanitizer = new HeaderStringSanitizer(targetKey, targetValue, value: replacementValue);
            await session.Session.Sanitize(sanitizer);

            Assert.Equal(0, matcher.CompareHeaderDictionaries(targetUntouchedEntry.Request.Headers, targetEntry.Request.Headers, new HashSet<string>(), new HashSet<string>()));
            Assert.Equal(0, matcher.CompareHeaderDictionaries(targetUntouchedEntry.Response.Headers, targetEntry.Response.Headers, new HashSet<string>(), new HashSet<string>()));
        }

        [Theory]
        [InlineData("listtable09bf2a3d", "<replacedtablename>", "Test.RecordEntries/post_delete_get_content.json")]
        [InlineData("%20profile%20offline", "<profilereplaced>", "Test.RecordEntries/oauth_request.json")]
        [InlineData("|,&x-client-last-telemetry=2|0|", "<client>", "Test.RecordEntries/oauth_request.json")]
        [InlineData("}", "<bracket>", "Test.RecordEntries/response_with_null_secrets.json")]
        public async void BodyStringSanitizerApplies(string targetValue, string replacementValue, string recordingFile)
        {
            var session = TestHelpers.LoadRecordSession(recordingFile);
            var targetEntry = session.Session.Entries[0];
            var originalBodyValue = Encoding.UTF8.GetString(targetEntry.Request.Body);

            var sanitizer = new BodyStringSanitizer(targetValue, value: replacementValue);
            await session.Session.Sanitize(sanitizer);

            var resultBodyValue = Encoding.UTF8.GetString(targetEntry.Request.Body);

            Assert.NotEqual(originalBodyValue, resultBodyValue);
            Assert.Contains(replacementValue, resultBodyValue);
            Assert.DoesNotContain(targetValue, resultBodyValue);
        }

        [Theory]
        [InlineData("TableNames", "<tablename>", "Test.RecordEntries/response_with_null_secrets.json")]
        [InlineData("d2270777-c002-0072-313d-4ce19f000000", "<targetId>", "Test.RecordEntries/response_with_null_secrets.json")]
        [InlineData(".19041-SP0", "<useragent>", "Test.RecordEntries/response_with_null_secrets.json")]
        public async void BodyStringSanitizerQuietlyExits(string targetValue, string replacementValue, string recordingFile)
        {
            var session = TestHelpers.LoadRecordSession(recordingFile);
            var untouchedSession = TestHelpers.LoadRecordSession(recordingFile);
            var targetEntry = session.Session.Entries[0];
            var originalBodyValue = Encoding.UTF8.GetString(targetEntry.Request.Body);
            var targetUntouchedEntry = untouchedSession.Session.Entries[0];
            var matcher = new RecordMatcher();

            var sanitizer = new BodyStringSanitizer(targetValue, value: replacementValue);
            await session.Session.Sanitize(sanitizer);

            var resultBodyValue = Encoding.UTF8.GetString(targetEntry.Request.Body);
            targetUntouchedEntry.Request.TryGetContentType(out var requestContentType);
            targetEntry.Request.TryGetContentType(out var recordContentType);
            ContentTypeUtilities.TryGetTextEncoding(requestContentType, out var encoding);
            Assert.Equal(0, matcher.CompareBodies(targetUntouchedEntry.Request.Body, targetEntry.Request.Body, requestContentType, recordContentType, encoding));
            Assert.Equal(0, matcher.CompareBodies(targetUntouchedEntry.Response.Body, targetEntry.Response.Body, requestContentType, recordContentType, encoding));
        }

        [Fact]
        public async void BodyStringSanitizerIgnoresNonTextualBodies()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/request_with_binary_content.json");
            var targetEntry = session.Session.Entries[0];
            var content = Encoding.UTF8.GetString(targetEntry.Request.Body);

            var bodyStringSanitizer = new BodyStringSanitizer("content");
            await session.Session.Sanitize(bodyStringSanitizer);

            Assert.Equal(content, Encoding.UTF8.GetString(targetEntry.Request.Body));
        }

        [Theory]
        [InlineData("/v2.0/", "<oath-v2>", "Test.RecordEntries/oauth_request.json")]
        [InlineData("https://management.azure.com/subscriptions/12345678-1234-1234-5678-123456789010", "<partofpath>", "Test.RecordEntries/request_with_subscriptionid.json")]
        [InlineData("?api-version=2019-05-01", "<api-version>", "Test.RecordEntries/request_with_subscriptionid.json")]
        public async void UriStringSanitizerApplies(string targetValue, string replacementValue, string recordingFile)
        {
            var session = TestHelpers.LoadRecordSession(recordingFile);
            var untouchedSession = TestHelpers.LoadRecordSession(recordingFile);

            var targetEntry = session.Session.Entries[0];
            var targetUntouchedEntry = untouchedSession.Session.Entries[0];
            var matcher = new RecordMatcher();

            var sanitizer = new UriStringSanitizer(targetValue, replacementValue);
            await session.Session.Sanitize(sanitizer);

            var originalUriValue = targetUntouchedEntry.RequestUri.ToString();
            var resultUriValue = targetEntry.RequestUri.ToString();

            Assert.NotEqual(originalUriValue, resultUriValue);
            Assert.Contains(replacementValue, resultUriValue);
            Assert.DoesNotContain(targetValue, resultUriValue);
        }

        [Theory]
        [InlineData("fakeazsdktestaccount2", "<replacementValue!", "Test.RecordEntries/post_delete_get_content.json")]
        public async void UriStringSanitizerQuietlyExits(string targetValue, string replacementValue, string targetFile)
        {
            var session = TestHelpers.LoadRecordSession(targetFile);
            var untouchedSession = TestHelpers.LoadRecordSession(targetFile);

            var targetEntry = session.Session.Entries[0];
            var targetUntouchedEntry = untouchedSession.Session.Entries[0];
            var matcher = new RecordMatcher();

            var sanitizer = new UriStringSanitizer(targetValue, replacementValue);
            await session.Session.Sanitize(sanitizer);

            Assert.Equal(targetUntouchedEntry.RequestUri, targetEntry.RequestUri);
        }

        [Theory]
        [InlineData("true", 0)]
        [InlineData("yes", 153)]
        [InlineData("no", 153)]
        [InlineData("false", 153)]
        [InlineData("gibberish", 153)]
        public void CheckDefaultSanitizerSettings(string environmentSettingValue, int sanitizerCount)
        {
            Environment.SetEnvironmentVariable("TEST_PROXY_DISABLE_DEFAULT_SANITIZERS", environmentSettingValue);

            SanitizerDictionary testDict = new SanitizerDictionary();

            Assert.Equal(sanitizerCount, testDict.DefaultSanitizerList.Count);
        }


    }
}
