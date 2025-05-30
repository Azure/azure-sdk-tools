// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace SearchIndexCreator
{
    public class IssueTriageContent
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Body { get; set; }
        public string? Service { get; set; }
        public string? Category { get; set; }
        public string? Author { get; set; }
        public string? Repository { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }
        public string? Url { get; set; }

        // Codeowner meaning the service owner is the author of this "comment"
        // 0 -> false 1 -> true. Used numeric representation for magnitude scoring boost function. 
        // Issue itself is false since it is not a comment
        public int? CodeOwner { get; set; }
        public string? DocumentType { get; set; } // "Issue" or "Document"
    }
}