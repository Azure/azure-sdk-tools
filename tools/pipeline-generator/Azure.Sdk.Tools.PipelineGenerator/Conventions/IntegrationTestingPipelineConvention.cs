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

        protected override async Task<bool> ApplyConventionAsync(BuildDefinition definition, SdkComponent component)
        {
            var hasChanges = await base.ApplyConventionAsync(definition, component);

            if (EnsureDefaultPullRequestTrigger(definition, overrideYaml: true, securePipeline: true))
            {
                hasChanges = true;
            }

            if (!Context.NoSchedule && EnsureDefaultScheduledTrigger(definition))
            {
                hasChanges = true;
            }

            return hasChanges;
        }
    }
}
