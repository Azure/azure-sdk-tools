using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace APIViewWeb.Models
{
    public class AgentChatResponse
    {
        [JsonPropertyName("response")]
        public string Response { get; set; }

        [JsonPropertyName("thread_id")]
        public string ThreadId { get; set; }

        [JsonPropertyName("messages")]
        public List<object> Messages { get; set; }
    }
}
