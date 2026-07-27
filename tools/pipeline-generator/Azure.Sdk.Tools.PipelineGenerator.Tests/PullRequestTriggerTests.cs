using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.Build.WebApi;
using NUnit.Framework;
using PipelineGenerator.Conventions;
using System.Linq;

namespace Azure.Sdk.Tools.PipelineGenerator.Tests
{
    /// <summary>
    /// Tests for the pull request trigger defaults applied by the pipeline conventions.
    /// Public (non-secure) pipelines must trigger automatically on pull requests, while
    /// secure pipelines, which have access to secrets, require a comment (`/azp run`) to trigger.
    /// </summary>
    public class PullRequestTriggerTests
    {
        private class TestPipelineConvention : PipelineConvention
        {
            public TestPipelineConvention() : base(NullLogger.Instance, null)
            {
            }

            public override string SearchPattern => "ci.yml";
            public override string PipelineNameSuffix => " - test";
            public override string PipelineCategory => "test";

            public new bool EnsureDefaultPullRequestTrigger(BuildDefinition definition, bool overrideYaml, bool securePipeline)
            {
                return base.EnsureDefaultPullRequestTrigger(definition, overrideYaml, securePipeline);
            }
        }

        private static PullRequestTrigger GetPullRequestTrigger(BuildDefinition definition)
        {
            return definition.Triggers.OfType<PullRequestTrigger>().Single();
        }

        [Test]
        public void PublicPipelineAddsTriggerWithoutCommentRequired()
        {
            var definition = new BuildDefinition();

            var hasChanges = new TestPipelineConvention().EnsureDefaultPullRequestTrigger(
                definition, overrideYaml: false, securePipeline: false);

            Assert.That(hasChanges, Is.True);

            var trigger = GetPullRequestTrigger(definition);
            Assert.That(trigger.IsCommentRequiredForPullRequest, Is.False);
            Assert.That(trigger.RequireCommentsForNonTeamMembersOnly, Is.False);
            Assert.That(trigger.Forks.AllowSecrets, Is.False);
            Assert.That(trigger.Forks.Enabled, Is.True);
            // Pull the pull request trigger settings (including branch filters) from the yaml file.
            Assert.That(trigger.SettingsSourceType, Is.EqualTo(2));
        }

        [Test]
        public void PublicPipelineRemovesCommentRequiredFromExistingTrigger()
        {
            var definition = new BuildDefinition();
            definition.Triggers.Add(new PullRequestTrigger
            {
                SettingsSourceType = 2,
                Forks = new Forks { AllowSecrets = true, Enabled = true },
                RequireCommentsForNonTeamMembersOnly = false,
                IsCommentRequiredForPullRequest = true
            });

            var hasChanges = new TestPipelineConvention().EnsureDefaultPullRequestTrigger(
                definition, overrideYaml: false, securePipeline: false);

            Assert.That(hasChanges, Is.True);

            var trigger = GetPullRequestTrigger(definition);
            Assert.That(trigger.IsCommentRequiredForPullRequest, Is.False);
            Assert.That(trigger.Forks.AllowSecrets, Is.False);
        }

        [Test]
        public void SecurePipelineAddsTriggerWithCommentRequired()
        {
            var definition = new BuildDefinition();

            var hasChanges = new TestPipelineConvention().EnsureDefaultPullRequestTrigger(
                definition, overrideYaml: true, securePipeline: true);

            Assert.That(hasChanges, Is.True);

            var trigger = GetPullRequestTrigger(definition);
            Assert.That(trigger.IsCommentRequiredForPullRequest, Is.True);
            Assert.That(trigger.RequireCommentsForNonTeamMembersOnly, Is.False);
            Assert.That(trigger.Forks.AllowSecrets, Is.True);
            Assert.That(trigger.Forks.Enabled, Is.True);
            // Override the pull request trigger settings from the yaml file.
            Assert.That(trigger.SettingsSourceType, Is.EqualTo(1));
            Assert.That(trigger.BranchFilters, Is.EqualTo(new[] { "+*" }));
        }

        [Test]
        public void PublicPipelineTriggerIsUnchangedWhenAlreadyCorrect()
        {
            var definition = new BuildDefinition();
            definition.Triggers.Add(new PullRequestTrigger
            {
                SettingsSourceType = 2,
                Forks = new Forks { AllowSecrets = false, Enabled = true },
                RequireCommentsForNonTeamMembersOnly = false,
                IsCommentRequiredForPullRequest = false
            });

            var hasChanges = new TestPipelineConvention().EnsureDefaultPullRequestTrigger(
                definition, overrideYaml: false, securePipeline: false);

            Assert.That(hasChanges, Is.False);
        }
    }
}
