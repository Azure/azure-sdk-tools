// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class RecordSessionTests
    {
        [Theory]
        [InlineData("{\"json\":\"value\"}", "application/json")]
        [InlineData("{\"json\":\"\\\"value\\\"\"}", "application/json")]
        [InlineData("{\"json\":{\"json\":\"value\"}}", "application/json")]
        [InlineData("[\"json\",\"value\"]", "application/json")]
        [InlineData("[{\"json\":\"value\"},{\"json\":\"value\"}]", "application/json")]
        [InlineData("\"\"", "application/json")]
        [InlineData("invalid json", "application/json")]
        [InlineData("null", "application/json")]
        [InlineData("\"null\"", "application/json")]
        [InlineData(null, "application/json")]
        [InlineData("{}", "application/json")]
        [InlineData("[]", "application/json")]
        [InlineData("19", "application/json")]
        [InlineData("true", "application/json")]
        [InlineData("false", "application/json")]
        [InlineData("{\"a-key\":\"akeywith+inthemiddle\"}", "application/json")]
        [InlineData("{\"json\":\"value\"}", "unknown")]
        [InlineData("multi\rline", "application/xml")]
        [InlineData("multi\r\nline", "application/xml")]
        [InlineData("multi\n\rline\n", "application/xml")]
        [InlineData("", "")]
        [InlineData("true", "")]
        public void CanRoundtripSessionRecord(string body, string contentType)
        {
            byte[] bodyBytes = body != null ? Encoding.UTF8.GetBytes(body) : null;

            var session = new RecordSession();
            session.Variables["a"] = "value a";
            session.Variables["b"] = "value b";

            RecordEntry recordEntry = new RecordEntry();
            recordEntry.Request.Headers.Add("Content-Type", new[] { contentType });
            recordEntry.Request.Headers.Add("Other-Header", new[] { "multi", "value" });
            recordEntry.Request.Body = bodyBytes;
            recordEntry.RequestUri = "url";
            recordEntry.RequestMethod = RequestMethod.Delete;

            recordEntry.Response.Headers.Add("Content-Type", new[] { contentType });
            recordEntry.Response.Headers.Add("Other-Response-Header", new[] { "multi", "value" });

            recordEntry.Response.Body = bodyBytes;
            recordEntry.StatusCode = 202;

            session.Entries.Add(recordEntry);

            var arrayBufferWriter = new ArrayBufferWriter<byte>();
            using var jsonWriter = new Utf8JsonWriter(arrayBufferWriter, new JsonWriterOptions()
            {
                Indented = true, Encoder = RecordEntry.WriterOptions.Encoder
            });
            session.Serialize(jsonWriter);
            jsonWriter.Flush();

            var document = JsonDocument.Parse(arrayBufferWriter.WrittenMemory);
            var deserializedSession = RecordSession.Deserialize(document.RootElement);

            Assert.Equal("value a", deserializedSession.Variables["a"]);
            Assert.Equal("value b", deserializedSession.Variables["b"]);

            RecordEntry deserializedRecord = deserializedSession.Entries.Single();

            Assert.Equal(RequestMethod.Delete, recordEntry.RequestMethod);
            Assert.Equal("url", recordEntry.RequestUri);
            Assert.Equal(202, recordEntry.StatusCode);

            Assert.Equal(new[] { contentType }, deserializedRecord.Request.Headers["content-type"]);
            Assert.Equal(new[] { "multi", "value" }, deserializedRecord.Request.Headers["other-header"]);

            Assert.Equal(new[] { contentType }, deserializedRecord.Response.Headers["content-type"]);
            Assert.Equal(new[] { "multi", "value" }, deserializedRecord.Response.Headers["other-response-header"]);

            Assert.Equal(bodyBytes, deserializedRecord.Request.Body);
            Assert.Equal(bodyBytes, deserializedRecord.Response.Body);
        }

        [Fact]
        public async Task CanRoundTripDockerDigest()
        {
            // get everything organized
            var sampleExpected = "{\n   \"schemaVersion\": 2,\n   \"mediaType\": \"application/vnd.docker.distribution.manifest.v2+json\",\n   \"config\": {\n      \"mediaType\": \"application/vnd.docker.container.image.v1+json\",\n      \"size\"" +
                ": 1472,\n      \"digest\": \"sha256:042a816809aac8d0f7d7cacac7965782ee2ecac3f21bcf9f24b1de1a7387b769\"\n   },\n   \"layers\": [\n      {\n         \"mediaType\": \"application/vnd.docker.image.rootfs.diff.tar.gzip\",\n         \"size\"" + "" +
                ": 3370628,\n         \"digest\": \"sha256:8921db27df2831fa6eaa85321205a2470c669b855f3ec95d5a3c2b46de0442c9\"\n      }\n   ]\n}";
            var testName = "roundtrip.json";
            DefaultHttpContext ctx = new DefaultHttpContext();
            Assets assets = new Assets()
            {
                AssetsRepo = "Azure/azure-sdk-assets-integration",
                AssetsRepoPrefixPath = "pull/scenarios",
                AssetsRepoId = "",
                TagPrefix = "language/tables",
                Tag = "python/tables_fc54d0"
            };
            var folderStructure = new string[]
            {
                GitStoretests.AssetsJson
            };
            var testEntry = new RecordEntry()
            {
                RequestUri = "https://Sanitized.azurecr.io/v2/alpine/manifests/3.17.1",
                RequestMethod = RequestMethod.Get,
                Request = new RequestOrResponse()
                {
                    Headers = new SortedDictionary<string, string[]>()
                    {
                        { "Accept", new string[]{ "application/json", "application/vnd.docker.distribution.manifest.v2+json" } },
                        { "Accept-Encoding", new string[]{ "gzip" } },
                        { "Authorization", new string[]{ "Sanitized" } },
                        { "User-Agent", new string[]{ "azsdk-go-azcontainerregistry/v0.2.2 (go1.21.6; linux)" } },
                    },
                    Body = null,
                },
                StatusCode = 200,
                Response = new RequestOrResponse()
                {
                    Headers = new SortedDictionary<string, string[]>()
                    {
                        { "Access-Control-Expose-Headers", new string[]{ "Docker-Content-Digest", "WWW-Authenticate", "Link","X-Ms-Correlation-Request-Id" } },
                        { "Connection", new string[]{ "keep-alive" } },
                        { "Content-Length", new string[]{ "528" } },
                        { "Content-Type", new string[]{ "application/vnd.docker.distribution.manifest.v2+json" } },
                        { "Date", new string[]{ "Fri, 17 May 2024 21:42:34 GMT" } },
                        { "Docker-Content-Digest", new string[]{ "sha256:93d5a28ff72d288d69b5997b8ba47396d2cbb62a72b5d87cd3351094b5d578a0" } },
                        { "Docker-Distribution-Api-Version", new string[]{ "registry/2.0" } },
                        { "ETag", new string[]{ "\"sha256:93d5a28ff72d288d69b5997b8ba47396d2cbb62a72b5d87cd3351094b5d578a0\"" } },
                        { "Server", new string[]{ "AzureContainerRegistry" } },
                        { "Strict-Transport-Security", new string[]{ "max-age=31536000; includeSubDomains", "max-age=31536000; includeSubDomains" } },
                        { "X-Content-Type-Options", new string[]{ "nosniff" } },
                        { "X-Ms-Client-Request-Id", new string[]{ "" } },
                        { "X-Ms-Correlation-Request-Id", new string[]{ "caf56438-d3ba-469d-a30c-360a4ff536c1" } },
                        { "X-Ms-Request-Id", new string[]{ "Sanitized" } },
                    },
                    Body = Encoding.UTF8.GetBytes(sampleExpected)
                },
            };

            // create the session which will be saved to disk, then save it
            var testFolder = TestHelpers.DescribeTestFolder(assets, folderStructure);
            var handler = new RecordingHandler(testFolder);
            await handler.StartRecordingAsync(testName, ctx.Response);
            var recordingId = ctx.Response.Headers["x-recording-id"].ToString();
            var session = handler.RecordingSessions[recordingId];
            session.Session.Entries.Add(testEntry);
            await handler.StopRecording(recordingId);

            // ensure that we audited properly
            var auditSession = handler.AuditSessions[recordingId];
            var auditItems = TestHelpers.ExhaustQueue<AuditLogItem>(auditSession);

            Assert.Equal(2, auditItems.Count);

            // now load it, did we avoid mangling it?
            var sessionFromDisk = TestHelpers.LoadRecordSession(Path.Combine(testFolder, testName));
            var targetEntry = sessionFromDisk.Session.Entries[0];
            var content = Encoding.UTF8.GetString(targetEntry.Response.Body);
            Assert.Equal(sampleExpected, content);
        }

        [Fact]
        public async Task CheckDigestNotNormalized()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/response_with_content_digest.json");

            DefaultHttpContext ctx = new DefaultHttpContext();
            DefaultHttpContext requestCtx = new DefaultHttpContext();

            var handler = new RecordingHandler(Directory.GetCurrentDirectory());
            var guid = Guid.NewGuid().ToString();

            handler.PlaybackSessions.AddOrUpdate(
                guid,
                session,
                (key, oldValue) => session
            );

            // we know that on disk and when loaded from memory these bytes are totally untouached
            // we need to make certain this is the case during matching as well
            var untouchedBytes = session.Session.Entries[1].Request.Body;

            // define all this stuff where it's easy to observe it
            var testEntry = new RecordEntry()
            {
                RequestUri = "\"https://Sanitized.azurecr.io/v2/hello-world/manifests/test",
                RequestMethod = RequestMethod.Put,
                Request = new RequestOrResponse()
                {
                    Headers = new SortedDictionary<string, string[]>()
                    {
                        { "Accept", new string[]{ "application/json" } },
                        { "Accept-Encoding", new string[]{ "gzip" } },
                        { "Authorization", new string[]{ "Sanitized" } },
                        { "Content-Length", new string[] { "11387" } },
                        { "User-Agent", new string[]{ "azsdk-go-azcontainerregistry/v0.2.2 (go1.22.2; Windows_NT)" } },
                        { "Content-Type", new string[] { "application/vnd.oci.image.index.v1+json" } },
                        { "x-recording-upstream-base-uri", new string[] { "https://Sanitized.azurecr.io" } }
                    },
                    Body = untouchedBytes,
                }
            };

            // now pull it into where it HAS to be, but is a fairly awkward preparation
            var httpRequest = requestCtx.Request;
            var httpResponse = requestCtx.Response;
            httpRequest.Method = testEntry.RequestMethod.ToString();
            httpRequest.Scheme = "https";
            httpRequest.Host = new HostString("Sanitized.azurecr.io");
            httpRequest.Path = "/v2/hello-world/manifests/test";
            foreach (var header in testEntry.Request.Headers)
            {
                httpRequest.Headers[header.Key] = header.Value;
            }
            httpRequest.Body = new MemoryStream(testEntry.Request.Body);

            var requestFeature = requestCtx.Features.Get<IHttpRequestFeature>();
            if (requestFeature != null)
            {
                requestFeature.RawTarget = httpRequest.Path;
            }

            // if we successfully match, the test is working as expected
            await handler.HandlePlaybackRequest(guid, httpRequest, httpResponse);
        }

        [Fact]
        public void EnsureJsonEscaping()
        {
            var shouldNotExist = new string[] {
                "\\u0026", "\\u002B"
            };

            var body = "{\"tags\":{\"hidden-link:/app-insights-resource-id\":\"/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/Lwm_Rg/providers/microsoft.insights/components/FunctionApp1Lwm\"}"
            + ",\"properties\":{\"WEBSITE_CONTENTAZUREFILECONNECTIONSTRING\":\"DefaultEndpointsProtocol=https;AccountName=anaccountname!;AccountKey=aBase64&String+Fake==;EndpointSuffix=core.windows.net\"}}";

            var session = new RecordSession();
            session.Entries.Add(new RecordEntry()
            {
                Response = new RequestOrResponse()
                {
                    Headers = new SortedDictionary<string, string[]>()
                    {
                        {
                            "Content-Type", new string[] { "application/json" }
                        }
                    },
                    Body = Encoding.UTF8.GetBytes(body),
                }
            });

            var tmpDir = Path.GetTempPath();
            var recordSession = Path.Combine(tmpDir, $"{Guid.NewGuid()}.json");
            using var stream = System.IO.File.Create(recordSession);
            var options = new JsonWriterOptions { Indented = true, Encoder = RecordEntry.WriterOptions.Encoder };
            var writer = new Utf8JsonWriter(stream, options);

            session.Serialize(writer);
            writer.Flush();
            stream.Close();

            var text = File.ReadAllText(recordSession);

            foreach(var unicodeChar in shouldNotExist)
            {
                Assert.DoesNotContain(unicodeChar, text);
            }
        }



        [Theory]
        [InlineData("{\"json\":\"value\"}", "application/json")]
        [InlineData("{\"json\":\"\\\"value\\\"\"}", "application/json")]
        [InlineData("{\"json\":{\"json\":\"value\"}}", "application/json")]
        [InlineData("[\"json\",  \"value\"]", "application/json")]
        [InlineData("{\"json\":  1.00000000000000345345445}", "application/json")]
        public void BodyNormalizationWorksWhenMatching(string body, string contentType)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);

            var session = new RecordSession();
            session.Variables["a"] = "value a";
            session.Variables["b"] = "value b";

            RecordEntry recordEntry = new RecordEntry();
            recordEntry.Request.Headers.Add("Content-Type", new[] { contentType });
            recordEntry.Request.Headers.Add("Other-Header", new[] { "multi", "value" });
            recordEntry.Request.Body = bodyBytes;
            recordEntry.RequestUri = "http://localhost/";
            recordEntry.RequestMethod = RequestMethod.Delete;

            recordEntry.Response.Headers.Add("Content-Type", new[] { contentType });
            recordEntry.Response.Headers.Add("Other-Response-Header", new[] { "multi", "value" });

            recordEntry.Response.Body = bodyBytes;
            recordEntry.StatusCode = 202;

            session.Entries.Add(recordEntry);

            var arrayBufferWriter = new ArrayBufferWriter<byte>();
            using var jsonWriter = new Utf8JsonWriter(arrayBufferWriter, new JsonWriterOptions()
            {
                Indented = true
            });
            session.Serialize(jsonWriter);
            jsonWriter.Flush();
            var document = JsonDocument.Parse(arrayBufferWriter.WrittenMemory);
            var deserializedSession = RecordSession.Deserialize(document.RootElement);

            var matcher = new RecordMatcher();
            Assert.NotNull(deserializedSession.Lookup(recordEntry, matcher, new[] { new RecordedTestSanitizer() }));
        }

        [Theory]
        [InlineData("<body>data</body>", "text/xml")]
        [InlineData("{\"json\":\"value\"}", "application/json")]
        [InlineData("{\"json\":\"value\"}", "text/json")]
        [InlineData("{\"json\":\"value\"}", "application/json+param=val")]
        [InlineData("missing", null)]
        public void BodyNormalizationRespectsContentType(string body, string contentType)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);

            var session = new RecordSession();

            RecordEntry recordEntry = new RecordEntry();
            if (contentType != null)
            {
                recordEntry.Request.Headers.Add("Content-Type", new[] { contentType });
            }

            recordEntry.Request.Body = bodyBytes;
            recordEntry.RequestUri = "http://localhost/";
            recordEntry.RequestMethod = RequestMethod.Delete;
            RecordEntry.NormalizeJsonBody(recordEntry.Request);

            if (contentType?.Contains("json") == true)
            {
                Assert.NotSame(bodyBytes, recordEntry.Request.Body);
            }
            else
            {
                Assert.Same(bodyBytes, recordEntry.Request.Body);
            }
        }


        [Fact]
        public void RecordMatcherThrowsExceptionsWithDetails()
        {
            var matcher = new RecordMatcher();

            var requestEntry = new RecordEntry()
            {
                RequestUri = "http://localhost/",
                RequestMethod = RequestMethod.Head,
                Request =
                {
                    Headers =
                    {
                        {"Content-Length", new[] {"41"}},
                        {"Some-Header", new[] {"Random value"}},
                        {"Some-Other-Header", new[] {"V"}}
                    },
                    Body = Encoding.UTF8.GetBytes("This is request body, it's nice and long.")
                }
            };

            RecordEntry[] entries = new[]
            {
                new RecordEntry()
                {
                    RequestUri = "http://remote-host",
                    RequestMethod = RequestMethod.Put,
                    Request =
                    {
                        Headers =
                            {
                                { "Content-Length", new[] { "41"}},
                                { "Some-Header", new[] { "Non-Random value"}},
                                { "Extra-Header", new[] { "Extra-Value" }}
                            },
                        Body = Encoding.UTF8.GetBytes("This is request body, it's nice and long but it also doesn't match.")
                    }
                }
            };

            TestRecordingMismatchException exception = Assert.Throws<TestRecordingMismatchException>(() => matcher.FindMatch(requestEntry, entries));
            Assert.Equal(
                "Unable to find a record for the request HEAD http://localhost/" + Environment.NewLine +
                "Method doesn't match, request <HEAD> record <PUT>" + Environment.NewLine +
                "Uri doesn't match:" + Environment.NewLine +
                "    request <http://localhost/>" + Environment.NewLine +
                "    record  <http://remote-host>" + Environment.NewLine +
                "Header differences:" + Environment.NewLine +
                "    <Some-Header> values differ, request <Random value>, record <Non-Random value>" + Environment.NewLine +
                "    <Some-Other-Header> is absent in record, value <V>" + Environment.NewLine +
                "    <Extra-Header> is absent in request, value <Extra-Value>" + Environment.NewLine +
                "Body differences:" + Environment.NewLine +
                "Request and record bodies do not match at index 40:" + Environment.NewLine +
                "     request: \"e and long.\"" + Environment.NewLine +
                "     record:  \"e and long but it also doesn't\"" + Environment.NewLine +
                "Remaining Entries:" + Environment.NewLine +
                "0: http://remote-host" + Environment.NewLine,
                exception.Message);
        }

        [Fact]
        public void RecordMatcherIgnoresValuesOfIgnoredHeaders()
        {
            var matcher = new RecordMatcher();

            var mockRequest = new RecordEntry()
            {
                RequestUri = "http://localhost",
                RequestMethod = RequestMethod.Put,
                Request =
                    {
                        Headers =
                        {
                            { "Request-Id", new[] { "Non-Random value"}},
                            { "Date", new[] { "Fri, 05 Nov 2020 02:42:26 GMT"} },
                            { "x-ms-date", new[] { "Fri, 05 Nov 2020 02:42:26 GMT"} },
                            { "x-ms-client-request-id", new[] {"non random request id"} },
                            { "x-ms-client-id", new[] {"non random client id"} },
                            { "User-Agent", new[] {"non random sdk"} },
                            { "traceparent", new[] { "non random traceparent" } }
                        }
                    }
            };

            RecordEntry[] entries = new[]
            {
                new RecordEntry()
                {
                    RequestUri = "http://localhost",
                    RequestMethod = RequestMethod.Put,
                    Request =
                    {
                        Headers =
                        {
                            { "Request-Id", new[] { "Some Random value"}},
                            { "Date", new[] { "Fri, 06 Nov 2020 02:42:26 GMT"} },
                            { "x-ms-date", new[] { "Fri, 06 Nov 2020 02:42:26 GMT"} },
                            { "x-ms-client-request-id", new[] {"some random request id"} },
                            { "x-ms-client-id", new[] {"some random client id"} },
                            { "User-Agent", new[] {"some random sdk"} },
                            { "traceparent", new[] {"some random traceparent"} }
                        }
                    }
                }
            };

            Assert.NotNull(matcher.FindMatch(mockRequest, entries));
        }

        [Fact]
        public void RecordMatcherIgnoresLegacyExcludedHeaders()
        {
            var matcher = new RecordMatcher
            {
                ExcludeHeaders = { "some header", "another" }
            };

            var mockRequest = new RecordEntry()
            {
                RequestUri = "http://localhost",
                RequestMethod = RequestMethod.Put,
                Request =
                    {
                        Headers =
                        {
                            { "some header", new[] { "Non-Random value"}},
                        }
                    }
            };

            RecordEntry[] entries = new[]
            {
                new RecordEntry()
                {
                    RequestUri = "http://localhost",
                    RequestMethod = RequestMethod.Put,
                    Request =
                    {
                        Headers =
                        {
                            { "another", new[] { "Some Random value"}},
                        }
                    }
                }
            };

            Assert.NotNull(matcher.FindMatch(mockRequest, entries));
        }

        [Fact(Skip = "Not yet implemented")]
        public void RecordMatcheRequiresPresenceOfIgnoredHeaders()
        {
            var matcher = new RecordMatcher();

            var mockRequest = new RecordEntry()
            {
                RequestUri = "http://localhost",
                RequestMethod = RequestMethod.Put,
                Request =
                {
                    // Request-Id and TraceParent are ignored until we can
                    // re-record all old tests.
                    Headers =
                    {
                        { "Request-Id", new[] { "Some Random value"}},
                        { "Date", new[] { "Fri, 06 Nov 2020 02:42:26 GMT"} },
                        { "x-ms-date", new[] { "Fri, 06 Nov 2020 02:42:26 GMT"} },
                    }
                }
            };

            RecordEntry[] entries = new[]
            {
                new RecordEntry()
                {
                    RequestUri = "http://localhost",
                    RequestMethod = RequestMethod.Put,
                    Request =
                    {
                        Headers =
                        {
                            { "x-ms-client-request-id", new[] {"some random request id"} },
                            { "User-Agent", new[] {"some random sdk"} },
                            { "traceparent", new[] {"some random traceparent"} }
                        }
                    }
                }
            };

            TestRecordingMismatchException exception = Assert.Throws<TestRecordingMismatchException>(() => matcher.FindMatch(mockRequest, entries));

            Assert.Equal(
                "Unable to find a record for the request PUT http://localhost" + Environment.NewLine +
                "Header differences:" + Environment.NewLine +
                "    <Date> is absent in record, value <Fri, 06 Nov 2020 02:42:26 GMT>" + Environment.NewLine +
                "    <x-ms-date> is absent in record, value <Fri, 06 Nov 2020 02:42:26 GMT>" + Environment.NewLine +
                "    <User-Agent> is absent in request, value <some random sdk>" + Environment.NewLine +
                "    <x-ms-client-request-id> is absent in request, value <some random request id>" + Environment.NewLine +
                "Body differences:" + Environment.NewLine,
                exception.Message);
        }

        [Theory]
        [InlineData("http://localhost?VolatileParam1=Value1&param=paramVal", "http://localhost?VolatileParam1=Value2&param=paramVal", true, true)]
        [InlineData("http://localhost?param=paramVal&VolatileParam1=Value1", "http://localhost?param=paramVal&VolatileParam1=Value2", true, true)]
        [InlineData("http://localhost?param=paramVal&VolatileParam1=Value1&VolatileParam2=Value2", "http://localhost?param=paramVal&VolatileParam1=Value3&VolatileParam2=Value3", true, true)]
        // order should still be respected
        [InlineData("http://localhost?param=paramVal&VolatileParam2=Value2&VolatileParam1=Value1", "http://localhost?param=paramVal&VolatileParam1=Value3&VolatileParam2=Value3", true, false)]
        // presence of volatile param is still required
        [InlineData("http://localhost?param=paramVal&VolatileParam1=Value1&VolatileParam2=Value2", "http://localhost?param=paramVal&VolatileParam1=Value3", true, false)]
        // non-volatile param values should be respected
        [InlineData("http://localhost?VolatileParam1=Value1&param=paramVal", "http://localhost?VolatileParam1=Value2&param=paramVal2", true, false)]
        // regression test cases
        [InlineData("http://localhost?VolatileParam1=Value&param=paramVal", "http://localhost?param=paramVal2&VolatileParam1=Value", false, false)]
        [InlineData("http://localhost?VolatileParam1=Value1&param=paramVal", "http://localhost?VolatileParam1=Value2&param=paramVal", false, false)]
        [InlineData("http://localhost?param=paramVal&VolatileParam1=Value1", "http://localhost?param=paramVal&VolatileParam1=Value2", false, false)]
        [InlineData("http://localhost?VolatileParam1=Value1&param=paramVal", "http://localhost?VolatileParam1=Value2&param=paramVal2", false, false)]
        public void RecordMatcherRespectsIgnoredQueryParameters(string requestUri, string entryUri, bool includeVolatile, bool shouldMatch)
        {
            var matcher = new RecordMatcher();
            if (includeVolatile)
            {
                matcher.IgnoredQueryParameters.Add("VolatileParam1");
                matcher.IgnoredQueryParameters.Add("VolatileParam2");
            }

            var mockRequest = new RecordEntry()
            {
                RequestUri = requestUri,
                RequestMethod = RequestMethod.Put,
            };

            RecordEntry[] entries = new[]
            {
                new RecordEntry()
                {
                    RequestUri = entryUri,
                    RequestMethod = RequestMethod.Put
                }
            };

            if (shouldMatch)
            {
                Assert.NotNull(matcher.FindMatch(mockRequest, entries));
            }
            else
            {
                Assert.Throws<TestRecordingMismatchException>(() => matcher.FindMatch(mockRequest, entries));
            }
        }

        [Fact]
        public void RecordMatcherThrowsExceptionsWhenNoRecordsLeft()
        {
            var matcher = new RecordMatcher();

            var mockRequest = new RecordEntry()
            {
                RequestUri = "http://localhost/",
                RequestMethod = RequestMethod.Head
            };

            RecordEntry[] entries = { };

            TestRecordingMismatchException exception = Assert.Throws<TestRecordingMismatchException>(() => matcher.FindMatch(mockRequest, entries));
            Assert.Equal(
                "Unable to find a record for the request HEAD http://localhost/" + Environment.NewLine +
                "No records to match." + Environment.NewLine,
                exception.Message);
        }

        [Fact]
        public async Task RecordingSessionSanitizeSanitizesVariables()
        {
            var sanitizer = new TestSanitizer();
            var session = new RecordSession();
            session.Variables["A"] = "secret";
            session.Variables["B"] = "Totally not a secret";

            await session.Sanitize(sanitizer);

            Assert.Equal("SANITIZED", session.Variables["A"]);
            Assert.Equal("Totally not a SANITIZED", session.Variables["B"]);
        }

        [Theory]
        [InlineData("*", "invalid json", "invalid json")]
        [InlineData("..secret",
                "[{\"secret\":\"I should be sanitized\"},{\"secret\":\"I should be sanitized\"}]",
                "[{\"secret\":\"Sanitized\"},{\"secret\":\"Sanitized\"}]")]
        [InlineData("$..secret",
                "{\"secret\":\"I should be sanitized\",\"level\":{\"key\":\"value\",\"secret\":\"I should be sanitized\"}}",
                "{\"secret\":\"Sanitized\",\"level\":{\"key\":\"value\",\"secret\":\"Sanitized\"}}")]
        public void RecordingSessionSanitizeTextBody(string jsonPath, string body, string expected)
        {
            var sanitizer = new RecordedTestSanitizer();
            sanitizer.JsonPathSanitizers.Add(jsonPath);

            string response = sanitizer.SanitizeTextBody(default, body);

            Assert.Equal(expected, response);
        }

        [Fact]
        public void RecordingSessionSanitizeTextBodyMultipleValues()
        {
            var sanitizer = new RecordedTestSanitizer();
            sanitizer.JsonPathSanitizers.Add("$..secret");
            sanitizer.JsonPathSanitizers.Add("$..topSecret");

            var body = "{\"secret\":\"I should be sanitized\",\"key\":\"value\",\"topSecret\":\"I should be sanitized\"}";
            var expected = "{\"secret\":\"Sanitized\",\"key\":\"value\",\"topSecret\":\"Sanitized\"}";

            string response = sanitizer.SanitizeTextBody(default, body);

            Assert.Equal(expected, response);
        }

        [Theory]
        [InlineData("Content-Type")]
        [InlineData("Accept")]
        [InlineData("Random-Header")]
        public void SpecialHeadersNormalizedForMatching(string name)
        {
            // Use HttpClientTransport as it does header normalization
            var originalRequest = new HttpClientTransport().CreateRequest();
            originalRequest.Method = RequestMethod.Get;
            originalRequest.Uri.Reset(new Uri("http://localhost"));
            originalRequest.Headers.Add(name, "application/json;odata=nometadata");
            originalRequest.Headers.Add("Date", "This should be ignored");

            var playbackRequest = new MockTransport().CreateRequest();
            playbackRequest.Method = RequestMethod.Get;
            playbackRequest.Uri.Reset(new Uri("http://localhost"));
            playbackRequest.Headers.Add(name, "application/json;odata=nometadata");
            playbackRequest.Headers.Add("Date", "It doesn't match");

            var matcher = new RecordMatcher();
            var requestEntry = RecordTransport.CreateEntry(originalRequest, null);
            var entry = RecordTransport.CreateEntry(playbackRequest, new MockResponse(200));

            Assert.NotNull(matcher.FindMatch(requestEntry, new[] { entry }));
        }
        
        [Theory]
        [InlineData("Content-Type")]
        [InlineData("Accept")]
        [InlineData("Random-Header")]
        public void SpecialHeadersNormalizedForMatchingMultiValue(string name)
        {
            // Use HttpClientTransport as it does header normalization
            var originalRequest = new HttpClientTransport().CreateRequest();
            originalRequest.Method = RequestMethod.Get;
            originalRequest.Uri.Reset(new Uri("http://localhost"));
            originalRequest.Headers.Add(name, "application/json, text/json");
            originalRequest.Headers.Add("Date", "This should be ignored");

            var playbackRequest = new MockTransport().CreateRequest();
            playbackRequest.Method = RequestMethod.Get;
            playbackRequest.Uri.Reset(new Uri("http://localhost"));
            playbackRequest.Headers.Add(name, "application/json, text/json");
            playbackRequest.Headers.Add("Date", "It doesn't match");

            var matcher = new RecordMatcher();
            var requestEntry = RecordTransport.CreateEntry(originalRequest, null);
            var entry = RecordTransport.CreateEntry(playbackRequest, new MockResponse(200));

            Assert.NotNull(matcher.FindMatch(requestEntry, new[] { entry }));
        }

        [Fact]
        public void ContentLengthNotChangedOnHeadRequestWithEmptyBody()
        {
            ContentLengthUpdatedCorrectlyOnEmptyBody(isHeadRequest: true);
        }

        [Fact]
        public void ContentLengthResetToZeroOnGetRequestWithEmptyBody()
        {
            ContentLengthUpdatedCorrectlyOnEmptyBody(isHeadRequest: false);
        }

        private void ContentLengthUpdatedCorrectlyOnEmptyBody(bool isHeadRequest)
        {
            var sanitizer = new RecordedTestSanitizer();
            var entry = new RecordEntry()
            {
                RequestUri = "http://localhost/",
                RequestMethod = isHeadRequest ? RequestMethod.Head : RequestMethod.Get,
                Response =
                {
                    Headers =
                    {
                        {"Content-Length", new[] {"41"}},
                        {"Some-Header", new[] {"Random value"}},
                        {"Some-Other-Header", new[] {"V"}}
                    },
                    Body = new byte[0]
                }
            };
            sanitizer.Sanitize(entry);

            if (isHeadRequest)
            {
                Assert.Equal(new[] { "41" }, entry.Response.Headers["Content-Length"]);
            }
            else
            {
                Assert.Equal(new[] { "0" }, entry.Response.Headers["Content-Length"]);
            }
        }

        private class TestSanitizer : RecordedTestSanitizer
        {
            public override string SanitizeVariable(string variableName, string environmentVariableValue)
            {
                return environmentVariableValue.Replace("secret", "SANITIZED");
            }
        }
    }
}
