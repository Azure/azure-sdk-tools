using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.WebhookRouter.Routing
{
    public class Payload
    {
        public Payload(IDictionary<string, StringValues> headers, JsonElement content)
        {
            Headers = headers;
            Content = content;
        }

        [JsonPropertyName("format")]
        public string Format => "0.1.0-alpha.1";

        [JsonPropertyName("headers")]
        public IDictionary<string, StringValues> Headers { get; private set; }

        [JsonPropertyName("content")]
        public JsonElement Content { get; private set; }
    }
}
