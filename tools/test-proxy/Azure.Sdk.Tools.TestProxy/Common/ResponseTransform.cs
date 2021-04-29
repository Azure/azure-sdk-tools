using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public abstract class ResponseTransform
    {
        
        /// <summary>
        /// Base class used to describe transforming a played back response with necessary 
        /// changes directly from a request.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        public abstract void ApplyTransform(HttpRequest request, HttpResponse response);
    }
}
