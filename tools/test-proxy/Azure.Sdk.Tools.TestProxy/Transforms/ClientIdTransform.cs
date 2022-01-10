using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Transforms
{
    /// <summary>
    /// This transform applies during playback mode. It copies the header "x-ms-client-id" of the request
    /// onto the response before returning to the client.
    /// </summary>
    public class ClientIdTransform : ResponseTransform
    {
        public ClientIdTransform(ApplyCondition condition = null)
        {
            Condition = condition;
        }

        public override void ApplyTransform(RecordEntry entry)
        {
            if (entry.Request.Headers.TryGetValue("x-ms-client-id", out var clientId))
            {
                entry.Response.Headers.Add("x-ms-client-id", clientId);
            }
        }
    }
}
