using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public abstract class ResponseTransform
    {
        public ApplyCondition Condition = null;

        public void Transform(HttpRequest request, HttpResponse response)
        {
            if (Condition == null || Condition.IsApplicable(request))
            {
                ApplyTransform(request, response);
            }
        }

        /// <summary>
        /// Base class used to describe transforming a played back response with necessary 
        /// changes directly from a request.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        public abstract void ApplyTransform(HttpRequest request, HttpResponse response);
    }
}
