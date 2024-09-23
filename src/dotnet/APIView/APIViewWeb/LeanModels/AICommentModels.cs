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
        public string Language { get; set; }
        public string BadCode { get; set; }
        public string GoodCode { get; set; } = null;
        public float[] Embedding { get; set; }
        public string Comment { get; set; } = null;
        public List<string> GuidelineIds { get; set; } = new List<string>();
        public List<AICommentChangeHistoryModel> ChangeHistory { get; set; } = new List<AICommentChangeHistoryModel>();
        public bool IsDeleted { get; set; } = false;
    }

    public class AICommentDTO
    {
        public virtual string Language { get; set; }
        public virtual string BadCode { get; set; }
        public string GoodCode { get; set; } = null;
        public string Comment { get; set; } = null;
        public List<string> GuidelineIds { get; set; } = new List<string>();
    }

    public class AICommentDTOForCreate : AICommentDTO
    {
        [Required]
        public override string Language { get; set; }
        [Required]
        public override string BadCode { get; set; }
    }

    public class AICommentDTOForSearch
    {
        [Required]
        public string Language { get; set; }
        [Required]
        public string BadCode { get; set; }
        public float Threshold { get; set; }
        public int Limit { get; set; } = 5;
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
                ChangeHistory = commentModel.ChangeHistory,
                IsDeleted = commentModel.IsDeleted
            };
        }
    }
}
