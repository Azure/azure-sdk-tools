using APIViewWeb.Helpers;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace APIViewWeb.LeanModels
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum CommentType
    {
        APIRevision = 0,
        SampleRevision
    }

    public class CommentItemModel
    {
        [JsonProperty("id")]
        public string Id { get; set; } = IdHelper.GenerateId();
        [JsonProperty("ri")]
        public string ReviewId { get; set; }
        [JsonProperty("ari")]
        public string APIRevisionId { get; set; }
        [JsonProperty("ei")]
        public string ElementId { get; set; }
        [JsonProperty("sc")]
        public string SectionClass { get; set; }
        [JsonProperty("ct")]
        public string CommentText { get; set; }
        [JsonProperty("cli")]
        public string CrossLanguageId { get; set; }
        [JsonIgnore]
        public List<CommentChangeHistoryModel> ChangeHistory { get; set; } = new List<CommentChangeHistoryModel>();
        [JsonProperty("ch")]
        public List<CommentChangeHistoryModel> ChangeHistorySerialized => ChangeHistory.Count > 0 ? ChangeHistory : null;
        
        public bool IsResolved { get; set; }
        [JsonIgnore]
        public List<string> Upvotes { get; set; } = new List<string>();
        [JsonProperty("uv")]
        public List<string> UpvotesSerialized => Upvotes.Count > 0 ? Upvotes : null;
        [JsonIgnore]
        public HashSet<string> TaggedUsers { get; set; } = new HashSet<string>();
        [JsonProperty("tu")]
        public HashSet<string> TaggedUsersSerialized => TaggedUsers.Count > 0 ? TaggedUsers : null;
        [JsonProperty("cty")]
        public CommentType CommentType { get; set; }
        [JsonProperty("rl")]
        public bool ResolutionLocked { get; set; } = false;
        [JsonProperty("cb")]
        public string CreatedBy { get; set; }
        [JsonProperty("co")]
        public DateTime CreatedOn { get; set; }
        [JsonProperty("leo")]
        public DateTime? LastEditedOn { get; set; }
        [JsonProperty("idl")]
        public bool IsDeleted { get; set; }
    }
}
