using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class ResponseTransform
    {
        
        /// <summary>
        /// Base class used to describe transforming a played back response with necessary 
        /// changes directly from a request.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        public virtual void ApplyTransform(HttpRequest request, HttpResponse response)
        {
            // empty by default
        }
    }
}
