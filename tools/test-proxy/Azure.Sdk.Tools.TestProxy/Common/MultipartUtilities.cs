using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public static class MultipartUtilities
    {
        public static readonly byte[] CrLf = "\r\n"u8.ToArray();

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
            if (boundary == "REDACTED" || boundary == "BOUNDARY" || boundary.EndsWith("00000000-0000-0000-0000-000000000000"))
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

        /// <summary>
        /// Given a multipart body, remove the boundary delimiters and return only the content.
        /// Used for normalizing multipart bodies for comparison.
        /// Uses <see cref="ReadOnlySequence{T}"/> to avoid unnecessary allocations.
        /// </summary>
        /// <param name="body">The body of a multipart request or response.</param>
        /// <param name="boundary">The boundary.</param>
        /// <param name="encoding">The encoding to use for comparing known strings to parts of the <paramref name="body"/>.</param>
        public static ReadOnlySequence<byte> RemoveBoundaries(byte[] body, ReadOnlySpan<byte> boundary, Encoding encoding)
        {
            if (body == null || body.Length == 0)
            {
                return ReadOnlySequence<byte>.Empty;
            }

            if (boundary == null || boundary.Length == 0)
            {
                return new ReadOnlySequence<byte>(body);
            }

            Segment first = null;
            Segment last = null;
            int position = 0;

            RemoveCurrentBoundary(body, boundary, ref position, ref first, ref last, encoding.GetBytes("Content-Type: multipart/"), encoding.GetBytes("boundary="));

            if (first == null)
            {
                return ReadOnlySequence<byte>.Empty;
            }

            return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        }

        /// <summary>
        /// Add a new segment to the <see cref="ReadOnlySequence{T}"/>.
        /// </summary>
        /// <param name="first">First <see cref="Segment"/> in the sequence.</param>
        /// <param name="last">Last <see cref="Segment"/> in the sequence.</param>
        /// <param name="body">The original byte array.</param>
        /// <param name="start">The start index of the slice.</param>
        /// <param name="length">The length of the slice.</param>
        private static void AddSlice(ref Segment first, ref Segment last, byte[] body, int start, int length)
        {
            if (length <= 0) return;

            var mem = new ReadOnlyMemory<byte>(body, start, length);
            if (first == null)
            {
                first = last = new Segment(mem, 0);
            }
            else
            {
                last = last.Append(mem);
            }
        }

        /// <summary>
        /// Recursively remove boundaries from a multipart body.
        /// Each part itself can be another multipart body.
        /// </summary>
        /// <param name="body">The original byte array.</param>
        /// <param name="boundary">Current boundary we are processing.</param>
        /// <param name="position">Current position in the <paramref name="body"/>.</param>
        /// <param name="first">First <see cref="Segment"/> of the sequence.</param>
        /// <param name="last">Last <see cref="Segment"/> of the sequence.</param>
        /// <param name="contentTypeMpfdBytes">Static byte array representation of "Content-Type: multipart/" in the correct encoding.</param>
        /// <param name="boundaryBytes">Static byte array representation of "boundary=" in the correct encoding.</param>
        private static void RemoveCurrentBoundary(
            byte[] body,
            ReadOnlySpan<byte> boundary,
            ref int position,
            ref Segment first,
            ref Segment last,
            ReadOnlySpan<byte> contentTypeMpfdBytes,
            ReadOnlySpan<byte> boundaryBytes)
        {
            ReadOnlySpan<byte> span = body.AsSpan();
            int spanLength = span.Length;
            int boundaryBytesLength = boundaryBytes.Length;

            const byte DASH = 0x2D;

            // Scan line by line (CRLF terminated). We will skip lines that are boundary delimiter lines:
            // --boundary
            // --boundary--
            int boundaryLength = boundary.Length;
            int partStart = position;
            while (position < spanLength)
            {
                int crlfIndex = span.Slice(position).IndexOf(CrLf);
                if (crlfIndex == -1)
                {
                    AddSlice(ref first, ref last, body, partStart, spanLength - partStart);
                    break;
                }

                ReadOnlySpan<byte> line = span.Slice(position, crlfIndex);
                position += crlfIndex + 2;

                // if we have a new content type with a boundary recursively process it
                //        "Content-Type: multipart/mixed; boundary=changesetresponse_b03ffa4a-53fc-4036-8a02-509eec67ca53\r\n",
                if (line.StartsWith(contentTypeMpfdBytes))
                {
                    int boundaryBytesIndex = line.IndexOf(boundaryBytes);
                    if (boundaryBytesIndex != -1)
                    {
                        int innerBoundaryStart = boundaryBytesIndex + boundaryBytesLength;
                        ReadOnlySpan<byte> innerBoundary = line.Slice(innerBoundaryStart, crlfIndex - innerBoundaryStart);
                        RemoveCurrentBoundary(body, innerBoundary, ref position, ref first, ref last, contentTypeMpfdBytes, boundaryBytes);
                        partStart = position;
                        continue;
                    }
                }

                bool isBoundaryLine = false;
                bool isEndBoundaryLine = false;
                if (line.Length >= 2 + boundaryLength)
                {
                    // Must start with "--"
                    if (line[0] == DASH && line[1] == DASH)
                    {
                        var afterDashes = line.Slice(2);
                        if (afterDashes.StartsWith(boundary))
                        {
                            int remaining = afterDashes.Length - boundaryLength;
                            isBoundaryLine = remaining == 0;
                            isEndBoundaryLine = remaining == 2 && afterDashes[boundaryLength] == DASH && afterDashes[boundaryLength + 1] == DASH;
                        }
                    }
                }

                if (isBoundaryLine || isEndBoundaryLine)
                {
                    AddSlice(ref first, ref last, body, partStart, position - (crlfIndex + 2) - partStart);
                    partStart = position;
                }

                if (isEndBoundaryLine)
                {
                    break; // end boundary, stop processing this layer
                }
            }
        }

        public static string NormalizeFilenameFromContentDispositionValue(string value)
        {
            if (string.IsNullOrEmpty(value) || !value.Contains("filename"))
            {
                return value;
            }

            var spanValue = value.AsSpan();
            var semicolonIndex = spanValue.IndexOf(';');
            if (semicolonIndex == -1) // no header parameters
            {
                return value;
            }

            var outputValue = new StringBuilder(value.Length + 5);
            var dispositionType = spanValue[..(semicolonIndex + 1)];
            outputValue.Append(dispositionType); // disposition type with ;

            var remaining = spanValue[(semicolonIndex + 1)..];

            // parse each parameter
            while (!remaining.IsEmpty)
            {
                // leading white space
                int nonWhitespace = 0;
                while (nonWhitespace < remaining.Length && char.IsWhiteSpace(remaining[nonWhitespace]))
                {
                    nonWhitespace++;
                }
                outputValue.Append(remaining[..nonWhitespace]);
                remaining = remaining[nonWhitespace..];

                // get param slice: "name=value" and param name and value
                var nextSemicolon = remaining.IndexOf(';');
                var param = nextSemicolon == -1 ? remaining : remaining.Slice(0, nextSemicolon + 1);
                var equalIndex = param.IndexOf('=');
                var paramName = equalIndex == -1 ? param : param.Slice(0, equalIndex);
                var paramValue = equalIndex == -1 ? ReadOnlySpan<char>.Empty : param.Slice(equalIndex + 1);

                if (paramName.SequenceEqual("filename".AsSpan()))
                {
                    outputValue.Append(paramName);
                    outputValue.Append('=');
                    
                    // normalize \ to /
                    var backslash = paramValue.IndexOf("\\");
                    while (backslash != -1)
                    {
                        var first = paramValue[..backslash];
                        var second = paramValue[(backslash + 1)..];
                        outputValue.Append(first);
                        outputValue.Append('/');
                        paramValue = second;
                        backslash = paramValue.IndexOf("\\");
                    }
                    outputValue.Append(paramValue);
                }
                else if (paramName.SequenceEqual("filename*".AsSpan()))
                {
                    outputValue.Append(paramName);
                    outputValue.Append('=');

                    // Get encoding
                    var firstQuote = paramValue.IndexOf('\'');
                    var encodingSpan = firstQuote == -1 ? ReadOnlySpan<char>.Empty : paramValue[..firstQuote].ToString();
                    var encodingString = encodingSpan.IsEmpty ? "UTF-8" : encodingSpan.ToString();
                    outputValue.Append(encodingSpan);
                    outputValue.Append('\'');
                    Encoding encoding = Encoding.GetEncoding(encodingString);

                    // Get value
                    var encodedValue = paramValue[(firstQuote + 1)..];
                    var secondQuote = encodedValue.IndexOf('\'');
                    var lang = encodedValue[..secondQuote];
                    outputValue.Append(lang);
                    outputValue.Append('\'');
                    encodedValue = encodedValue[(secondQuote + 1)..];

                    // Decode
                    var decoded = HttpUtility.UrlDecode(encodedValue.ToString(), encoding);
                    var decodedSpan = decoded.AsSpan();

                    // normalize \ to /
                    StringBuilder normalizedEncoded = new(decodedSpan.Length);
                    var backslash = decodedSpan.IndexOf("\\");
                    while (backslash != -1)
                    {
                        var first = decodedSpan[..backslash];
                        var second = decodedSpan[(backslash + 1)..];
                        normalizedEncoded.Append(first);
                        normalizedEncoded.Append('/');
                        decodedSpan = second;
                        backslash = decodedSpan.IndexOf("\\");
                    }
                    normalizedEncoded.Append(decodedSpan);

                    // Encode again
                    var reEncoded = HttpUtility.UrlEncode(normalizedEncoded.ToString(), encoding);
                    outputValue.Append(reEncoded);
                }
                else
                {
                    outputValue.Append(param); // append entire param if not filename or filename*
                }
                remaining = nextSemicolon == -1 ? ReadOnlySpan<char>.Empty : remaining[(nextSemicolon + 1)..];
            }
            return outputValue.ToString();
        }

        /// <summary>
        /// A class representing a segment in a <see cref="ReadOnlySequence{T}"/>.
        /// </summary>
        private sealed class Segment : ReadOnlySequenceSegment<byte>
        {
            public Segment(ReadOnlyMemory<byte> memory, long runningIndex)
            {
                Memory = memory;
                RunningIndex = runningIndex;
            }

            public Segment Append(ReadOnlyMemory<byte> memory)
            {
                var next = new Segment(memory, RunningIndex + Memory.Length);
                Next = next;
                return next;
            }
        }

        /// <summary>
        /// Compares to <see cref="ReadOnlySequence{T}"/> instances for equality, returning the index and length of the first segment that has a difference.
        /// </summary>
        /// <param name="a">The first sequence to compare.</param>
        /// <param name="b">The second sequence to compare.</param>
        /// <param name="encoding">The encoding of the bytes to compare.</param>
        /// <param name="index">The index of the first difference.</param>
        /// <param name="length">The length of the differing segment.</param>
        /// <returns>True if the sequences are equal; otherwise, false.</returns>
        public static bool SequenceEqual(this in ReadOnlySequence<byte> a, in ReadOnlySequence<byte> b, Encoding encoding, out int index, out int length)
        {
            index = 0;
            length = 0;

            // skipping length compare to get the index of first difference

            if (a.IsSingleSegment && b.IsSingleSegment)
            {
                bool equal = a.FirstSpan.SequenceEqual(b.FirstSpan);
                if (!equal)
                {
                    length = (int)Math.Min(a.Length, b.Length);
                }
                return equal;
            }

            var ra = new SequenceReader<byte>(a);
            var rb = new SequenceReader<byte>(b);

            while (!ra.End && !rb.End)
            {
                var sa = ra.UnreadSpan;
                var sb = rb.UnreadSpan;
                length = Math.Min(sa.Length, sb.Length);

                index = (int)ra.Consumed;
                var slice1 = sa.Slice(0, length);
                var slice2 = sb.Slice(0, length);
                if (!slice1.SequenceEqual(slice2))
                {
                    // TODO
                    return false;
                }

                ra.Advance(length);
                rb.Advance(length);
            }

            return ra.End && rb.End;
        }

        /// <summary>
        /// Given a slice of a <see cref="ReadOnlySequence{T}"/>, convert it to a string using the provided encoding.
        /// </summary>
        /// <param name="seq">The sequence to convert.</param>
        /// <param name="index">The starting index of the slice.</param>
        /// <param name="length">The length of the slice.</param>
        /// <param name="encoding">The encoding to use.</param>
        /// <returns>The string representation of the slice.</returns>
        public static string SliceToString(this in ReadOnlySequence<byte> seq, long index, long length, Encoding encoding)
        {
            encoding ??= Encoding.UTF8;
            var slice = seq.Slice(index, length);

            if (slice.IsSingleSegment)
                return encoding.GetString(slice.FirstSpan);

            return encoding.GetString(slice.ToArray());
        }
    }
}
