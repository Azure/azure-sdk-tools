using System;
using System.Collections.Generic;
using System.Text;

namespace PipelineGenerator
{
    public class IntegrationTestingPipelineConvention : PipelineConvention
    {
        public override string SearchPattern => "tests.yml";

        public IntegrationTestingPipelineConvention(PipelineGenerationContext context) : base(context)
        {
        }
    }
}
