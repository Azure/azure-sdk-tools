// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class RequestOrResponse
    {
        private byte[] _cachedCompressedBody;
        private byte[] _cachedBodyHash;
        private string _cachedContentEncoding;

        public SortedDictionary<string, string[]> Headers { get; set; } = new SortedDictionary<string, string[]>(StringComparer.InvariantCultureIgnoreCase);

        public byte[] Body { get; set; }

        /// <summary>
        /// Cached metadata about this body. Built once during precache phase,
        /// then reused by sanitizers to avoid reparsing multipart or other complex bodies.
        /// </summary>
        public PreCachedBodyMetadata CachedBodyMetadata { get; set; }

        internal byte[] GetBodyForPlayback(bool cacheCompression)
        {
            if (!cacheCompression)
            {
                return CompressionUtilities.CompressBody(Body, Headers);
            }

            var encoding = CompressionUtilities.GetCompressionEncoding(Headers);
            if (encoding == null || Body == null)
            {
                _cachedCompressedBody = null;
                _cachedBodyHash = null;
                _cachedContentEncoding = null;
                return Body;
            }

            Span<byte> bodyHash = stackalloc byte[SHA256.HashSizeInBytes];
            SHA256.HashData(Body, bodyHash);
            if (_cachedCompressedBody != null && _cachedContentEncoding == encoding && bodyHash.SequenceEqual(_cachedBodyHash))
            {
                return _cachedCompressedBody;
            }

            _cachedCompressedBody = CompressionUtilities.CompressBodyCore(Body, encoding);
            _cachedBodyHash ??= new byte[SHA256.HashSizeInBytes];
            bodyHash.CopyTo(_cachedBodyHash);
            _cachedContentEncoding = encoding;
            return _cachedCompressedBody;
        }

        public bool TryGetContentType(out string contentType)
        {
            contentType = null;
            if (Headers.TryGetValue("Content-Type", out var contentTypes) &&
                contentTypes.Length == 1)
            {
                contentType = contentTypes[0];
                return true;
            }
            return false;
        }

        public bool IsTextContentType(out Encoding encoding)
        {
            encoding = null;
            return TryGetContentType(out string contentType) &&
                   ContentTypeUtilities.TryGetTextEncoding(contentType, out encoding);
        }

        public bool TryGetBodyAsText(out string text)
        {
            text = null;

            if (IsTextContentType(out Encoding encoding))
            {
                text = encoding.GetString(Body);

                return true;
            }

            return false;
        }
    }
}
