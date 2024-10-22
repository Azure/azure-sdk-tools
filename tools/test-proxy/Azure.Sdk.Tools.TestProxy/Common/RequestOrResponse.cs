// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class RequestOrResponse
    {
        /// <summary>
        /// Represents the modified state of the headers and body.
        /// </summary>
        public class RequestOrResponseIsModified
        {
            /// <summary>
            /// Gets or sets a value indicating whether the headers are modified.
            /// </summary>
            public bool Headers { get; set; } = false;

            /// <summary>
            /// Gets or sets a value indicating whether the body is modified.
            /// </summary>
            public bool Body { get; set; } = false;
        }

        private SortedDictionary<string, string[]> _headers = new SortedDictionary<string, string[]>(StringComparer.InvariantCultureIgnoreCase);
        public SortedDictionary<string, string[]> Headers
        {
            get { return this._headers; }
            set
            {
                // If the _headers are modified, set the flag to true
                if (DebugLogger.CheckLogLevel(LogLevel.Debug) && !this._headers.SequenceEqual(value)) this.IsModified.Headers = true;
                this._headers = value; 
            }
        }

        private byte[] _body;
        public byte[] Body
        {
            get { return this._body; }
            set
            {
                // If the _body is modified, set the flag to true
                if (DebugLogger.CheckLogLevel(LogLevel.Debug) &&
                    (this._body != null || value != null) &&
                    (this._body == null || value == null || !this._body.SequenceEqual(value)))
                {
                    this.IsModified.Body = true;
                }
                this._body = value; 
            }
        }

        public RequestOrResponseIsModified IsModified { get; set; } = new RequestOrResponseIsModified();

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
