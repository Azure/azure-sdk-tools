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

        public void Transform(RecordEntry entry)
        {
            if (Condition == null || Condition.IsApplicable(entry))
            {
                ApplyTransform(entry);
            }
        }

        /// <summary>
        /// Base class used to describe transforming a played back response with necessary 
        /// changes directly from a request.
        /// </summary>
        /// <param name="entry">The entry to transform.</param>
        public abstract void ApplyTransform(RecordEntry entry);
    }
}
