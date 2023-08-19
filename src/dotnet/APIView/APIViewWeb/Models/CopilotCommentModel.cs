using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace APIViewWeb.Models
{
    public class CopilotCommentModel
    {
        [JsonProperty("id")]
        public string Id { get; set; } = IdHelper.GenerateId();
        [JsonProperty("language")]
        public string Language { get; set; }
        [JsonProperty("bad_code")]
        public string BadCode { get; set; }
        [JsonProperty("good_code")]
        public string GoodCode { get; set; } = null;
        [JsonProperty("embedding")]
        public float[] Embedding { get; set; }
        [JsonProperty("comment")]
        public string Comment { get; set; } = null;
        [JsonProperty("guideline_ids")]
        public IEnumerable<string> GuidelineIds { get; set; }
        [JsonProperty("modified_on")]
        public DateTime ModifiedOn { get; set; }
        [JsonProperty("modified_by")]
        public string ModifiedBy { get; set; }
        [JsonProperty("is_deleted")]
        public bool IsDeleted { get; set; } = false;
    }

    class SearchEmbedding
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("embedding")]
        public IReadOnlyList<float> Embedding { get; set; }
        [JsonPropertyName("language")]
        public string Language { get; set; }
    }
}
