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

        public override string GetDefinitionName(SdkComponent component)
        {
            var baseName = component.Variant == null
                            ? $"{Context.Prefix} - {component.Name}"
                            : $"{Context.Prefix} - {component.Name} - {component.Variant}";
            return baseName + " - weekly";
        }

        public override string SearchPattern => "ci.yml";
    }
}
