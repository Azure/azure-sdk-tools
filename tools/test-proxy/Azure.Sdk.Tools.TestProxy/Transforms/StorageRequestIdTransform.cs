using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Transforms
{
    public class StorageRequestIdTransform : ResponseTransform
    {
        public override void ApplyTransform(HttpRequest request, HttpResponse response)
        {
            // Storage Blobs requires "x-ms-client-request-id" header in request and response to match
            if (request.Headers.TryGetValue("x-ms-client-request-id", out var clientRequestId))
            {
                response.Headers["x-ms-client-request-id"] = clientRequestId;
            }
        }
    }
}
