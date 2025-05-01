using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text;
using System;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Newtonsoft.Json.Linq;
using System.Net;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public static class MultipartUtilities
    {
        public static readonly byte[] CrLf = new byte[] { (byte)'\r', (byte)'\n' };

        public static byte[] ReadAllBytes(Stream s)
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
        /// azure services don't actually return totally compliant mixed bodies. A lot of the time they merely include LF--boundaryabc123 instead of the spec-required
        /// CRLF--boundaryabc123
        /// 
        /// This function rewrites a complete multipart entity so that every header line
        /// (from the delimiter up to the first blank line) ends with CR LF,
        /// and every delimiter line starts with CR LF. The body region is
        /// left byte‑for‑byte intact.
        /// </summary>
        /// <param name="src">The byte buffer we need to update.</param>
        /// <returns></returns>
        public static byte[] NormalizeBareLf(byte[] src)
        {
            const byte CR = 0x0D, LF = 0x0A, DASH = 0x2D;

            var dst = new byte[src.Length + 1000];
            int w = 0;
            bool atLineStart = true;
            bool inHeaders = false;

            for (int i = 0; i < src.Length; i++)
            {
                byte b = src[i];

                // 1. a delimiter line means the next lines are headers
                if (atLineStart && b == DASH && i + 1 < src.Length && src[i + 1] == DASH)
                    inHeaders = true;

                // 2. inside headers, look ahead for the pattern LF LF
                if (inHeaders && b == LF && i + 1 < src.Length && src[i + 1] == LF)
                {
                    // we’re on the *first* LF of LF LF
                    // ensure we output CR LF CR LF
                    if (w == 0 || dst[w - 1] != CR) dst[w++] = CR;
                    dst[w++] = LF;   // current LF
                    dst[w++] = CR;   // injected CR before second LF
                    dst[w++] = LF;   // second LF
                    i++;             // skip over original second LF
                    atLineStart = true;
                    inHeaders = false;   // blank line ends header block
                    continue;
                }

                // 3. bare LF at end of a non‑blank header line
                if (inHeaders && b == LF && (w == 0 || dst[w - 1] != CR))
                    dst[w++] = CR;

                dst[w++] = b;
                atLineStart = b == LF;

                // 4. CR LF CR LF already correct → leave header mode
                if (inHeaders && atLineStart &&
                    i + 1 < src.Length && (src[i + 1] == CR || src[i + 1] == LF))
                    inHeaders = false;
            }

            var fixedBytes = w == src.Length ? src : dst.AsSpan(0, w).ToArray();

            if (src.Length != w)
            {
                if (DebugLogger.CheckLogLevel(LogLevel.Debug))
                {
                    var beforeText = Convert.ToBase64String(src);
                    var afterText = Convert.ToBase64String(fixedBytes);
                    DebugLogger.LogDebug($"We updated the multipart body from length {src.Length} to length {w}");
                    DebugLogger.LogDebug($"Base64 before: {beforeText}");
                    DebugLogger.LogDebug($"Base64 after: {afterText}");
                }
            }

            return fixedBytes;
        }

        public static string ResolveFirstBoundary(string boundary, byte[] raw)
        {
            // Boundary might have been sanitised to "REDACTED"
            if (boundary == "REDACTED" || boundary.EndsWith("00000000-0000-0000-0000-000000000000"))
            {
                ReadOnlySpan<byte> crlf = stackalloc byte[] { 0x0D, 0x0A };
                int idx = raw.AsSpan().IndexOf(crlf);
                if (idx == -1) throw new InvalidDataException("Multipart body missing CRLF.");

                boundary = Encoding.ASCII.GetString(raw, 2, idx - 2); // skip leading "--"
            }

            return boundary;
        }

        public static bool IsNestedMultipart(
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

        public static void WriteTextBody(Utf8JsonWriter w, ReadOnlySpan<char> text)
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

        public static void DumpAscii(ReadOnlySpan<byte> bytes, int count = 256)
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
        public static void SerializeMultipartBody(
            Utf8JsonWriter jsonWriter,
            string name,
            byte[] raw,
            string boundary)
        {
            jsonWriter.WriteStartArray(name);

            // Boundary might have been sanitised to "REDACTED"
            boundary = ResolveFirstBoundary(boundary, raw);

            // Only run the LF→CRLF fixer once at the outermost level
            byte[] fixedRaw = NormalizeBareLf(raw);

            WriteMultipartLines(jsonWriter,
                                new MemoryStream(fixedRaw, writable: false),
                                boundary);

            jsonWriter.WriteEndArray();
        }

        public static void WriteMultipartLines(
            Utf8JsonWriter jsonWriter,
            Stream stream,
            string boundary)
        {
            byte[] buf = NormalizeBareLf(ReadAllBytes(stream));
            var reader = new MultipartReader(boundary, new MemoryStream(buf, false));

            string open = $"--{boundary}\r\n";
            string close = $"--{boundary}--\r\n";
            try
            {

                MultipartSection part;
                while ((part = reader.ReadNextSectionAsync().GetAwaiter().GetResult()) != null)
                {
                    jsonWriter.WriteStringValue(open);

                    foreach (var h in part.Headers)
                        jsonWriter.WriteStringValue($"{h.Key}: {h.Value}\r\n");
                    jsonWriter.WriteStringValue("\r\n");

                    if (IsNestedMultipart(part.Headers, out var childBoundary))
                    {
                        WriteMultipartLines(jsonWriter, part.Body, childBoundary);
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
                            jsonWriter.WriteStartArray(); jsonWriter.WriteEndArray();
                        }
                        else
                        {
                            jsonWriter.WriteStringValue($"b64:{Convert.ToBase64String(bytes)}");
                        }
                    }

                    jsonWriter.WriteStringValue("\r\n");
                }
            }
            catch (IOException ex)
            {
                var byteContent = Convert.ToBase64String(buf);
                string message = $$"""
The test-proxy is unexpectedly unable to read this section of the config during serialization: \"{{ex.Message}}\"
File an issue on Azure/azure-sdk-tools and include this base64 string for reproducibility:
{{byteContent}}
""";

                throw new HttpException(HttpStatusCode.InternalServerError, message);
            }
            jsonWriter.WriteStringValue(close);
        }

        public static byte[] DeserializeMultipartBody(JsonElement property, string boundary)
        {
            // this is a patch for the _old_ way of storing `multipart/mixed` recordings. On disk, `ResponseBody` was just a pure base64 string.
            // the bytes just need to be read exactly as they are.
            if (property.ValueKind == JsonValueKind.String)
            {
                return Convert.FromBase64String(property.GetString());
            }

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
    }
}
