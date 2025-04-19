// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Primitives;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    internal static class ContentTypeUtilities
    {

        public static bool IsManifestContentType(string contentType)
        {
            const string dockerManifest = "application/vnd.docker.distribution.manifest.v";
            const string dockerIndex = "application/vnd.oci.image.index.v";

            return contentType.Contains(dockerManifest) || contentType.Contains(dockerIndex);
        }

        public static bool IsMultipartMixed(IDictionary<string, string[]> headers,
                                             out string boundary)
        {
            boundary = null;

            if (!headers.TryGetValue("Content-Type", out var values))
                return false;

            var ct = values[0];
            if (!ct.StartsWith("multipart/mixed", StringComparison.OrdinalIgnoreCase))
                return false;

            const string key = "boundary=";
            var idx = ct.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx == -1) return false;

            boundary = ct[(idx + key.Length)..]   // everything after “boundary=”
                         .Trim()                  // strip spaces
                         .Trim('"');              // strip optional quotes
            return boundary.Length > 0;
        }

        public static bool IsTextContentType(IDictionary<string, string[]> headers, out Encoding encoding)
        {
            encoding = null;
            return TryGetContentType(headers, out string contentType) &&
                   ContentTypeUtilities.TryGetTextEncoding(contentType, out encoding);
        }

        internal static bool IsTextContentType(Dictionary<string, StringValues> headers, out Encoding encoding)
        {
            encoding = null;
            return TryGetContentType(headers, out string contentType) &&
                   ContentTypeUtilities.TryGetTextEncoding(contentType, out encoding);
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

        public static bool TryGetContentType(IDictionary<string, StringValues> requestHeaders, out string contentType)
        {
            contentType = null;

            // Try lookup –­ StringValues can be empty, null, or hold >1 items.
            if (requestHeaders != null &&
                requestHeaders.TryGetValue("Content-Type", out var values) &&
                values.Count == 1)
            {
                contentType = values[0];
                return true;
            }

            return false;
        }

        public static bool TryGetTextEncoding(string contentType, out Encoding encoding)
        {
            const string charsetMarker = "; charset=";
            const string utf8Charset = "utf-8";
            const string textContentTypePrefix = "text/";
            const string jsonSuffix = "json";
            const string appJsonPrefix = "application/json";
            const string xmlSuffix = "xml";
            const string urlEncodedSuffix = "-urlencoded";

            // Default is technically US-ASCII, but will default to UTF-8 which is a superset.
            const string appFormUrlEncoded = "application/x-www-form-urlencoded";

            if (contentType == null)
            {
                encoding = null;
                return false;
            }

            var charsetIndex = contentType.IndexOf(charsetMarker, StringComparison.OrdinalIgnoreCase);
            if (charsetIndex != -1)
            {
                ReadOnlySpan<char> charset = contentType.AsSpan().Slice(charsetIndex + charsetMarker.Length);
                if (charset.StartsWith(utf8Charset.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    encoding = Encoding.UTF8;
                    return true;
                }
            }

            if (
                    (
                        contentType.StartsWith(textContentTypePrefix, StringComparison.OrdinalIgnoreCase) ||
                        contentType.EndsWith(jsonSuffix, StringComparison.OrdinalIgnoreCase) ||
                        contentType.EndsWith(xmlSuffix, StringComparison.OrdinalIgnoreCase) ||
                        contentType.EndsWith(urlEncodedSuffix, StringComparison.OrdinalIgnoreCase) ||
                        contentType.StartsWith(appJsonPrefix, StringComparison.OrdinalIgnoreCase) ||
                        contentType.StartsWith(appFormUrlEncoded, StringComparison.OrdinalIgnoreCase)
                    ) && !ContentTypeUtilities.IsManifestContentType(contentType)
                )
            {
                encoding = Encoding.UTF8;
                return true;
            }


            encoding = null;
            return false;
        }
    }
}
