using System;
using Newtonsoft.Json;

namespace APIViewWeb.Models
{
    public class CommentModel
    {
        [JsonProperty("id")]
        public string CommentId { get; set; } = Guid.NewGuid().ToString("N");
        public string ReviewId { get; set; }
        public string ElementId { get; set; }
        public string Comment { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Username { get; set; }
        public bool IsResolve { get; set; }
    }
}
