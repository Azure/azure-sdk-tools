// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    /// <summary>
    /// Represents the structure of a multipart message as a tree.
    /// Built once during precache, then reused by sanitizers to avoid reparsing.
    /// </summary>
    public class MultipartTree
    {
        /// <summary>
        /// The boundary string for this multipart section
        /// </summary>
        public string Boundary { get; set; }

        /// <summary>
        /// List of sections in this multipart body.
        /// Each section is a complete part with headers and body content.
        /// </summary>
        public List<MultipartTreeSection> Sections { get; set; } = new List<MultipartTreeSection>();

        /// <summary>
        /// Materialize the tree back to complete multipart body bytes.
        /// Called only when body needs to be serialized or returned to client.
        /// </summary>
        public byte[] Materialize()
        {
            using var outStream = new MemoryStream();
            Materialize(outStream);
            return outStream.ToArray();
        }

        /// <summary>
        /// Stream-based materialization to avoid allocating intermediate buffers when nesting.
        /// </summary>
        /// <param name="destination">Stream that receives the serialized multipart payload.</param>
        internal void Materialize(Stream destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
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
                if (section.NestedTree != null)
                {
                    section.NestedTree.Materialize(destination);
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
    /// Represents a single section within a multipart message
    /// </summary>
    public class MultipartTreeSection
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
        /// If this section contains nested multipart, this tree represents that structure
        /// </summary>
        public MultipartTree NestedTree { get; set; }

        /// <summary>
        /// Whether this section contains nested multipart data
        /// </summary>
        public bool IsNestedMultipart => NestedTree != null;
    }
}
