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
        private byte[] _body;
        private string _sanitizingText;
        private bool _reuseBodyText;

        public SortedDictionary<string, string[]> Headers { get; set; } = new SortedDictionary<string, string[]>(StringComparer.InvariantCultureIgnoreCase);

        public byte[] Body
        {
            get
            {
                _sanitizingText = null;
                return _body;
            }
            set
            {
                _body = value;
                _sanitizingText = null;
            }
        }

        internal bool HasBody => _body != null;
        internal int BodyLength => _body?.Length ?? 0;

        internal void BeginTextSanitization()
        {
            _reuseBodyText = true;
        }

        internal void EndTextSanitization()
        {
            _reuseBodyText = false;
            _sanitizingText = null;
        }

        internal void SetBodyText(string text)
        {
            _body = Encoding.UTF8.GetBytes(text);
            _sanitizingText = _reuseBodyText ? text : null;
        }

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
                text = _sanitizingText ?? encoding.GetString(_body);
                if (_reuseBodyText)
                {
                    _sanitizingText = text;
                }

                return true;
            }

            return false;
        }
    }
}
