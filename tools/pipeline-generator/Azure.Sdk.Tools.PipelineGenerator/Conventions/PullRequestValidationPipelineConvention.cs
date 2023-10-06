using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PipelineGenerator.Conventions
{
    public class PullRequestValidationPipelineConvention : PipelineConvention
    {
        public PullRequestValidationPipelineConvention(ILogger logger, PipelineGenerationContext context) : base(logger, context)
        {
        }

        public override string SearchPattern => "ci.yml";
        public override string PipelineNameSuffix => " - ci";
        public override string PipelineCategory => "ci";
        public override PipelineClassifications Classification => PipelineClassifications.NonProduction;

        protected override async Task ApplyConventionAsync(BuildDefinition definition, SdkComponent component)
        {
            await base.ApplyConventionAsync(definition, component);
            EnsureDefaultPullRequestTrigger(definition, overrideYaml: false, securePipeline: false);
            EnsureDefaultCITrigger(definition);
        }
    }
}
