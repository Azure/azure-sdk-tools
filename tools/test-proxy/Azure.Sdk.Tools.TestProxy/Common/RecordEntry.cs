// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class RecordEntry
    {
        // Requests and responses are usually formatted using Newtonsoft.Json that has more relaxed encoding rules
        // To enable us to store more responses as JSON instead of string in Recording files use
        // relaxed settings for roundtrip
        private static readonly JsonWriterOptions WriterOptions = new JsonWriterOptions()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public RequestOrResponse Request { get; set; } = new RequestOrResponse();

        public RequestOrResponse Response { get; set; } = new RequestOrResponse();

        public string RequestUri { get; set; }

        public bool IsTrack1Recording { get; set; }

        public RequestMethod RequestMethod { get; set; }

        public int StatusCode { get; set; }

        public static RecordEntry Deserialize(JsonElement element)
        {
            var record = new RecordEntry();

            if (element.TryGetProperty(nameof(RequestMethod), out JsonElement property))
            {
                record.RequestMethod = RequestMethod.Parse(property.GetString());
            }

            if (element.TryGetProperty(nameof(RequestUri), out property))
            {
                record.RequestUri = property.GetString();
            }

            if (element.TryGetProperty("EncodedRequestUri", out property))
            {
                record.IsTrack1Recording = true;
            }

            if (element.TryGetProperty("RequestHeaders", out property))
            {
                DeserializeHeaders(record.Request.Headers, property);
            }

            if (element.TryGetProperty("RequestBody", out property))
            {
                DeserializeBody(record.Request, property);
            }

            if (element.TryGetProperty(nameof(StatusCode), out property) &&
                property.TryGetInt32(out var statusCode))
            {
                record.StatusCode = statusCode;
            }

            if (element.TryGetProperty("ResponseHeaders", out property))
            {
                DeserializeHeaders(record.Response.Headers, property);
            }

            if (element.TryGetProperty("ResponseBody", out property))
            {
                DeserializeBody(record.Response, property);
            }

            return record;
        }

        private static void DeserializeBody(RequestOrResponse requestOrResponse, in JsonElement property)
        {
            if (property.ValueKind == JsonValueKind.Null)
            {
                requestOrResponse.Body = null;
            }
            else if (IsTextContentType(requestOrResponse.Headers, out Encoding encoding))
            {
                if (property.ValueKind == JsonValueKind.Array)
                {
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (JsonElement item in property.EnumerateArray())
                        {
                            sb.Append(item.GetString());
                        }

                        requestOrResponse.Body = encoding.GetBytes(sb.ToString());
                    }
                }
                else if (property.ValueKind == JsonValueKind.String)
                {
                    requestOrResponse.Body = encoding.GetBytes(property.GetString());
                }
                else
                {
                    requestOrResponse.Body = encoding.GetBytes(property.GetRawText());
                }

                // TODO consider versioning RecordSession so that we can stop doing the below for newly created recordings
                NormalizeJsonBody(requestOrResponse);
            }
            else if (property.ValueKind == JsonValueKind.Array)
            {
                requestOrResponse.Body = Array.Empty<byte>();
            }
            else
            {
                requestOrResponse.Body = Convert.FromBase64String(property.GetString());
            }
        }

        public static void NormalizeJsonBody(RequestOrResponse requestOrResponse)
        {
            if (requestOrResponse.TryGetContentType(out string contentType) && contentType.Contains("json"))
            {
                try
                {
                    // in case the bytes are actually a pre-encoded JSON object, try to parse it
                    using var memoryStream = new MemoryStream();
                    using var writer = new Utf8JsonWriter(memoryStream, WriterOptions);
                    using var document = JsonDocument.Parse(requestOrResponse.Body);
                    document.RootElement.WriteTo(writer);
                    writer.Flush();
                    requestOrResponse.Body = memoryStream.ToArray();
                    RecordedTestSanitizer.UpdateSanitizedContentLength(requestOrResponse);
                }
                catch (JsonException)
                {
                }
            }
        }

        private static void DeserializeHeaders(IDictionary<string, string[]> headers, in JsonElement property)
        {
            foreach (JsonProperty item in property.EnumerateObject())
            {
                if (item.Value.ValueKind == JsonValueKind.Array)
                {
                    var values = new List<string>();
                    foreach (JsonElement headerValue in item.Value.EnumerateArray())
                    {
                        values.Add(headerValue.GetString());
                    }

                    headers[item.Name] = values.ToArray();
                }
                else
                {
                    headers[item.Name] = new[] { item.Value.GetString() };
                }
            }
        }

        public void Serialize(Utf8JsonWriter jsonWriter)
        {
            jsonWriter.WriteStartObject();

            jsonWriter.WriteString(nameof(RequestUri), RequestUri);
            jsonWriter.WriteString(nameof(RequestMethod), RequestMethod.Method);
            jsonWriter.WriteStartObject("RequestHeaders");
            SerializeHeaders(jsonWriter, Request.Headers);
            jsonWriter.WriteEndObject();

            SerializeBody(jsonWriter, "RequestBody", Request.Body, Request.Headers);

            jsonWriter.WriteNumber(nameof(StatusCode), StatusCode);

            jsonWriter.WriteStartObject("ResponseHeaders");
            SerializeHeaders(jsonWriter, Response.Headers);
            jsonWriter.WriteEndObject();

            SerializeBody(jsonWriter, "ResponseBody", Response.Body, Response.Headers);
            jsonWriter.WriteEndObject();
        }

        private void SerializeBody(Utf8JsonWriter jsonWriter, string name, byte[] requestBody, IDictionary<string, string[]> headers)
        {
            if (requestBody == null)
            {
                jsonWriter.WriteNull(name);
            }
            else if (requestBody.Length == 0)
            {
                jsonWriter.WriteStartArray(name);
                jsonWriter.WriteEndArray();
            }
            else if (IsTextContentType(headers, out Encoding encoding))
            {
                // Try parse response as JSON and write it directly if possible
                try
                {
                    using JsonDocument document = JsonDocument.Parse(requestBody);

                    // We use array as a wrapper for string based serialization
                    // so if the root is an array we can't write it directly
                    // fallback to generic string writing. Also, if the root is a string
                    // we don't want to write it directly, as this would make matching
                    // not work in libraries that allow passing JSON as a string.
                    // Finally, if the root is a JSON null, but requestBody was not null, then that means the body actually contained
                    // bytes for "null" and we should instead write it as a string.
                    if (document.RootElement.ValueKind != JsonValueKind.Array &&
                        document.RootElement.ValueKind != JsonValueKind.String &&
                        document.RootElement.ValueKind != JsonValueKind.Null)
                    {
                        using var memoryStream = new MemoryStream();
                        // Settings of this writer should be in sync with the one used in deserialization
                        using (var reformattedWriter = new Utf8JsonWriter(memoryStream, WriterOptions))
                        {
                            document.RootElement.WriteTo(reformattedWriter);
                        }

                        jsonWriter.WritePropertyName(name.AsSpan());
                        document.RootElement.WriteTo(jsonWriter);
                        return;
                    }
                }
                catch (Exception)
                {
                    // ignore
                }

                ReadOnlySpan<char> text = encoding.GetString(requestBody).AsMemory().Span;

                var indexOfNewline = IndexOfNewline(text);
                if (indexOfNewline == -1)
                {
                    jsonWriter.WriteString(name, text);
                }
                else
                {
                    jsonWriter.WriteStartArray(name);
                    do
                    {
                        jsonWriter.WriteStringValue(text.Slice(0, indexOfNewline + 1));
                        text = text.Slice(indexOfNewline + 1);
                        indexOfNewline = IndexOfNewline(text);
                    } while (indexOfNewline != -1);

                    if (!text.IsEmpty)
                    {
                        jsonWriter.WriteStringValue(text);
                    }

                    jsonWriter.WriteEndArray();
                }
            }
            else
            {
                jsonWriter.WriteString(name, Convert.ToBase64String(requestBody));
            }
        }

        private int IndexOfNewline(ReadOnlySpan<char> span)
        {
            int indexOfNewline = span.IndexOfAny('\r', '\n');

            if (indexOfNewline == -1)
            {
                return -1;
            }

            if (span.Length > indexOfNewline + 1 &&
                (span[indexOfNewline + 1] == '\r' ||
                span[indexOfNewline + 1] == '\n'))
            {
                indexOfNewline++;
            }

            return indexOfNewline;
        }

        private void SerializeHeaders(Utf8JsonWriter jsonWriter, IDictionary<string, string[]> header)
        {
            foreach (KeyValuePair<string, string[]> requestHeader in header)
            {
                if (requestHeader.Value.Length == 1)
                {
                    jsonWriter.WriteString(requestHeader.Key, requestHeader.Value[0]);
                }
                else
                {
                    jsonWriter.WriteStartArray(requestHeader.Key);
                    foreach (var value in requestHeader.Value)
                    {
                        jsonWriter.WriteStringValue(value);
                    }

                    jsonWriter.WriteEndArray();
                }
            }
        }

        public static bool TryGetContentType(IDictionary<string, string[]> requestHeaders, out string contentType)
        {
            contentType = null;
            if (requestHeaders.TryGetValue("Content-Type", out var contentTypes) &&
                contentTypes.Length == 1)
            {
                contentType = contentTypes[0];
                return true;
            }
            return false;
        }

        public static bool IsTextContentType(IDictionary<string, string[]> requestHeaders, out Encoding encoding)
        {
            encoding = null;
            return TryGetContentType(requestHeaders, out string contentType) &&
                   ContentTypeUtilities.TryGetTextEncoding(contentType, out encoding);
        }
    }
}
