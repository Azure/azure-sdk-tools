using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Sdk.PipelineGenerator.Tool
{
    public class IntegrationTestingPipelineConvention : PipelineConvention
    {
        public override string SearchPattern => "tests.yml";

        public IntegrationTestingPipelineConvention(ILogger logger, PipelineGenerationContext context) : base(logger, context)
        {
        }

        protected override string GetDefinitionName(SdkComponent component)
        {
            return $"{Context.Prefix} - {component.Name} - tests";
        }

        protected override Task<bool> ApplyConventionAsync(BuildDefinition definition, SdkComponent component)
        {
            return Task.FromResult(false);
        }
    }
}
