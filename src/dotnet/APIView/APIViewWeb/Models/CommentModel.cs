using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace APIViewWeb.Models
{
    public class CommentModel
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string ReviewId { get; set; }
        public string ElementId { get; set; }
        public string Comment { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Username { get; set; }
    }
}
