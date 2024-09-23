using Microsoft.TeamsAI;
using Microsoft.SemanticKernel;
using Octokit;
using AzureSdkQaBot.Model;

namespace AzureSdkQaBot
{
    public class AzureSdkQaBotApplication : Application<AppState, AppStateManager>
    {
        private IKernel kernel;
        private IGitHubClient GitHubClient;
        private readonly ILogger logger;

        public AzureSdkQaBotApplication(ApplicationOptions<AppState, AppStateManager> options, IKernel semanticKernel, IGitHubClient GitHubClient, ILogger logger) : base(options)
        {
            this.kernel = semanticKernel;
            this.GitHubClient = GitHubClient;
            this.logger = logger;
            AI.ImportActions(new QuestionAnsweringActions(this, this.kernel, this.logger));
            AI.ImportActions(new GitHubPrActions(this, this.kernel, this.GitHubClient, this.logger));
            AI.Prompts.AddFunction("getCitations", (turnContext, appState) =>
            {
                var citations = appState.Conversation!.Citations!.Select((citation, index) =>
                {
                    return $"<Citation {index + 1}>{Environment.NewLine}Source:{Environment.NewLine}{citation.Source}{Environment.NewLine}Content:{Environment.NewLine}{citation.Content}";
                });
                string context = string.Join(Environment.NewLine + Environment.NewLine, citations);
                return Task.FromResult(context);
            });
            AI.Prompts.AddFunction("getInput", (turnContext, appState) =>
            {
                string input = GitHubPrActions.GetUserQueryFromContext(turnContext);
                return Task.FromResult(input);
            });
        }
    }
}
