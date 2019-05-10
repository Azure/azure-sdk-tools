using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PipelineGenerator
{
    public class PullRequestValidationPipelineConvention : PipelineConvention
    {
        public override string SearchPattern => "ci.yml";

        public PullRequestValidationPipelineConvention(ILogger logger, PipelineGenerationContext context) : base(logger, context)
        {
        }

        protected override string GetDefinitionName(SdkComponent component)
        {
            return $"{Context.Prefix} - {component.Name} - ci";
        }

        protected async override Task<bool> ApplyConventionAsync(BuildDefinition definition, SdkComponent component)
        {
            definition.Triggers.Add(new ContinuousIntegrationTrigger()
            {
                SettingsSourceType = 2 // HACK: This is editor invisible, but this is required to inherit branch filters from YAML file.
            });

            definition.Triggers.Add(new PullRequestTrigger()
            {
                SettingsSourceType = 2, // HACK: See above.
                Forks = new Forks()
                {
                    AllowSecrets = false,
                    Enabled = true
                }
            });

            return true;
        }
    }
}
