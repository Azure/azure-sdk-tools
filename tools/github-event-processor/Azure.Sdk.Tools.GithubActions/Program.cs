using System;
using System.IO;
using Octokit;
using Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload;
using System.Text.Json;
using System.Dynamic;
using Octokit.Internal;
using System.Threading.Tasks;
using Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing;
using Azure.Sdk.Tools.GitHubEventProcessor.GitHubAuth;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.GitHubEventProcessor
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Error: There are two required arguments:");
                Console.WriteLine(" 1. The github.event_name");
                Console.WriteLine(" 2. The GITHUB_PAYLOAD json file.");
                Environment.Exit(1);
            }
            if (!File.Exists(args[1]))
            {
                Console.WriteLine($"Error: The GITHUB_PAYLOAD file {args[1]} does not exist.");
                Environment.Exit(1);
            }

            RulesConfiguration rc = new RulesConfiguration();
            rc.TestIt();
            if (true)
            {
                Environment.Exit(0);
            }

            string eventName = args[0];
            var serializer = new SimpleJsonSerializer();
            string rawJson = File.ReadAllText(args[1]);
            // JRS - need to plumb this through
            GitHubEventClient gitHubEventClient = new GitHubEventClient(OrgConstants.ProductHeaderName);
            GitHubClient gitHubClient = GitHubClientCreator.createClientWithGitHubEnvToken("azure-sdk-github-event-processor");

            // JRS-Remove this override once I figure out where codeowners is coming from
            CodeOwnerUtils.codeOwnersFilePathOverride = @"C:\src\azure-sdk-for-java\.github\CODEOWNERS";
            await RateLimitUtil.writeRateLimits(gitHubClient, "RateLimit at start of execution:");
            switch (eventName)
            {
                case EventConstants.issue:
                    {
                        IssueEventGitHubPayload issueEventPayload = serializer.Deserialize<IssueEventGitHubPayload>(rawJson);
                        await IssueProcessing.ProcessIssueEvent(gitHubClient, issueEventPayload);
                        break;
                    }
                case EventConstants.issue_comment:
                    {
                        IssueCommentPayload issueCommentPayload = serializer.Deserialize<IssueCommentPayload>(rawJson);
                        await IssueCommentProcessing.ProcessIssueCommentEvent(gitHubClient, issueCommentPayload);
                        break;
                    }
                case EventConstants.pull_request_target:
                    {
                        PullRequestEventGitHubPayload prEventPayload = serializer.Deserialize<PullRequestEventGitHubPayload>(rawJson);
                        using var doc = JsonDocument.Parse(rawJson);
                        // The actions event payload for a pull_request has a class on the pull request that
                        // the OctoKit.PullRequest class does not have. This will be null if the user the user does
                        // not have Auto-Merge enabled through the pull request UI and will be non-null if the
                        // user enabled it through the UI. An AutoMergeEnabled was added to the root of the
                        // PullRequestEventGitHubPayload class, which defaults to false. The information in the
                        // auto_merge is nothing we'd act on but there are a couple of rules that require knowing
                        // whether or not it's been set.
                        var autoMergeProp = doc.RootElement.GetProperty("pull_request").GetProperty("auto_merge");
                        if (JsonValueKind.Object == autoMergeProp.ValueKind)
                        {
                            prEventPayload.AutoMergeEnabled = true;
                        }

                        await PullRequestProcessing.ProcessPullRequestEvent(gitHubClient, prEventPayload);
                        break;
                    }
                case EventConstants.pull_request_review:
                    {
                        PullRequestReviewEventPayload prReviewEventPayload = serializer.Deserialize<PullRequestReviewEventPayload>(rawJson);
                        await PullRequestReviewProcessing.ProcessPullRequestReviewEvent(gitHubClient, prReviewEventPayload);
                        break;
                    }
                // Need to add cases for Cron jobs
                default:
                    {
                        break;
                    }
            }

            // await ScheduledEventProcessing.ProcessScheduledEvent(_gitHubClient, rawJson);
            // await IssueProcessing.ProcessIssueEvent(_gitHubClient, rawJson);
            // Actual event processing
            // await IssueProcessing.ProcessIssueEvent(_gitHubClient, rawJson);
            // await IssueCommentProcessing.ProcessIssueComment(_gitHubClient, rawJson);
            // await PullRequestProcessing.ProcessPullRequestEvent(_gitHubClient, rawJson);

            // JRS - processing to test rate limit changes based upon different actions
            // JRS - test to see how fetching files affects limit count
            //await PullRequestProcessing.TestFunctionToGetPRLablesForFiles(_gitHubClient);
            // await IssueProcessing.TestFunctionToCheckRatesForRemovingLabels(_gitHubClient, rawJson);
            // await IssueProcessing.TestFunctionToCheckRatesForAddingLabels(_gitHubClient, rawJson);
            // await IssueProcessing.TestFunctionToCheckRatesForAddingAndRemovingLabels(_gitHubClient, rawJson);
            // await PullRequestProcessing.UpdatePRLabel(_gitHubClient, rawJson);
            // await SearchUtil.SearchIssuesTest(_gitHubClient);
            //await PullRequestProcessing.DismissPullRequestApprovals(_gitHubClient, rawJson);
            //var serializer = new SimpleJsonSerializer();
            //ScheduledEventGitHubPayload scheduledEventGitHubPayload = serializer.Deserialize<ScheduledEventGitHubPayload>(rawJson);
            //await ScheduledEventProcessing.LockClosedIssues(_gitHubClient, scheduledEventGitHubPayload);
            await RateLimitUtil.writeRateLimits(gitHubClient, "RateLimit at end of execution:");
        }
    }
}
