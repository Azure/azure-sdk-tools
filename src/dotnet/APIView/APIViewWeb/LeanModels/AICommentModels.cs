using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace APIViewWeb.LeanModels
{
    public class AICommentModel
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
        public IEnumerable<string> GuidelineIds { get; set; } = new List<string>();
        [JsonProperty("modified_on")]
        public DateTime ModifiedOn { get; set; }
        [JsonProperty("modified_by")]
        public string ModifiedBy { get; set; }
        [JsonProperty("is_deleted")]
        public bool IsDeleted { get; set; } = false;
    }

    public class AICommentDTO
    {
        public string Language { get; set; }
        public string BadCode { get; set; }
        public string GoodCode { get; set; } = null;
        public string Comment { get; set; } = null;
        public IEnumerable<string> GuidelineIds { get; set; } = new List<string>();
    }

    public class AICommentDTOForCreate : AICommentDTO
    {
        [Required]
        public new string Language { get; set; }
        [Required]
        public new string BadCode { get; set; }
    }

    public class AICommentDTOForSearch : AICommentDTOForCreate
    {
        public float Threshold { get; set; }
        public int Limit { get; set; } = 5;
        public float[] Embedding { get; set; }
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

    public class AICommentModelForSearch
    {
        public float Similarity { get; set; }
        public AICommentModel AiCommentModel { get; set; }

        public AICommentModelForSearch(float similarity, AICommentModel commentModel)
        {
            this.Similarity = similarity;
            this.AiCommentModel = CreateCopyWithoutEmbedding(commentModel);
        }

        private static AICommentModel CreateCopyWithoutEmbedding(AICommentModel commentModel)
        {
            return new AICommentModel()
            {
                Id = commentModel.Id,
                BadCode = commentModel.BadCode,
                GoodCode = commentModel.GoodCode,
                Embedding = null,
                Language = commentModel.Language,
                Comment = commentModel.Comment,
                GuidelineIds = commentModel.GuidelineIds,
                ModifiedOn = commentModel.ModifiedOn,
                ModifiedBy = commentModel.ModifiedBy,
                IsDeleted = commentModel.IsDeleted
            };
        }
    }
}
