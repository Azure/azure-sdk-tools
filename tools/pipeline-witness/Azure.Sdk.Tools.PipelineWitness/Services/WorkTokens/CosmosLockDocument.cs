using System;
using Newtonsoft.Json;

namespace Azure.Sdk.Tools.PipelineWitness.Services.WorkTokens
{
    public class CosmosLockDocument
    {
        public CosmosLockDocument()
        {
        }

        public CosmosLockDocument(string id, TimeSpan duration)
        {
            Id = id;
            Expiration = DateTime.UtcNow.Add(duration);
        }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("expiration")]
        public DateTime Expiration { get; set; }
    }
}
