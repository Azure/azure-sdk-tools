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
                byte[] sectionBody = section.NestedTree != null
                    ? section.NestedTree.Materialize()
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
