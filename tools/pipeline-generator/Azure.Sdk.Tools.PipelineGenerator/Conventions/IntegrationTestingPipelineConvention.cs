using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PipelineGenerator.Conventions
{
    public class IntegrationTestingPipelineConvention : PipelineConvention
    {
        public IntegrationTestingPipelineConvention(ILogger logger, PipelineGenerationContext context) : base(logger, context)
        {
        }

        public override string SearchPattern => "tests.yml";
        public override string PipelineNameSuffix => " - tests";
        public override string PipelineCategory => "tests";
        public override PipelineClassifications Classification => PipelineClassifications.NonProduction;

        protected override async Task ApplyConventionAsync(BuildDefinition definition, SdkComponent component)
        {
            await base.ApplyConventionAsync(definition, component);
            EnsureDefaultPullRequestTrigger(definition, overrideYaml: true, securePipeline: true);
            EnsureDefaultScheduledTrigger(definition);
        }
    }
}
