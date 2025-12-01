using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace APIViewWeb.LeanModels
{
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum CommentType
    {
        APIRevision = 0,
        SampleRevision
    }

    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum CommentSeverity
    {
        Question = 0,
        Suggestion = 1,
        ShouldFix = 2,
        MustFix = 3
    }

    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum CommentSource
    {
        UserGenerated,
        AIGenerated,
        Diagnostic
    }

    public class CommentFeedback
    {
        public List<string> Reasons { get; set; } = new List<string>();
        public string Comment { get; set; } = string.Empty;
        public bool IsDelete { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime SubmittedOn { get; set; }
    }

    public class CommentItemModel
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        [JsonProperty("id")]
        public string Id { get; set; } = IdHelper.GenerateId();
        public string ReviewId { get; set; }
        public string APIRevisionId { get; set; }
        public string SampleRevisionId { get; set; }
        public string ElementId { get; set; }
        public string SectionClass { get; set; }
        public string CommentText { get; set; }
        public string CrossLanguageId { get; set; }
        public string CorrelationId { get; set; }
        public List<CommentChangeHistoryModel> ChangeHistory { get; set; } = new List<CommentChangeHistoryModel>();        
        public bool IsResolved { get; set; }
        public List<string> Upvotes { get; set; } = new List<string>();
        public List<string> Downvotes { get; set; } = new List<string>();
        public HashSet<string> TaggedUsers { get; set; } = new HashSet<string>();
        public CommentType CommentType { get; set; }
        public CommentSeverity? Severity { get; set; }
        public CommentSource CommentSource { get; set; } = CommentSource.UserGenerated;
        public bool ResolutionLocked { get; set; } = false;
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? LastEditedOn { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsGeneric { get; set; }
        public List<string> GuidelineIds { get; set; } = [];
        public List<string> MemoryIds { get; set; } = [];
        public float ConfidenceScore { get; set; }

        public List<CommentFeedback> Feedback { get; set; } = [];
        public static CommentSeverity ParseSeverity(string value)
        {
            return value?.ToUpperInvariant() switch
            {
                "QUESTION" => CommentSeverity.Question,
                "SHOULD" => CommentSeverity.ShouldFix,
                "SUGGESTION" => CommentSeverity.Suggestion,
                "MUST" => CommentSeverity.MustFix,
                _ => CommentSeverity.ShouldFix
            };
        }
    }
}
