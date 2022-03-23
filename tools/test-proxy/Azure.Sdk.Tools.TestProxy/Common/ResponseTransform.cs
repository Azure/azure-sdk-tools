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

        public void Transform(HttpRequest request, RecordEntry match)
        {
            if (Condition == null || Condition.IsApplicable(match))
            {
                ApplyTransform(request, match);
            }
        }

        /// <summary>
        /// Base class used to describe transforming a played back response with necessary 
        /// changes directly from a request.
        /// </summary>
        /// <param name="request">The request from which transformations will be pulled.</param>
        /// <param name="match">The matched playback entry that can be transformed.</param>
        public abstract void ApplyTransform(HttpRequest request, RecordEntry match);
    }
}
