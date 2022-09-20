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

        /// <summary>
        /// Transform that updates a matched playback response with the x-ms-client-id header value pulled from the request.
        /// </summary>
        /// <param name="request">The request from which transformations will be pulled.</param>
        /// <param name="match">The matched playback entry that can be transformed with an incoming client id.</param>
        public override void ApplyTransform(HttpRequest request, RecordEntry match)
        {
            if (request.Headers.TryGetValue("x-ms-client-id", out var clientId))
            {
                match.Response.Headers["x-ms-client-id"] = clientId;
            }
        }
    }
}
