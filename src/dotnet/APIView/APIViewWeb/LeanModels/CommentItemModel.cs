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
        public List<CommentChangeHistoryModel> ChangeHistory { get; set; } = new List<CommentChangeHistoryModel>();        
        public bool IsResolved { get; set; }
        public List<string> Upvotes { get; set; } = new List<string>();
        public List<string> Downvotes { get; set; } = new List<string>();

        public HashSet<string> TaggedUsers { get; set; } = new HashSet<string>();
        public CommentType CommentType { get; set; }
        public bool ResolutionLocked { get; set; } = false;
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? LastEditedOn { get; set; }
        public bool IsDeleted { get; set; }
    }
}
