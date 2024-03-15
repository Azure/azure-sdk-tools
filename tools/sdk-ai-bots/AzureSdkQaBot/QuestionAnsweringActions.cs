using Microsoft.Bot.Builder;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel;
using Microsoft.TeamsAI.AI.Action;
using Microsoft.TeamsAI;
using Newtonsoft.Json;
using AzureSdkQaBot.Model;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Bot.Schema;

namespace AzureSdkQaBot
{
    public class QuestionAnsweringActions
    {
        private IKernel _kernel;
        private readonly Application<AppState, AppStateManager> _application;
        private readonly ILogger _logger;

        public QuestionAnsweringActions(Application<AppState, AppStateManager> application, IKernel kernel, ILogger logger)
        {
            _application = application;
            _kernel = kernel;
            _logger = logger;
        }

        [Action("QuestionAnswering")]
        public async Task<bool> QuestionAnswering([ActionTurnContext] ITurnContext turnContext, [ActionTurnState] AppState appState)
        {
            string query = turnContext.Activity.Text;

            this._logger.LogInformation($"QuestionAnswering: query: {query}");
            if (string.IsNullOrWhiteSpace(query))
            {
                await turnContext.SendActivityAsync("The input contains only white spaces, which is not valid. Please enter a valid input.");
            }
            else
            {
                (string answer, List<string> relevancies) = await this.QuestionAnsweringHandler(turnContext, appState);

                // add support message for tool failures
                answer = GitHubPrActions.AddSupportMessageForTools(turnContext, answer);

                // redact query to less than 60 characters
                if (query.Length > 60)
                {
                    query = turnContext.Activity.Text.Substring(0, 57) + "...";
                }

                Attachment card = await CardBuilder.NewQAAttachment(query, answer, relevancies, CancellationToken.None);
                try
                {
                    await turnContext.SendActivityAsync(MessageFactory.Attachment(card), CancellationToken.None);
                }
                catch (Exception ex)
                {
                    await turnContext.TraceActivityAsync("BadRequest", ex, "The bot received a bad request from Teams.");
                    Console.WriteLine("Error:" + ex);
                    _logger.LogError($"QuestionAnswering: the bot received a bad request from Teams. Message is: {ex}");
                }
            }

            return false;
        }
        public async Task<Tuple<string, List<string>>> QuestionAnsweringHandler([ActionTurnContext] ITurnContext turnContext, [ActionTurnState] AppState appState)
        {
            var memories = _kernel.Memory.SearchAsync("azuresdk-knowledge-rag", turnContext.Activity.Text, limit: 3, minRelevanceScore: 0.77, withEmbeddings: false, CancellationToken.None);

            List<Citation> citations = new();
            Dictionary<string, string> documentLinkToSource = new();

            await foreach (MemoryQueryResult memory in memories)
            {
                RagChunk chunk = JsonConvert.DeserializeObject<RagChunk>(memory.Metadata.AdditionalMetadata)!;
                string source = chunk.HeadingTitle == null
                    ? $"[{chunk.DocumentTitle}]({chunk.DocumentLink})"
                    : $"[{chunk.HeadingTitle}]({chunk.headingLink})";
                Citation citation = new(source, chunk.RagText);
                if (!citations.Contains(citation))
                {
                    citations.Add(citation);
                    documentLinkToSource[chunk.DocumentLink] = source;
                }
            }

            if (appState == null)
            {
                throw new ArgumentNullException(nameof(appState));
            }

            appState.Conversation!.Citations = citations;
            appState.Temp!.Input = turnContext.Activity.Text;
            string result;
            try
            {
                result = await _application.AI.CompletePromptAsync(turnContext, appState, "QA", _application.AI.Options, CancellationToken.None);
                result = result.Trim();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CompletePromptAsync error: {ex}");
                this._logger.LogError($"QuestionAnsweringHandler-CompletePromptAsync error: {ex}");
                return Tuple.Create(Constants.Message_Error_ExceedTokenLimit, new List<string>());
            }

            Console.WriteLine("========");
            Console.WriteLine(result);
            Console.WriteLine("========");

            string answer = result;
            List<string> relevancies = new();

            if (!answer.StartsWith("Sorry, I do not know"))
            {
                string sourceTag = "**Source**: ";
                int index = answer.LastIndexOf(sourceTag);
                if (index != -1)
                {
                    string source = answer.Substring(index + sourceTag.Length);
                    foreach (string documentLink in documentLinkToSource.Keys)
                    {
                        if (!source.Contains(documentLink))
                        {
                            relevancies.Add(documentLinkToSource[documentLink]);
                        }
                    }
                }
            }

            return Tuple.Create(answer, relevancies);
        }

        [Action(DefaultActionTypes.FlaggedInputActionName)]
        public async Task<bool> FlaggedInputAction([ActionTurnContext] ITurnContext turnContext, [ActionEntities] Dictionary<string, object> entities)
        {
            string entitiesJsonString = JsonConvert.SerializeObject(entities);
            await turnContext.SendActivityAsync($"I'm sorry your message was flagged: {entitiesJsonString}");
            return false;
        }

        [Action(DefaultActionTypes.FlaggedOutputActionName)]
        public async Task<bool> FlaggedOutputAction([ActionTurnContext] ITurnContext turnContext)
        {
            await turnContext.SendActivityAsync("I'm not allowed to talk about such things.");
            return false;
        }
    }
}
