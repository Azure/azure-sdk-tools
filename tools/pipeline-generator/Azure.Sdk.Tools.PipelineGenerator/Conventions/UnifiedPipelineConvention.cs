using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PipelineGenerator.Conventions
{
    public class UnifiedPipelineConvention : PipelineConvention
    {
        public UnifiedPipelineConvention(ILogger logger, PipelineGenerationContext context) : base(logger, context)
        {
        }

        protected override string GetDefinitionName(SdkComponent component)
        {
            return component.Variant == null ? $"{Context.Prefix} - {component.Name}" : $"{Context.Prefix} - {component.Name} - {component.Variant}";
        }

        public override string SearchPattern => "ci.yml";

        protected override async Task<bool> ApplyConventionAsync(BuildDefinition definition, SdkComponent component)
        {
            var hasChanges = await base.ApplyConventionAsync(definition, component);

            if (EnsureDefautPullRequestTrigger(definition, overrideYaml: true, securePipeline: true))
            {
                hasChanges = true;
            }

            if (EnsureDefaultCITrigger(definition))
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
