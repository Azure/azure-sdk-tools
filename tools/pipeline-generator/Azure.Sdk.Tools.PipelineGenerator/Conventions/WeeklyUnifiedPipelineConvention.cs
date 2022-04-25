using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PipelineGenerator.Conventions
{
    public class WeeklyUnifiedPipelineConvention : WeeklyIntegrationTestingPipelineConvention
    {
        public WeeklyUnifiedPipelineConvention(ILogger logger, PipelineGenerationContext context) : base(logger, context)
        {
        }

        public override string SearchPattern => "ci.yml";
        public override string PipelineNameSuffix => " - weekly";
        public override string PipelineCategory => "unified-weekly";
    }
}
