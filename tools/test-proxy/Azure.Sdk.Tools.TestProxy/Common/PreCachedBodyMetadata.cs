// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    /// <summary>
    /// Cached metadata about a request or response body, built during precache phase.
    /// Currently optimized for multipart bodies, but extensible for other body types.
    /// Built once and reused by sanitizers to avoid reparsing.
    /// </summary>
    public class PreCachedBodyMetadata
    {
        /// <summary>
        /// For multipart bodies: the boundary string for this section
        /// </summary>
        public string Boundary { get; set; }

        /// <summary>
        /// For multipart bodies: list of sections in this multipart body.
        /// Each section is a complete part with headers and body content.
        /// </summary>
        public List<PreCachedBodySection> Sections { get; set; } = new List<PreCachedBodySection>();

        /// <summary>
        /// Materialize the cached metadata back to body bytes.
        /// For multipart: reconstructs the complete multipart body from sections.
        /// Called only when body needs to be serialized or returned to client.
        /// </summary>
        public byte[] Materialize()
        {
            if (Sections.Count == 0 || string.IsNullOrEmpty(Boundary))
                return null;

            byte[] boundaryStart = Encoding.ASCII.GetBytes($"--{Boundary}\r\n");
            byte[] boundaryClose = Encoding.ASCII.GetBytes($"--{Boundary}--\r\n");

            using var outStream = new MemoryStream();

            foreach (var section in Sections)
            {
                outStream.Write(boundaryStart);

                // Write headers
                foreach (var header in section.Headers)
                {
                    var headerLine = $"{header.Key}: {header.Value}\r\n";
                    outStream.Write(Encoding.ASCII.GetBytes(headerLine));
                }

                outStream.Write(MultipartUtilities.CrLf);

                // Write body (handles nested multipart recursively)
                byte[] sectionBody = section.NestedMetadata != null
                    ? section.NestedMetadata.Materialize()
                    : section.Body;

                if (sectionBody != null && sectionBody.Length > 0)
                {
                    outStream.Write(sectionBody);
                }

                outStream.Write(MultipartUtilities.CrLf);
            }

            outStream.Write(boundaryClose);

            return outStream.ToArray();
        }
    }

    /// <summary>
    /// Represents a single section within a cached body structure (currently multipart).
    /// </summary>
    public class PreCachedBodySection
    {
        /// <summary>
        /// Headers for this section (Content-Type, Content-Disposition, etc.)
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Raw body content of this section (may contain text, binary, or multipart data)
        /// </summary>
        public byte[] Body { get; set; }

        /// <summary>
        /// If this section contains nested multipart, this metadata represents that structure
        /// </summary>
        public PreCachedBodyMetadata NestedMetadata { get; set; }

        /// <summary>
        /// Whether this section contains nested multipart data
        /// </summary>
        public bool IsNestedMultipart => NestedMetadata != null;
    }
}
