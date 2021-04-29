using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Transforms
{
    public class ClientIdTransform : ResponseTransform
    {
        public override void ApplyTransform(HttpRequest request, HttpResponse response)
        {
            if (request.Headers.TryGetValue("x-ms-client-id", out var clientId))
            {
                response.Headers.Add("x-ms-client-id", clientId);
            }
        }
    }
}
