// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models
{
    public class ApiviewData
    {
        [JsonPropertyName("Language")]
        public string Language { get; set; } = string.Empty;
        [JsonPropertyName("Package Name")]
        public string PackageName { get; set; } = string.Empty;
        [JsonPropertyName("SDK API View Link")]
        public string ApiviewLink {  get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents complete metadata for an APIView review
    /// </summary>
    public class ReviewMetadata
    {
        [JsonPropertyName("reviewId")]
        public string ReviewId { get; set; } = string.Empty;
        
        [JsonPropertyName("packageName")]
        public string PackageName { get; set; } = string.Empty;
        
        [JsonPropertyName("language")]
        public string Language { get; set; } = string.Empty;
        
        [JsonPropertyName("revision")]
        public RevisionMetadata? Revision { get; set; }
    }

    /// <summary>
    /// Represents revision-specific metadata from APIView
    /// </summary>
    public class RevisionMetadata
    {
        [JsonPropertyName("revisionId")]
        public string RevisionId { get; set; } = string.Empty;
        
        [JsonPropertyName("pullRequestNo")]
        public int? PullRequestNo { get; set; }
        
        [JsonPropertyName("pullRequestRepository")]
        public string? PullRequestRepository { get; set; }
        
        [JsonPropertyName("revisionLabel")]
        public string? RevisionLabel { get; set; }
    }

    /// <summary>
    /// Represents a consolidated comment from a discussion thread
    /// </summary>
    // TODO: Add ThreadUrl property for direct links to APIView discussion threads
    public class ConsolidatedComment
    {
        public string ThreadId { get; set; } = string.Empty;
        public int LineNo { get; set; }
        
        /// <summary>
        /// Line identifier from APIView. Note: _lineId is Python-specific and serves as 
        /// placeholder for fullObjectPath until fullObjectPath is added to APIView API.
        /// </summary>
        public string? LineId { get; set; } = string.Empty;
        
        public string LineText { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a raw comment from APIView API
    /// </summary>
    internal class APIViewComment
    {
        [JsonPropertyName("lineNo")]
        public int LineNo { get; set; }
        
        [JsonPropertyName("_lineId")]
        public string? LineId { get; set; }
        
        [JsonPropertyName("_lineText")]
        public string? LineText { get; set; }
        
        [JsonPropertyName("createdOn")]
        public string? CreatedOn { get; set; }
        
        [JsonPropertyName("upvotes")]
        public int Upvotes { get; set; }
        
        [JsonPropertyName("downvotes")]
        public int Downvotes { get; set; }
        
        [JsonPropertyName("createdBy")]
        public string? CreatedBy { get; set; }
        
        [JsonPropertyName("commentText")]
        public string? CommentText { get; set; }
        
        [JsonPropertyName("isResolved")]
        public bool IsResolved { get; set; }
        
        [JsonPropertyName("severity")]
        public string? Severity { get; set; }
        
        [JsonPropertyName("threadId")]
        public string? ThreadId { get; set; }
    }
}
