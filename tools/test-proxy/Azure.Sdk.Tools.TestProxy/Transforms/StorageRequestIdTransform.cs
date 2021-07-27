﻿using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.AspNetCore.Http;
namespace Azure.Sdk.Tools.TestProxy.Transforms
{
    /// <summary>
    /// Storage requests send a header "x-ms-client-request-id" with every request.
    /// The response from the service contains a header that MUST MATCH this value.
    /// 
    /// This transform implements the above behavior during playback.
    /// </summary>
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
