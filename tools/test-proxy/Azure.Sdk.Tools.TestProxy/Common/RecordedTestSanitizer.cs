// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

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

        private static readonly byte[] CrLf = new byte[] { (byte)'\r', (byte)'\n' };


        private byte[] SanitizeMultipartBody(string boundary, byte[] raw)
        {
            // Boundary might have been sanitised to "REDACTED"
            boundary = RecordEntry.ResolveFirstBoundary(boundary, raw);

            // Only run the LF→CRLF fixer once at the outermost level
            // the reason we still do this instead of just using the body as-is, is that we may be loading up
            // a recording from before we started storing the corrected multipart/mixed body.
            byte[] fixedRaw = RecordEntry.NormalizeBareLf(raw);

            var reader = new MultipartReader(boundary, new MemoryStream(raw));
            using var outStream = new MemoryStream();

            byte[] boundaryStart = Encoding.ASCII.GetBytes($"--{boundary}\r\n");
            byte[] boundaryClose = Encoding.ASCII.GetBytes($"--{boundary}--\r\n");

            MultipartSection section;
            while ((section = reader.ReadNextSectionAsync()
                                     .GetAwaiter()
                                     .GetResult()) != null)
            {
                // 1) opening boundary
                outStream.Write(boundaryStart);

                // 2) headers (by spec must be ASCII encoded)
                foreach (var h in section.Headers)
                {
                    var headerLine = $"{h.Key}: {h.Value}\r\n";
                    outStream.Write(Encoding.ASCII.GetBytes(headerLine));
                }

                // 3) blank line between headers and body
                outStream.Write(CrLf);

                // 4) body (sanitised)
                using var tmp = new MemoryStream();
                section.Body.CopyTo(tmp);
                var original = tmp.ToArray();
                byte[] newBody;

                if (ContentTypeUtilities.IsTextContentType(section.Headers, out var enc))
                {
                    var sanitised = SanitizeTextBody(section.ContentType, enc.GetString(original));
                    newBody = enc.GetBytes(sanitised);

                    // todo: ensure content-length is updated!
                }
                else
                {
                    newBody = original;
                }

                outStream.Write(newBody);
                outStream.Write(CrLf); // todo: do we still need the body terminator here? body terminator
            }

            // 5) closing boundary
            outStream.Write(boundaryClose);

            return outStream.ToArray();
        }

        public virtual void SanitizeBody(RequestOrResponse message)
        {
            if (message.Body != null)
            {
                message.TryGetContentType(out string contentType);

                if (ContentTypeUtilities.IsMultipartMixed(message.Headers, out var boundary))
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

        public virtual void Sanitize(RecordEntry entry)
        {
            if (Condition == null || Condition.IsApplicable(entry))
            {
                entry.RequestUri = SanitizeUri(entry.RequestUri);

                SanitizeHeaders(entry.Request.Headers);

                SanitizeBody(entry.Request);

                SanitizeHeaders(entry.Response.Headers);

                if (entry.RequestMethod != RequestMethod.Head)
                {
                    SanitizeBody(entry.Response);
                }
            }
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
