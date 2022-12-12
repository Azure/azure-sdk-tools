using System;
using System.IO;
using Octokit;
using Azure.Sdk.Tools.GithubEventProcessor.GitHubPayload;
using System.Text.Json;
using System.Dynamic;
using Octokit.Internal;
using System.Threading.Tasks;
using Azure.Sdk.Tools.GithubEventProcessor.EventProcessing;
using Azure.Sdk.Tools.GithubEventProcessor.GitHubAuth;
using Azure.Sdk.Tools.GithubEventProcessor.Utils;
using Azure.Sdk.Tools.GithubEventProcessor.Constants;

namespace Azure.Sdk.Tools.GithubEventProcessor
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length < 2 || !File.Exists(args[1]))
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
            string eventName = args[0];
            string rawJson = File.ReadAllText(args[1]);
            GitHubClient gitHubClient = GitHubClientCreator.createClientWithGitHubEnvToken("azure-sdk-github-event-processor");
            // JRS-BeginRemove
            // gitHubClient.
            // JRS-EndRemove

            await RateLimitUtil.writeRateLimits(gitHubClient, "RateLimit at start of execution:");

            switch (eventName)
            {
                case EventConstants.issue:
                    {
                        break;
                    }
                case EventConstants.issue_comment:
                    {
                        break;
                    }
                case EventConstants.pull_request_target:
                    {
                        break;
                    }
                case EventConstants.pull_request_review:
                    {
                        break;
                    }
                default:
                    {
                        break;
                    }
            }

            // await ScheduledEventProcessing.ProcessScheduledEvent(gitHubClient, rawJson);
            // await IssueProcessing.ProcessIssueEvent(gitHubClient, rawJson);
            // Actual event processing
            // await IssueProcessing.ProcessIssueEvent(gitHubClient, rawJson);
            // await IssueCommentProcessing.ProcessIssueComment(gitHubClient, rawJson);
            // await PullRequestProcessing.ProcessPullRequestEvent(gitHubClient, rawJson);

            // JRS - processing to test rate limit changes based upon different actions
            // JRS - test to see how fetching files affects limit count
            //await PullRequestProcessing.TestFunctionToGetPRLablesForFiles(gitHubClient);
            // await IssueProcessing.TestFunctionToCheckRatesForRemovingLabels(gitHubClient, rawJson);
            // await IssueProcessing.TestFunctionToCheckRatesForAddingLabels(gitHubClient, rawJson);
            // await IssueProcessing.TestFunctionToCheckRatesForAddingAndRemovingLabels(gitHubClient, rawJson);
            // await PullRequestProcessing.UpdatePRLabel(gitHubClient, rawJson);
            // await SearchUtil.SearchIssuesTest(gitHubClient);
            //await PullRequestProcessing.DismissPullRequestApprovals(gitHubClient, rawJson);
            //var serializer = new SimpleJsonSerializer();
            //ScheduledEventGitHubPayload scheduledEventGitHubPayload = serializer.Deserialize<ScheduledEventGitHubPayload>(rawJson);
            //await ScheduledEventProcessing.LockClosedIssues(gitHubClient, scheduledEventGitHubPayload);
            await RateLimitUtil.writeRateLimits(gitHubClient, "RateLimit at end of execution:");
            Console.WriteLine("done");
        }
    }
}

// JRS - other attempts at deserializing an object

// Class was created using Edit > Paste special > Paste Json as classes
//IssueGitHubPayload jsonIssuePayload = System.Text.Json.JsonSerializer.Deserialize<IssueGitHubPayload>(rawJson);
//Console.WriteLine(jsonIssuePayload.action);
//foreach (Assignee assignee in jsonIssuePayload.issue.assignees)
//{
//    Console.WriteLine($"assignee={assignee}");
//}
//Console.WriteLine(jsonIssuePayload.issue.assignee);

//// With newtonsoft's dynamic
//dynamic payloadNewtonsoft = JsonConvert.DeserializeObject<dynamic>(rawJson);
//Console.WriteLine(payloadNewtonsoft.action);
//foreach (dynamic assignee in payloadNewtonsoft.issue.assignees)
//{
//    Console.WriteLine($"assignee={assignee}");
//}

//// With Microsoft's ExpandoObject - great for flat json, terrible for anything nested 
//dynamic msJsonObject = System.Text.Json.JsonSerializer.Deserialize<ExpandoObject>(rawJson);
//Console.WriteLine(msJsonObject.action);
//Console.WriteLine(msJsonObject.issue.GetType());

//// With Microsoft's Json
//var jsonDoc = JsonDocument.Parse(rawJson);
//Console.WriteLine(jsonDoc.RootElement.GetProperty("action"));
//foreach (dynamic assignee in jsonDoc.RootElement.GetProperty("issue")!.GetProperty("assignees").EnumerateArray())
//{
//    Console.WriteLine($"assignee={assignee}");
//}

// JRS - regular json serializer doesn't work here
//IssueEventPayload issueEventPayload = JsonSerializer.Deserialize<IssueEventPayload>(rawJson);
//Console.WriteLine(issueEventPayload.Action);
//foreach (User assignee in issueEventPayload.Issue.Assignees)
//{
//    Console.WriteLine($"assignee={assignee}");
//}

// This is close but not quite, there are fields missing, for example labels_url
//var serializer = new SimpleJsonSerializer();
//IssueEventPayload issueEventPayload = serializer.Deserialize<IssueEventPayload>(rawJson);
//Console.WriteLine(issueEventPayload.Action);
//foreach (User assignee in issueEventPayload.Issue.Assignees)
//{
//    Console.WriteLine($"assignee={assignee}");
//}
