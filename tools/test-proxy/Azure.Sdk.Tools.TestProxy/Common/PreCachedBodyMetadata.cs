// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Primitives;

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

            using var outStream = new MemoryStream();
            Materialize(outStream);
            return outStream.ToArray();
        }

        /// <summary>
        /// Stream-based materialization to avoid intermediate byte[] allocations for nested multiparts.
        /// The caller owns <paramref name="destination"/> and is responsible for flushing/disposing it.
        /// </summary>
        /// <param name="destination">Stream that receives the serialized body.</param>
        internal void Materialize(Stream destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (Sections.Count == 0 || string.IsNullOrEmpty(Boundary))
            {
                return;
            }

            byte[] boundaryStart = Encoding.ASCII.GetBytes($"--{Boundary}\r\n");
            byte[] boundaryClose = Encoding.ASCII.GetBytes($"--{Boundary}--\r\n");

            foreach (var section in Sections)
            {
                destination.Write(boundaryStart);

                // Write headers
                foreach (var header in section.Headers)
                {
                    var headerLine = $"{header.Key}: {header.Value}\r\n";
                    destination.Write(Encoding.ASCII.GetBytes(headerLine));
                }

                destination.Write(MultipartUtilities.CrLf);

                // Write body (handles nested multipart recursively)
                if (section.NestedMetadata != null)
                {
                    section.NestedMetadata.Materialize(destination);
                }
                else if (section.Body != null && section.Body.Length > 0)
                {
                    destination.Write(section.Body);
                }

                destination.Write(MultipartUtilities.CrLf);
            }

            destination.Write(boundaryClose);
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
        public Dictionary<string, StringValues> Headers { get; set; } =
            new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);

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
