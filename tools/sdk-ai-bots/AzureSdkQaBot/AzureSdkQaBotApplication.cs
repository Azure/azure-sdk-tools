using Microsoft.Teams.AI;
using Microsoft.SemanticKernel;
using Octokit;
using AzureSdkQaBot.Model;
using Microsoft.Teams.AI.AI.Clients;
using Microsoft.Teams.AI.AI.Prompts;

namespace AzureSdkQaBot
{
    public class AzureSdkQaBotApplication : Application<AppState>
    {
        private IKernel kernel;
        private IGitHubClient GitHubClient;
        private readonly ILogger logger;

        public AzureSdkQaBotApplication(ApplicationOptions<AppState> options, LLMClient<string> llmClient, PromptManager promptManager, IKernel semanticKernel, IGitHubClient GitHubClient, ILogger logger) : base(options)
        {
            this.kernel = semanticKernel;
            this.GitHubClient = GitHubClient;
            this.logger = logger;
            AI.ImportActions(new QuestionAnsweringActions(llmClient, promptManager, this.kernel, this.logger));
            AI.ImportActions(new GitHubPrActions(llmClient, promptManager, this.kernel, this.GitHubClient, this.logger));
        }
    }
}
