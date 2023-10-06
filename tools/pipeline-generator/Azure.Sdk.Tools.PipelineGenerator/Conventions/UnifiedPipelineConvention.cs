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

        public override string SearchPattern => "ci.yml";
        public override string PipelineNameSuffix => "";
        public override string PipelineCategory => "unified";
        public override PipelineClassifications Classification => PipelineClassifications.Production;

        protected override async Task ApplyConventionAsync(BuildDefinition definition, SdkComponent component)
        {
            await base.ApplyConventionAsync(definition, component);
            EnsureDefaultPullRequestTrigger(definition, overrideYaml: true, securePipeline: true);
            EnsureDefaultCITrigger(definition);
            EnsureDefaultScheduledTrigger(definition);
        }
    }
}
