// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    /// <summary>
    /// The default sanitizer that is always applied. Removes the header "Authorization" and replaces it with the value "Sanitized".
    /// </summary>
    public class RecordedTestSanitizer
    {
        public const string SanitizeValue = "Sanitized";

        public string SanitizerId { get; set; }

        public List<string> JsonPathSanitizers { get; } = new List<string>();

        public ApplyCondition Condition { get; protected set; } = null;

        /// <summary>
        /// Indicates which parts of a RecordEntry this sanitizer applies to.
        /// By default, only applies to headers (Authorization header sanitization).
        /// Derived sanitizers should override this to specify their scope.
        /// </summary>
        protected SanitizerScope _scope = SanitizerScope.Header;

        /// <summary>
        /// Checks if this sanitizer applies to a given part of the record.
        /// </summary>
        protected bool AppliesTo(SanitizerScope part) => (_scope & part) != 0;

        /// This is just a temporary workaround to avoid breaking tests that need to be re-recorded
        //  when updating the JsonPathSanitizer logic to avoid changing date formats when deserializing requests.
        //  this property will be removed in the future.
        public bool LegacyConvertJsonDateTokens { get; set; }

        private static readonly string[] s_sanitizeValueArray = { SanitizeValue };

        public static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None
        };

        public List<string> SanitizedHeaders { get; } = new List<string> { "Authorization" };

        /// <summary>
        /// Abstraction for getting a compiled Regex from a string. Used by derived classes to cache their compiled regexes.
        /// </summary>
        /// <param name="regex">The regular expression pattern to compile.</param>
        public static Regex GetRegex(string regex)
        {
            try
            {
                return new Regex(regex, RegexOptions.Compiled);
            }
            catch (Exception e)
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"Expression of value {regex} does not successfully compile. Failure Details: {e.Message}");
            }
        }

        public virtual string SanitizeUri(string uri)
        {
            return uri;
        }

        public virtual void SanitizeHeaders(IDictionary<string, string[]> headers)
        {
            foreach (var header in SanitizedHeaders)
            {
                if (headers.ContainsKey(header))
                {
                    headers[header] = s_sanitizeValueArray;
                }
            }
        }

        public virtual string SanitizeTextBody(string contentType, string body)
        {
            if (JsonPathSanitizers.Count == 0)
                return body;
            try
            {
                JToken jsonO;
                // Prevent default behavior where JSON.NET will convert DateTimeOffset
                // into a DateTime.
                if (!LegacyConvertJsonDateTokens)
                {
                    jsonO = JsonConvert.DeserializeObject<JToken>(body, SerializerSettings);
                }
                else
                {
                    jsonO = JToken.Parse(body);
                }

                foreach (string jsonPath in JsonPathSanitizers)
                {
                    foreach (JToken token in jsonO.SelectTokens(jsonPath))
                    {
                        token.Replace(JToken.FromObject(SanitizeValue));
                    }
                }
                return JsonConvert.SerializeObject(jsonO, SerializerSettings);
            }
            catch
            {
                return body;
            }
        }

        public virtual byte[] SanitizeBody(string contentType, byte[] body)
        {
            return body;
        }

        public virtual string SanitizeVariable(string variableName, string environmentVariableValue) => environmentVariableValue;

        public byte[] SanitizeMultipartBody(string boundary, byte[] raw)
        {
            // Boundary might have been sanitised to "REDACTED", handle that.
            boundary = MultipartUtilities.ResolveFirstBoundary(boundary, raw);

            // Only run the LF→CRLF fixer once at the outermost level
            // the reason we still do this instead of just using the body as-is, is that we may be loading up
            // a recording from before we started storing the corrected multipart/mixed body.
            byte[] fixedRaw = MultipartUtilities.NormalizeBareLf(raw);

            var reader = new MultipartReader(boundary, new MemoryStream(fixedRaw));
            using var outStream = new MemoryStream();

            byte[] boundaryStart = Encoding.ASCII.GetBytes($"--{boundary}\r\n");
            byte[] boundaryClose = Encoding.ASCII.GetBytes($"--{boundary}--\r\n");

            try
            {
                MultipartSection section;
                while ((section = reader.ReadNextSectionAsync()
                                         .GetAwaiter()
                                         .GetResult()) != null)
                {
                    // we actually need to figure out the sanitization length _before_ we look at the headers
                    // as if there is a Content-Length header we need to update it to reflect the new reality
                    byte[] original = MultipartUtilities.ReadAllBytes(section.Body);
                    byte[] fixedBody = MultipartUtilities.NormalizeBareLf(original);
                    byte[] newBody;

                    if (MultipartUtilities.IsNestedMultipart(section.Headers, out var childBoundary))
                    {
                        newBody = SanitizeMultipartBody(childBoundary, fixedBody);
                    }
                    else if (ContentTypeUtilities.IsTextContentType(section.Headers, out var enc))
                    {
                        var sanitised = SanitizeTextBody(section.ContentType, enc.GetString(fixedBody));
                        newBody = enc.GetBytes(sanitised);
                    }
                    else
                    {
                        // binary or application/http part – no extra sanitising,
                        // but keep CR/LF corrections we just applied
                        newBody = fixedBody;
                    }

                    // 1) opening boundary
                    outStream.Write(boundaryStart);
                    // 2) headers (by spec must be ASCII encoded)
                    foreach (var h in section.Headers)
                    {
                        var newValue = h.Value;
                        if (h.Key == "Content-Length")
                        {
                            var parsed = int.TryParse(h.Value[0], out var contentLength);
                            if (parsed && contentLength != newBody.Length)
                            {
                                newValue = new StringValues(newBody.Length.ToString());
                            }
                        }
                        var headerLine = $"{h.Key}: {newValue}\r\n";
                        outStream.Write(Encoding.ASCII.GetBytes(headerLine));
                    }
                    // 3) blank line between headers and body
                    outStream.Write(MultipartUtilities.CrLf);
                    // 4) output the revised body (or nothing if there is nothing in the body)
                    outStream.Write(newBody);
                    outStream.Write(MultipartUtilities.CrLf);
                }
            }
            catch (IOException ex)
            {
                var byteContent = Convert.ToBase64String(fixedRaw);
                string message = $$"""
The test-proxy is unexpectedly unable to read this section of the config during sanitization: \"{{ex.Message}}\"
File an issue on Azure/azure-sdk-tools and include this base64 string for reproducibility:
{{byteContent}}
""";

                throw new HttpException(HttpStatusCode.InternalServerError, message);
            }

            // 5) finally write closing boundary
            outStream.Write(boundaryClose);

            return outStream.ToArray();
        }

        public virtual void SanitizeBody(RequestOrResponse message)
        {
            if (message.Body != null)
            {
                message.TryGetContentType(out string contentType);

                if (ContentTypeUtilities.IsMultipart(message.Headers, out var boundary))
                {
                    message.Body = SanitizeMultipartBody(boundary, message.Body);
                }
                else if (message.TryGetBodyAsText(out string text))
                {
                    message.Body = Encoding.UTF8.GetBytes(SanitizeTextBody(contentType, text));
                }
                else
                {
                    message.Body = SanitizeBody(contentType, message.Body);
                }

                UpdateSanitizedContentLength(message);
            }
        }

        public virtual void Sanitize(RecordEntry entry, bool matchingBodies = true)
        {
            // PHASE 0: Build precache metadata for any multipart bodies
            // This happens ONCE per entry, then all sanitizers use the cached metadata
            if (matchingBodies)
            {
                PreCacheBodyMetadata(entry.Request);
                if (entry.RequestMethod != RequestMethod.Head)
                {
                    PreCacheBodyMetadata(entry.Response);
                }
            }

            if (Condition == null || Condition.IsApplicable(entry))
            {
                if (AppliesTo(SanitizerScope.Uri))
                {
                    entry.RequestUri = SanitizeUri(entry.RequestUri);
                }

                if (AppliesTo(SanitizerScope.Header))
                {
                    SanitizeHeaders(entry.Request.Headers);
                }

                if (matchingBodies && AppliesTo(SanitizerScope.Body))
                {
                    SanitizeBody(entry.Request);
                }

                if (AppliesTo(SanitizerScope.Header))
                {
                    SanitizeHeaders(entry.Response.Headers);
                }

                if (entry.RequestMethod != RequestMethod.Head && matchingBodies && AppliesTo(SanitizerScope.Body))
                {
                    SanitizeBody(entry.Response);
                }
            }
        }

        /// <summary>
        /// Build cached body metadata from a request or response body.
        /// Currently optimized for multipart bodies. Future extensibility: other body types.
        /// This is called once per body during precache phase, then the metadata is reused.
        /// If the message is not multipart or already has metadata, this is a no-op.
        /// </summary>
        public virtual void PreCacheBodyMetadata(RequestOrResponse message)
        {
            if (message?.Body == null || message.CachedBodyMetadata != null)
                return;  // Already has metadata, or no body

            if (!ContentTypeUtilities.IsMultipart(message.Headers, out var boundary))
                return;  // Not multipart, don't build metadata (yet)

            // Build metadata structure from multipart body
            boundary = MultipartUtilities.ResolveFirstBoundary(boundary, message.Body);
            byte[] fixedRaw = MultipartUtilities.NormalizeBareLf(message.Body);
            var reader = new MultipartReader(boundary, new MemoryStream(fixedRaw));

            var metadata = new PreCachedBodyMetadata { Boundary = boundary };

            try
            {
                MultipartSection section;
                while ((section = reader.ReadNextSectionAsync().GetAwaiter().GetResult()) != null)
                {
                    var bodySection = new PreCachedBodySection();

                    // Copy headers - convert StringValues to string
                    foreach (var header in section.Headers)
                    {
                        bodySection.Headers[header.Key] = header.Value.ToString();
                    }

                    // Read body
                    byte[] sectionBody = MultipartUtilities.ReadAllBytes(section.Body);
                    bodySection.Body = sectionBody;

                    // Check if section itself is multipart (nested)
                    if (bodySection.Headers.TryGetValue("Content-Type", out var contentType) &&
                        ContentTypeUtilities.IsMultiPart(contentType, out var nestedBoundary))
                    {
                        bodySection.NestedMetadata = BuildNestedMultipartMetadata(sectionBody, nestedBoundary);
                    }

                    metadata.Sections.Add(bodySection);
                }

                message.CachedBodyMetadata = metadata;
            }
            catch (IOException ex)
            {
                var byteContent = Convert.ToBase64String(fixedRaw);
                string message_text = $$"""
The test-proxy is unexpectedly unable to build precached body metadata during precache: \"{{ex.Message}}\"
File an issue on Azure/azure-sdk-tools and include this base64 string for reproducibility:
{{byteContent}}
""";
                throw new HttpException(HttpStatusCode.InternalServerError, message_text);
            }
        }

        /// <summary>
        /// Helper for recursive nested multipart metadata
        /// </summary>
        private PreCachedBodyMetadata BuildNestedMultipartMetadata(byte[] body, string boundary)
        {
            boundary = MultipartUtilities.ResolveFirstBoundary(boundary, body);
            byte[] fixedRaw = MultipartUtilities.NormalizeBareLf(body);
            var reader = new MultipartReader(boundary, new MemoryStream(fixedRaw));

            var metadata = new PreCachedBodyMetadata { Boundary = boundary };

            try
            {
                MultipartSection section;
                while ((section = reader.ReadNextSectionAsync().GetAwaiter().GetResult()) != null)
                {
                    var bodySection = new PreCachedBodySection();

                    foreach (var header in section.Headers)
                    {
                        bodySection.Headers[header.Key] = header.Value.ToString();
                    }

                    byte[] sectionBody = MultipartUtilities.ReadAllBytes(section.Body);
                    bodySection.Body = sectionBody;

                    if (bodySection.Headers.TryGetValue("Content-Type", out var nested) &&
                        ContentTypeUtilities.IsMultiPart(nested, out var nestedBoundary))
                    {
                        bodySection.NestedMetadata = BuildNestedMultipartMetadata(sectionBody, nestedBoundary);
                    }

                    metadata.Sections.Add(bodySection);
                }
            }
            catch (IOException ex)
            {
                var byteContent = Convert.ToBase64String(fixedRaw);
                string message_text = $$"""
The test-proxy is unexpectedly unable to build nested multipart metadata during precache: \"{{ex.Message}}\"
File an issue on Azure/azure-sdk-tools and include this base64 string for reproducibility:
{{byteContent}}
""";
                throw new HttpException(HttpStatusCode.InternalServerError, message_text);
            }

            return metadata;
        }

        public virtual void Sanitize(RecordSession session)
        {
            foreach (RecordEntry entry in session.Entries)
            {
                Sanitize(entry);
            }

            foreach (KeyValuePair<string, string> variable in session.Variables.ToArray())
            {
                session.Variables[variable.Key] = SanitizeVariable(variable.Key, variable.Value);
            }
        }

        /// <summary>
        /// Optionally update the Content-Length header if we've sanitized it
        /// and the new value is a different length from the original
        /// Content-Length header.  We don't add a Content-Length header if it
        /// wasn't already present.
        /// </summary>
        /// <param name="requestOrResponse">The Request or Response message</param>
        protected internal static void UpdateSanitizedContentLength(RequestOrResponse requestOrResponse)
        {
            var headers = requestOrResponse.Headers;
            int sanitizedLength = requestOrResponse.Body?.Length ?? 0;
            // Only update Content-Length if already present.
            if (headers.ContainsKey("Content-Length"))
            {
                headers["Content-Length"] = new string[] { sanitizedLength.ToString(CultureInfo.InvariantCulture) };
            }
        }
    }
}
