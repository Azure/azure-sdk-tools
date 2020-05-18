using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.PipelineWitness
{
    public class AzurePipelinesRun
    {
        public AzurePipelinesRun()
        {
        }

        public AzurePipelinesRun(JsonDocument document)
        {
            this.Run = document.RootElement;
            this.Id = document.RootElement.GetProperty("id").ToString();
        }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("run")]
        public JsonElement Run { get; set; }
    }
}
