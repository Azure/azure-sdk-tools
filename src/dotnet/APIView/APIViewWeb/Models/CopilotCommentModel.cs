using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace APIViewWeb.Models
{
    public class CopilotCommentModel
    {
        [BsonId]
        public ObjectId Id { get; set; }
        public string BadCode { get; set; }
        public string GoodCode { get; set; } = null;
        public float[] Embedding { get; set; }
        public string Language { get; set; }
        public string Comment { get; set; } = null;
        public string[] GuidelineIds { get; set; } = null;
        public DateTime ModifiedOn { get; set; }
        public string ModifiedBy { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
