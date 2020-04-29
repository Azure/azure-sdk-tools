using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Azure.Sdk.Tools.WebhookRouter.Routing
{
    public class Payload
    {
        public Payload(JsonDocument json)
        {
            Json = json;
        }

        public JsonDocument Json { get; private set; }
    }
}
