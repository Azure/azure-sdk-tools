using System;
using System.Collections.Generic;
using System.Text;

namespace PipelineGenerator
{
    public class PullRequestValidationPipelineConvention : PipelineConvention
    {
        public override string SearchPattern => "ci.yml";

        public PullRequestValidationPipelineConvention(PipelineGenerationContext context) : base(context)
        {
        }
    }
}
