using System;
using System.Collections.Concurrent;
using System.IO;
using Octokit;
using Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload;
using System.Text.Json;
using Octokit.Internal;
using System.Threading.Tasks;
using Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using Azure.Sdk.Tools.GitHubEventProcessor.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Azure.Core;
using Azure.Identity;
using System.Collections.Generic;
using System.Linq;

namespace Azure.Sdk.Tools.GitHubEventProcessor
{
    internal class Program
    {
        private const string AppConfigurationEndpoint = "https://gh-triage-app-config.azconfig.io";

        /// <summary>
        /// Creates an McpConfiguration instance by connecting to Azure App Configuration.
        /// This method is shared between the main entry point and test methods.
        /// </summary>
        private static McpConfiguration CreateMcpConfiguration()
        {
            TokenCredential credential = new ChainedTokenCredential(
                new ManagedIdentityCredential(),
                new AzureCliCredential(),
                new VisualStudioCredential(),
                new VisualStudioCodeCredential()
            );

            IConfiguration mcpConfig;
            try
            {
                var builder = new ConfigurationBuilder();
                builder.AddAzureAppConfiguration(options =>
                {
                    options.Connect(new Uri(AppConfigurationEndpoint), credential);
                });
                mcpConfig = builder.Build();
                Console.WriteLine($"Connected to Azure App Configuration: {AppConfigurationEndpoint}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not connect to Azure App Configuration. Error: {ex.Message}");
                mcpConfig = new ConfigurationBuilder().Build();
            }

            return new McpConfiguration(mcpConfig);
        }

        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "[HH:mm:ss] ";
                });
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<McpConfiguration>(sp => CreateMcpConfiguration());
                services.AddSingleton<McpIssueProcessing>();
            })
            .Build();

            var serviceProvider = host.Services;

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
                        var issueProcessor = GetIssueProcessor(issueEventPayload.Repository, serviceProvider);
                        await issueProcessor.ProcessIssueEvent(gitHubEventClient, issueEventPayload);
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

        /// <summary>
        /// Cache of IssueProcessing instances per repository.
        /// </summary>
        private static readonly ConcurrentDictionary<string, IssueProcessing> IssueProcessors = new();

        /// <summary>
        /// Factory method to get the appropriate IssueProcessing instance for a repository.
        /// Returns McpIssueProcessing for Microsoft MCP repository, otherwise returns base IssueProcessing.
        /// </summary>
        private static IssueProcessing GetIssueProcessor(Repository repository, IServiceProvider serviceProvider)
        {
            string repoKey = $"{repository.Owner.Login}/{repository.Name}";

            if (repository.Owner.Login.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) &&
                repository.Name.Equals("mcp", StringComparison.OrdinalIgnoreCase))
            {
                return IssueProcessors.GetOrAdd(repoKey, _ => serviceProvider.GetRequiredService<McpIssueProcessing>());
            }

            return IssueProcessors.GetOrAdd(repoKey, _ => new IssueProcessing());
        }
    }
}