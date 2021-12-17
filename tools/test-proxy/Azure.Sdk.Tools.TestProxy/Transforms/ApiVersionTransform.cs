using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Transforms
{
    public class ApiVersionTransform : ResponseTransform
    {
        public ApiVersionTransform(ApplyCondition condition = null)
        {
            Condition = condition;
        }

        /// <summary>
        /// This transform applies during playback mode. It copies the header "api-version" of the request
        /// onto the response before sending the response back to the client.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        public override void ApplyTransform(HttpRequest request, HttpResponse response)
        {
            if (request.Headers.TryGetValue("api-version", out var clientId))
            {
                response.Headers.Add("api-version", clientId);
            }
        }
    }
}
