// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Sdk.Tools.TestProxy.Common;
using Moq;
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
        [InlineData("{ \"json\": \"value\" }", "unknown")]
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
                Indented = true
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
                "     record:  \"e and long but it also doesn't\"" + Environment.NewLine,
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
        public void RecordingSessionSanitizeSanitizesVariables()
        {
            var sanitizer = new TestSanitizer();
            var session = new RecordSession();
            session.Variables["A"] = "secret";
            session.Variables["B"] = "Totally not a secret";

            session.Sanitize(sanitizer);

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
