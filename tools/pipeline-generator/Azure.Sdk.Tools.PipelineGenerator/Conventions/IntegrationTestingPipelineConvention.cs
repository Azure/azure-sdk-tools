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
        public override bool IsScheduled => !Context.NoSchedule;
        public override bool RemoveCITriggers => true;

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

            // Ensure PR trigger
            var prTriggers = definition.Triggers.OfType<PullRequestTrigger>();
            if (prTriggers == default || !prTriggers.Any())
            {
                var newTrigger = GetDefaultPrTrigger();
                definition.Triggers.Add(newTrigger);
                hasChanges = true;
            }
            else
            {
                foreach (var trigger in prTriggers)
                {
                    if (EnsurePrTriggerDefaults(trigger))
                    {
                        hasChanges = true;
                    }
                }
            }

            return hasChanges;
        }

        private PullRequestTrigger GetDefaultPrTrigger()
        {
            var newTrigger = new PullRequestTrigger
            {
                Forks = new Forks { AllowSecrets = true, Enabled = true },
                RequireCommentsForNonTeamMembersOnly = false,
                IsCommentRequiredForPullRequest = true,
            };
            newTrigger.BranchFilters.Add("+master");

            return newTrigger;
        }

        private bool EnsurePrTriggerDefaults(PullRequestTrigger target)
        {
            var hasChanges = false;

            if (!target.Forks.AllowSecrets)
            {
                target.Forks.AllowSecrets = true;
                hasChanges = true;
            }

            if (!target.Forks.Enabled)
            {
                target.Forks.Enabled = true;
                hasChanges = true;
            }

            if (target.RequireCommentsForNonTeamMembersOnly)
            {
                target.RequireCommentsForNonTeamMembersOnly = false;
                hasChanges = true;
            }

            if (!target.IsCommentRequiredForPullRequest)
            {
                target.IsCommentRequiredForPullRequest = true;
                hasChanges = true;
            }

            return hasChanges;
        }
    }
}
