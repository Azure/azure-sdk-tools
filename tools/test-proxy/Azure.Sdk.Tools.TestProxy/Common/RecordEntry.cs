// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class RecordEntry
    {
        // Requests and responses are usually formatted using Newtonsoft.Json that has more relaxed encoding rules
        // To enable us to store more responses as JSON instead of string in Recording files use
        // relaxed settings for roundtrip.
        public static readonly JsonWriterOptions WriterOptions = new JsonWriterOptions()
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

        private static byte[] DeserializeMultipartBody(JsonElement property, string boundary)
        {
            using var ms = new MemoryStream();

            foreach (var item in property.EnumerateArray())
            {
                // Handle the “empty binary part” marker: []
                if (item.ValueKind == JsonValueKind.Array)
                {
                    // nothing to write – it really was a 204 / empty body
                    continue;
                }

                var segment = item.GetString();

                if (segment.StartsWith("b64:", StringComparison.Ordinal))
                {
                    var bytes = Convert.FromBase64String(segment.Substring(4));
                    ms.Write(bytes);
                }
                else
                {
                    // Delimiter lines, headers, and text bodies are ASCII by spec.
                    ms.Write(Encoding.ASCII.GetBytes(segment));
                }
            }

            return ms.ToArray();
        }

        private static void DeserializeBody(RequestOrResponse requestOrResponse, in JsonElement property)
        {
            if (property.ValueKind == JsonValueKind.Null)
            {
                requestOrResponse.Body = null;
            }
            else if (ContentTypeUtilities.IsTextContentType(requestOrResponse.Headers, out Encoding encoding))
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
            else if (ContentTypeUtilities.IsMultipartMixed(requestOrResponse.Headers, out var boundary) && property.ValueKind == JsonValueKind.Array)
            {
                requestOrResponse.Body = DeserializeMultipartBody(property, boundary);
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
            if (requestOrResponse.TryGetContentType(out string contentType) && contentType.Contains("json") && !ContentTypeUtilities.IsManifestContentType(contentType))
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

        private static byte[] ReadAllBytes(Stream s)
        {
            if (s is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> seg))
                return seg.AsSpan(seg.Offset, seg.Count).ToArray();
            
            using var copy = new MemoryStream();

            int first = s.ReadByte();
            if (first == -1)
                return Array.Empty<byte>();

            copy.WriteByte((byte)first);
            s.CopyTo(copy);
            return copy.ToArray();
        }

        /// <summary>
        /// This function is necessary because while the MultipartReader REQUIRES a payload that follows the spec for multipart/mixed,
        /// azure services don't actually return totally compliant mixed bodies. A lot of the time they merely include LF--<boundary> instead of the spec-required
        /// CRLF--<boundary>
        /// 
        /// So what we do is 
        /// </summary>
        /// <param name="buf"></param>
        /// <returns></returns>
        /// Rewrites a complete multipart entity so that every header line
        /// (from the delimiter up to the first blank line) ends with CR LF,
        /// and every delimiter line starts with CR LF. The body region is
        /// left byte‑for‑byte intact.
        private static byte[] NormalizeBareLf(byte[] src)
        {
            const byte CR = 0x0D, LF = 0x0A;

            var dst = new byte[src.Length + 32];
            int w = 0;
            bool atLineStart = true;
            bool inHeaders = false;  // true between delimiter and first blank line

            for (int i = 0; i < src.Length; i++)
            {
                byte b = src[i];

                // Detect start of a delimiter line to re‑enter header mode
                if (atLineStart && b == 0x2D && i + 1 < src.Length && src[i + 1] == 0x2D)
                    inHeaders = true;

                if (inHeaders && prev == LF && b == LF && dst[w - 2] == CR)
                {
                    dst[w - 1] = CR;   // overwrite the second LF with CR
                    dst[w++] = LF;   // re‑add LF
                    prev = LF;         // current byte already handled
                    continue;
                }

                dst[w++] = b;
                atLineStart = b == LF;

                // First empty line ends header mode
                if (inHeaders && atLineStart &&
                    (i + 1 == src.Length || src[i + 1] == CR || src[i + 1] == LF))
                    inHeaders = false;
            }

            return w == src.Length ? src : dst.AsSpan(0, w).ToArray();
        }

        private static void SerializeMultipartBody(
            Utf8JsonWriter jsonWriter,
            string name,
            byte[] raw,
            string boundary)
        {
            jsonWriter.WriteStartArray(name);

            // Boundary might have been sanitised to "REDACTED"
            if (boundary == "REDACTED")
            {
                ReadOnlySpan<byte> crlf = stackalloc byte[] { 0x0D, 0x0A };
                int idx = raw.AsSpan().IndexOf(crlf);
                if (idx == -1) throw new InvalidDataException("Multipart body missing CRLF.");

                boundary = Encoding.ASCII.GetString(raw, 2, idx - 2); // skip leading "--"
            }

            // Only run the LF→CRLF fixer once at the outermost level
            byte[] fixedRaw = NormalizeBareLf(raw);

            var lol = Encoding.UTF8.GetString(fixedRaw);
            DebugLogger.LogInformation(lol);
            DebugLogger.LogInformation(Convert.ToBase64String(fixedRaw));
            DebugLogger.LogInformation(raw.Length == fixedRaw.Length ? "We didn't insert anything." : "We did insert a character.");
            
            WriteMultipartLines(jsonWriter,
                                new MemoryStream(fixedRaw, writable: false),
                                boundary);

            jsonWriter.WriteEndArray();
        }

        private static bool IsNestedMultipart(
            IDictionary<string, StringValues> headers,
            out string boundary)
        {
            boundary = null;
            if (!headers.TryGetValue("Content-Type", out var v)) return false;

            if (!MediaTypeHeaderValue.TryParse(v[0], out var mt)) return false;
            if (!mt.MediaType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
                return false;

            boundary = mt.Boundary.Value?.Trim('"');
            return !string.IsNullOrEmpty(boundary);
        }

        private static void WriteTextBody(Utf8JsonWriter w, ReadOnlySpan<char> text)
        {
            while (true)
            {
                int idx = text.IndexOf('\n');
                if (idx == -1) break;
                idx += 1;                            // keep '\n'
                w.WriteStringValue(text[..idx]);
                text = text[idx..];
            }
            if (!text.IsEmpty) w.WriteStringValue(text);
        }

        private static void DumpAscii(ReadOnlySpan<byte> bytes, int count = 256)
        {
            var sb = new StringBuilder();
            int n = Math.Min(count, bytes.Length);

            for (int i = 0; i < n; i++)
            {
                byte b = bytes[i];
                sb.Append(b switch
                {
                    0x0D => '␍',   // CR
                    0x0A => '␊',   // LF
                    _ => (char)b
                });
            }
            DebugLogger.LogInformation("‑‑‑‑‑‑‑‑‑‑ first " + n + " bytes ‑‑‑‑‑‑‑‑‑‑");
            DebugLogger.LogInformation(sb.ToString() + Environment.NewLine);
        }

        private static void WriteMultipartLines(
            Utf8JsonWriter jsonWriter,
            Stream stream,
            string boundary)
        {
            var reader = new MultipartReader(boundary, stream);
            string open = $"--{boundary}\r\n";
            string close = $"--{boundary}--\r\n";

            MultipartSection part;
            while ((part = reader.ReadNextSectionAsync()
                                 .GetAwaiter().GetResult()) != null)
            {
                jsonWriter.WriteStringValue(open);

                foreach (var h in part.Headers)
                    jsonWriter.WriteStringValue($"{h.Key}: {h.Value}\r\n");

                jsonWriter.WriteStringValue("\r\n");

                if (IsNestedMultipart(part.Headers, out var nestedBoundary))
                {
                    byte[] fixedRaw = NormalizeBareLf(ReadAllBytes(part.Body));
                    WriteMultipartLines(jsonWriter,
                                        new MemoryStream(fixedRaw, false),
                                        nestedBoundary);
                }
                else if (ContentTypeUtilities.IsTextContentType(part.Headers, out var enc))
                {
                    WriteTextBody(jsonWriter, enc.GetString(ReadAllBytes(part.Body)));
                }
                else
                {
                    byte[] bytes = ReadAllBytes(part.Body);
                    if (bytes.Length == 0)
                    {
                        jsonWriter.WriteStartArray();
                        jsonWriter.WriteEndArray();
                    }
                    else
                    {
                        jsonWriter.WriteStringValue($"b64:{Convert.ToBase64String(bytes)}\r\n");
                    }
                }
            }

            jsonWriter.WriteStringValue(close);
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
            else if (ContentTypeUtilities.IsTextContentType(headers, out Encoding encoding))
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
            else if (ContentTypeUtilities.IsMultipartMixed(headers, out var boundary))
            {
                SerializeMultipartBody(jsonWriter, name, requestBody, boundary);
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

        /// <summary>
        /// Creates a copy of the provided record entry (Only the RequestUri, Request and Response are copied over).
        /// Used primarily for sanitization logging.
        /// </summary>
        /// <returns>The copied record entry.</returns>
        public RecordEntry Clone()
        {
            // Create a copy of the record entry
            var copiedRecordEntry = new RecordEntry();
            copiedRecordEntry.RequestUri = this.RequestUri;

            copiedRecordEntry.Request = new RequestOrResponse();
            copiedRecordEntry.Request.Headers = new SortedDictionary<string, string[]>(this.Request.Headers.ToDictionary(kvp => kvp.Key, kvp => (string[])kvp.Value.Clone()));
            copiedRecordEntry.Request.Body = this.Request.Body != null ? (byte[])this.Request.Body.Clone() : null;

            copiedRecordEntry.Response = new RequestOrResponse();
            copiedRecordEntry.Response.Headers = new SortedDictionary<string, string[]>(this.Response.Headers.ToDictionary(kvp => kvp.Key, kvp => (string[])kvp.Value.Clone()));
            copiedRecordEntry.Response.Body = this.Response.Body != null ? (byte[])this.Response.Body.Clone() : null;
            return copiedRecordEntry;
        }
    }
}
