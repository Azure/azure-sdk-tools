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
        private const string ReportBuildStatusKey = "reportBuildStatus";

        public override string SearchPattern => "ci.yml";

        public PullRequestValidationPipelineConvention(ILogger logger, PipelineGenerationContext context) : base(logger, context)
        {
        }

        protected override string GetDefinitionName(SdkComponent component)
        {
            return $"{Context.Prefix} - {component.Name} - ci";
        }

        protected override Task<bool> ApplyConventionAsync(BuildDefinition definition, SdkComponent component)
        {
            // NOTE: Not happy with this code at all, I'm going to look for a reasonable
            // API that can do equality comparisons (without having to do all the checks myself).

            var hasChanges = false;

            if (definition.Path != $"\\{this.Context.Prefix}")
            {
                definition.Path = $"\\{this.Context.Prefix}";
                hasChanges = true;
            }

            var ciTrigger = definition.Triggers.OfType<ContinuousIntegrationTrigger>().SingleOrDefault();

            if (ciTrigger == null)
            {
                definition.Triggers.Add(new ContinuousIntegrationTrigger()
                {
                    SettingsSourceType = 2 // HACK: This is editor invisible, but this is required to inherit branch filters from YAML file.
                });
                hasChanges = true;
            }
            else
            {
                if (ciTrigger.SettingsSourceType != 2)
                {
                    ciTrigger.SettingsSourceType = 2;
                    hasChanges = true;
                }
            }

            var prTrigger = definition.Triggers.OfType<PullRequestTrigger>().SingleOrDefault();

            if (prTrigger == null)
            {
                // TODO: We should probably be more complete here.
                definition.Triggers.Add(new PullRequestTrigger()
                {
                    SettingsSourceType = 2, // HACK: See above.
                    Forks = new Forks()
                    {
                        AllowSecrets = false,
                        Enabled = true
                    }
                });
                hasChanges = true;
            }
            else
            {
                // TODO: We should probably be more complete here.
                if (prTrigger.SettingsSourceType != 2 || prTrigger.Forks.AllowSecrets != false || prTrigger.Forks.Enabled != true)
                {
                    prTrigger.SettingsSourceType = 2;
                    prTrigger.Forks.AllowSecrets = false;
                    prTrigger.Forks.Enabled = true;
                    hasChanges = true;
                }
            }

            if (definition.Repository.Properties.TryGetValue(ReportBuildStatusKey, out var reportBuildStatusString))
            {
                if (!bool.TryParse(reportBuildStatusString, out var reportBuildStatusValue) || !reportBuildStatusValue)
                {
                    definition.Repository.Properties[ReportBuildStatusKey] = "true";
                    hasChanges = true;
                }
            }
            else
            {
                definition.Repository.Properties.Add(ReportBuildStatusKey, "true");
                hasChanges = true;
            }

            return Task.FromResult(hasChanges);
        }
    }
}
