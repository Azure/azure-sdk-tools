using System;
using System.IO;
using Octokit;
using Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload;
using System.Text.Json;
using Octokit.Internal;
using System.Threading.Tasks;
using Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;

namespace Azure.Sdk.Tools.GitHubEventProcessor
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // "dotnet run --" the "--" says don't count dotnet run as arguments
            if (args.Length < 2)
            {
                Console.WriteLine("Error: There are two required arguments:");
                Console.WriteLine(" 1. The github.event_name");
                Console.WriteLine(" 2. The GITHUB_PAYLOAD json file.");
                Environment.Exit(1);
		        return;
            }
            if (!File.Exists(args[1]))
            {
                Console.WriteLine($"Error: The GITHUB_PAYLOAD file {args[1]} does not exist.");
                Environment.Exit(1);
		        return;
            }

            string eventName = args[0];
            var serializer = new SimpleJsonSerializer();
            string rawJson = File.ReadAllText(args[1]);
            GitHubEventClient gitHubEventClient = new GitHubEventClient(OrgConstants.ProductHeaderName);
            await gitHubEventClient.WriteRateLimits("RateLimit at start of execution:");
            switch (eventName)
            {
                case EventConstants.Issues:
                    {
                        IssueEventGitHubPayload issueEventPayload = serializer.Deserialize<IssueEventGitHubPayload>(rawJson);
                        gitHubEventClient.SetConfigEntryOverrides(issueEventPayload.Repository);
                        await IssueProcessing.ProcessIssueEvent(gitHubEventClient, issueEventPayload);
                        break;
                    }
                case EventConstants.IssueComment:
                    {
                        IssueCommentPayload issueCommentPayload = serializer.Deserialize<IssueCommentPayload>(rawJson);
                        gitHubEventClient.SetConfigEntryOverrides(issueCommentPayload.Repository);
                        // IssueComment events are for both issues and pull requests. If the comment is on a pull request,
                        // then Issue's PullRequest object in the payload will be non-null
                        if (issueCommentPayload.Issue.PullRequest != null)
                        {
                            await PullRequestCommentProcessing.ProcessPullRequestCommentEvent(gitHubEventClient, issueCommentPayload);
                        }
                        else
                        {
                            await IssueCommentProcessing.ProcessIssueCommentEvent(gitHubEventClient, issueCommentPayload);
                        }

                        break;
                    }
                case EventConstants.PullRequestTarget:
                    {
                        // The pull_request, because of the auto_merge processing, requires more than just deserialization of the
                        // the rawJson.
                        PullRequestEventGitHubPayload prEventPayload = PullRequestProcessing.DeserializePullRequest(rawJson, serializer);
                        gitHubEventClient.SetConfigEntryOverrides(prEventPayload.Repository);
                        await PullRequestProcessing.ProcessPullRequestEvent(gitHubEventClient, prEventPayload);
                        break;
                    }
                case EventConstants.Schedule:
                    {
                        if (args.Length < 3)
                        {
                            Console.WriteLine("Error: For scheduled tasks there are three required arguments:");
                            Console.WriteLine($" 1. The github.event_name (which will be {EventConstants.Schedule} for cron tasks.");
                            Console.WriteLine(" 2. The GITHUB_PAYLOAD json file.");
                            Console.WriteLine(" 3. The cron task to run.");
                            Environment.Exit(1);
                            return;
                        }

                        ScheduledEventGitHubPayload scheduledEventPayload = serializer.Deserialize<ScheduledEventGitHubPayload>(rawJson);
                        gitHubEventClient.SetConfigEntryOverrides(scheduledEventPayload.Repository);
                        string cronTaskToRun = args[2];
                        await ScheduledEventProcessing.ProcessScheduledEvent(gitHubEventClient, scheduledEventPayload, cronTaskToRun);
                        break;
                    }
                default:
                    {
                        Console.WriteLine($"Event type {eventName} does not have any processing associated with it.");
                        break;
                    }
            }
            await gitHubEventClient.WriteRateLimits("RateLimit at end of execution:");
        }
    }
}
