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
        public override string SearchPattern => "tests.yml";

        public IntegrationTestingPipelineConvention(ILogger logger, PipelineGenerationContext context) : base(logger, context)
        {
        }

        protected override string GetDefinitionName(SdkComponent component)
        {
            return component.Variant == null ? $"{Context.Prefix} - {component.Name} - tests" : $"{Context.Prefix} - {component.Name} - tests.{component.Variant}";
        }

        protected override async Task<bool> ApplyConventionAsync(BuildDefinition definition, SdkComponent component)
        {
            var hasChanges = await base.ApplyConventionAsync(definition, component);

            if (EnsureDefautPullRequestTrigger(definition, overrideYaml: true, securePipeline: true))
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
