using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace APIViewWeb.Models
{
    public class CommentModel
    {
        [JsonProperty("id")]
        public string CommentId { get; set; } = IdHelper.GenerateId();
        public string ReviewId { get; set; }
        public string RevisionId { get; set; }
        public string ElementId { get; set; }
        public string Comment { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Username { get; set; }
        public bool IsResolve { get; set; }
        public DateTime? EditedTimeStamp { get; set; }
        public List<string> Upvotes { get; set; } = new List<string>();
    }
}
