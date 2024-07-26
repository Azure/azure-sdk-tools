using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.TeamsAI;
using Microsoft.TeamsAI.AI.Action;
using System.Text.RegularExpressions;
using Octokit;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.SemanticKernel;
using AzureSdkQaBot.Model;

namespace AzureSdkQaBot
{
    public class GitHubPrActions
    {
        private readonly IGitHubClient _gitHubClient;
        private readonly ILogger _logger;
        private IKernel _kernel;
        private const string ReviewWorkflow_MgmtPlane = "https://aka.ms/azsdk/pr-diagram";
        private const string ReviewWorkflow_DataPlane = "https://eng.ms/docs/products/azure-developer-experience/design/api-specs-pr/api-specs-pr?tabs=dataplane";
        private IList<string> _referencesMgmtPlane = new List<string>() { $"[Workflow Reference]({ReviewWorkflow_MgmtPlane})" };
        private IList<string> _referencesDataPlane = new List<string>() { $"[Workflow Reference]({ReviewWorkflow_DataPlane})" };
        private QuestionAnsweringActions _questionAnsweringActions;

        // DONOT use below fields directly and use corresponding Get{} method to save the GitHub api call times because they're not initialized by default
        private PullRequest? _pullRequest;
        private IReadOnlyList<Label>? _labels;
        // private IOrderedEnumerable<CheckRun>? _checkRun;

        public GitHubPrActions(Application<AppState, AppStateManager> application, IKernel kernel, IGitHubClient client, ILogger logger)
        {
            _gitHubClient = client;
            _logger = logger;
            _questionAnsweringActions = new QuestionAnsweringActions(application, kernel, _logger);
        }

        [Action("NonGitHubPRHandler")]
        public async Task<bool> NonGitHubPRHandler([ActionTurnContext] ITurnContext turnContext, [ActionTurnState] AppState turnState, [ActionEntities] Dictionary<string, object> entities)
        {
            if (turnContext == null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            if (turnState == null)
            {
                throw new ArgumentNullException(nameof(turnState));
            }

            this._logger.LogInformation($"NonGitHubPRHandler:");

            string prLink = (string)entities["prLink"];
            if (string.IsNullOrEmpty(prLink) || !prLink.Contains("https://"))
            {
                await _questionAnsweringActions.QuestionAnswering(turnContext, turnState);
                return false;
            }
            // redact query to less than 60 characters
            string query = turnContext.Activity.Text;
            if (query.Length > 60)
            {
                query = turnContext.Activity.Text.Substring(0, 57) + "...";
            }

            this._logger.LogInformation($"NonGitHubPRHandler: PR link:{prLink}. Query: {query}");
            string reply = "";
            string action = "";

            if (!prLink.Contains("https://github.com/"))
            {
                reply = Constants.Message_Error_NonGithub_PR;
            }

            // add support message for tool failures
            reply = AddSupportMessageForTools(turnContext, reply);

            // call QA function
            (string additionalAnswer, List<string>? relevancies) = await _questionAnsweringActions.QuestionAnsweringHandler(turnContext, turnState);

            // get the relevance links based on user input or PR labels to differentiate the mgmt plane and data plane
            IList<string> references = await GetRelevanceLinks(turnContext);
            Attachment card;
            if (additionalAnswer.StartsWith("Sorry, I do not know"))
            {
                additionalAnswer = "";
                relevancies = null;
            }
            card = await CardBuilder.NewPRAndQAAttachment(query, reply, action, additionalAnswer, references, relevancies, CancellationToken.None);

            try
            {
                await turnContext.SendActivityAsync(MessageFactory.Attachment(card), CancellationToken.None);
            }
            catch (Exception ex)
            {
                await turnContext.TraceActivityAsync("SendActivityError", ex, "The bot received an error when send message to Teams.");
                this._logger.LogError($"NonGitHubPRHandler: PR link:{prLink}. Query:{query}. Error: {ex}");
            }

            // End the current chain
            return false;
        }

        [Action("PullRequestReviewNextStep")]
        public async Task<bool> PullRequestReviewNextStep([ActionTurnContext] ITurnContext turnContext, [ActionTurnState] AppState turnState, [ActionEntities] Dictionary<string, object> entities)
        {
            if (turnContext == null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            if (turnState == null)
            {
                throw new ArgumentNullException(nameof(turnState));
            }

            string prLink = (string)entities["prLink"];
            if (string.IsNullOrEmpty(prLink) || !prLink.Contains("https://"))
            {
                await _questionAnsweringActions.QuestionAnswering(turnContext, turnState);
                return false;
            }

            // redact query to less than 60 characters
            string query = turnContext.Activity.Text;
            if (query.Length > 60)
            {
                query = turnContext.Activity.Text.Substring(0, 57) + "...";
            }
            this._logger.LogInformation($"PullRequestReviewNextStep: PR link:{prLink}. Query: {query}");

            (string org, string repo, int prNum) = GetPrInfoFromPrLink(prLink);
            string reply = "";
            string action = "";

            // Get pull request info
            if (prNum != 0)
            {
                (bool isPrMerged, reply) = await IsPrMerged(org, repo, prNum);
                if (reply == Constants.Message_Error_Null_PR)
                {
                    reply += $" {prLink}";
                }
                else if (!isPrMerged)
                {
                    // Check the pull request labels, check result, and prompt next step
                    (reply, action) = await CheckPrStatus(org, repo, prNum);
                }
            }

            // add support message for tool failures
            reply = AddSupportMessageForTools(turnContext, reply);

            // call QA function
            (string additionalAnswer, List<string>? relevancies) = await _questionAnsweringActions.QuestionAnsweringHandler(turnContext, turnState);

            // get the relevance links based on user input or PR labels to differenciate the mgmt plane and data plane
            IList<string> references = await GetRelevanceLinks(turnContext, org, repo, prNum);
            Attachment card;
            if (additionalAnswer.StartsWith("Sorry, I do not know"))
            {
                additionalAnswer = "";
                relevancies = null;
            }
            card = await CardBuilder.NewPRAndQAAttachment(query, reply, action, additionalAnswer, references, relevancies, CancellationToken.None);

            try
            {
                await turnContext.SendActivityAsync(MessageFactory.Attachment(card), CancellationToken.None);
            }
            catch (Exception ex)
            {
                await turnContext.TraceActivityAsync("SendActivityError", ex, "The bot received an error when send message to Teams.");
                this._logger.LogError($"PullRequestReviewNextStep: PR link:{prLink}. Query:{query}. Error: {ex}");
            }

            // End the current chain
            return false;
        }

        [Action("MergePullRequest")]
        public async Task<bool> MergePullRequest([ActionTurnContext] ITurnContext turnContext, [ActionTurnState] AppState turnState, [ActionEntities] Dictionary<string, object> entities)
        {
            if (turnContext == null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            if (turnState == null)
            {
                throw new ArgumentNullException(nameof(turnState));
            }

            string prLink = "";
            if (entities != null)
            {
                prLink = (string)entities["prLink"];
            }
            if (entities == null || string.IsNullOrEmpty(prLink) || !prLink.Contains("https://"))
            {
                await _questionAnsweringActions.QuestionAnswering(turnContext, turnState);
                return false;
            }
            // redact query to less than 60 characters
            string query = turnContext.Activity.Text;
            if (query.Length > 60)
            {
                query = turnContext.Activity.Text.Substring(0, 57) + "...";
            }
            this._logger.LogInformation($"MergePullRequest: PR link:{prLink}. Query: {query}");
            (string org, string repo, int prNum) = GetPrInfoFromPrLink(prLink);
            string reply = "";
            string action = "";

            // Get pull request info
            if (prNum != 0)
            {
                (bool isPrMerged, reply) = await IsPrMerged(org, repo, prNum);
                if (reply == Constants.Message_Error_Null_PR)
                {
                    reply += $" {prLink}";
                }
                else if(!isPrMerged)
                {
                    // Try to merge the PR
                    (reply, action) = await DoPrMerge(org, repo, prNum);
                }
            }
            // get the relevance links based on user input or PR labels to differenciate the mgmt plane and data plane
            IList<string> references = await GetRelevanceLinks(turnContext, org, repo, prNum);
            var card = await CardBuilder.NewPRAndQAAttachment(query, reply, action, "", references, null, CancellationToken.None);

            try
            {
                await turnContext.SendActivityAsync(MessageFactory.Attachment(card), CancellationToken.None);
            }
            catch (Exception ex)
            {
                await turnContext.TraceActivityAsync("SendActivityError", ex, "The bot received an error when send message to Teams.");
                Console.WriteLine("Error:" + ex);
                this._logger.LogError($"MergePullRequest: PR link:{prLink}. Query:{query}. Error: {ex}");
            }

            // End the current chain
            return false;
        }

        [Action("PrBreakingChangeReview")]
        public async Task<bool> PullRequestBreakingChangeReview([ActionTurnContext] ITurnContext turnContext, [ActionTurnState] AppState turnState, [ActionEntities] Dictionary<string, object> entities)
        {
            if (turnContext == null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            if (turnState == null)
            {
                throw new ArgumentNullException(nameof(turnState));
            }

            string prLink = (string)entities["prLink"];
            if (string.IsNullOrEmpty(prLink) || !prLink.Contains("https://"))
            {
                await _questionAnsweringActions.QuestionAnswering(turnContext, turnState);
                return false;
            }

            // redact query to less than 60 characters
            string query = turnContext.Activity.Text;
            if (query.Length > 60)
            {
                query = turnContext.Activity.Text.Substring(0, 57) + "...";
            }
            this._logger.LogInformation($"PrBreakingChangeReview: PR link:{prLink}. Query: {query}");
            (string org, string repo, int prNum) = GetPrInfoFromPrLink(prLink);
            string reply = "";
            string action = "";

            // Get pull request info
            if (prNum != 0)
            {
                (bool isPrMerged, reply) = await IsPrMerged(org, repo, prNum);
                if (reply == Constants.Message_Error_Null_PR)
                {
                    reply += $" {prLink}";
                }
                else if (!isPrMerged)
                {
                    IReadOnlyList<Label> labels = await GetPrLabels(org, repo, prNum);
                    bool isMgmtPlane = false;
                    if (labels.Any(x => x.Name == Constants.Label_ResourceManager))
                    {
                        isMgmtPlane = true;
                    }
                    // check breaking change
                    (bool completeBcReview, reply, action) = CheckBreakingChangeReview(labels);
                    if (completeBcReview || reply == "")
                    {
                        // sdk breaking change review
                        if (!labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_Go || x.Name == Constants.Label_SDKBreakingChange_Python || x.Name == Constants.Label_SDKBreakingChange_PythonTrack2 || x.Name == Constants.Label_SDKBreakingChange_JavaScript))
                        {
                            reply = Constants.Message_SDKBreakingChangeReview_NotNeeded;
                        }
                        else if ((labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_Go) && !labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_Go_Approval))
                                    || (labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_Python || x.Name == Constants.Label_SDKBreakingChange_PythonTrack2) && !labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_Python_Approval))
                                    || (labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_JavaScript) && !labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_JavaScript_Approval)))
                        {
                            bool completeArmReview = false;
                            if (isMgmtPlane)
                            {
                                (completeArmReview, reply, action) = CheckArmReview(labels);
                            }
                            if (isMgmtPlane && !completeArmReview && reply != "")
                            {
                                // arm review isn't finished
                                reply = Constants.Message_ArmReview_NotFinished_BeforeSdkBreakingChangeReview;
                                action = Constants.Action_ArmReview_NotFinished_BeforeSdkBreakingChangeReview;
                            }
                            // arm review completes or not needed
                            else if (labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_Go) && !labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_Go_Approval))
                            {
                                reply = Constants.Message_SDKBreakingChangeGo_NotFinished;
                                action = Constants.Action_SDKBreakingChangeGo_NotFinished;
                            }
                            else if (labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_Python) && !labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_Python_Approval))
                            {
                                reply = Constants.Message_SDKBreakingChangePython_NotFinished;
                                action = Constants.Action_SDKBreakingChangePython_NotFinished;
                            }
                            else if (labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_JavaScript) && !labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_JavaScript_Approval))
                            {
                                reply = Constants.Message_SDKBreakingChangeJavaScript_NotFinished;
                                action = Constants.Action_SDKBreakingChangeJavaScript_NotFinished;
                            }
                        }
                        else
                        {
                            reply = Constants.Message_BreakingChangeReview_Finished;
                            action = Constants.Action_BreakingChangeReview_Finished;
                        }
                    }
                }
                // add support message for tool failures
                reply = AddSupportMessageForTools(turnContext, reply);
            }

            // call QA function
            (string additionalAnswer, List<string>? relevancies) = await _questionAnsweringActions.QuestionAnsweringHandler(turnContext, turnState);

            // get the relevance links based on user input or PR labels to differenciate the mgmt plane and data plane
            IList<string> references = await GetRelevanceLinks(turnContext, org, repo, prNum);
            Attachment card;
            if (additionalAnswer.StartsWith("Sorry, I do not know"))
            {
                additionalAnswer = "";
                relevancies = null;
            }
            card = await CardBuilder.NewPRAndQAAttachment(query, reply, action, additionalAnswer, references, relevancies, CancellationToken.None);

            try
            {
                await turnContext.SendActivityAsync(MessageFactory.Attachment(card), CancellationToken.None);
            }
            catch (Exception ex)
            {
                await turnContext.TraceActivityAsync("SendActivityError", ex, "The bot received an error when send message to Teams.");
                Console.WriteLine("Error:" + ex);
                this._logger.LogError($"PrBreakingChangeReview: PR link:{prLink}. Query:{query}. Error: {ex}");
            }

            // End the current chain
            return false;
        }

        [Action("PrBreakingChangeReview-Go")]
        public async Task<bool> PullRequestGoSdkBreakingChangeReview([ActionTurnContext] ITurnContext turnContext, [ActionTurnState] AppState turnState, [ActionEntities] Dictionary<string, object> entities)
        {
            if (turnContext == null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            if (turnState == null)
            {
                throw new ArgumentNullException(nameof(turnState));
            }

            string prLink = (string)entities["prLink"];
            if (string.IsNullOrEmpty(prLink) || !prLink.Contains("https://"))
            {
                await _questionAnsweringActions.QuestionAnswering(turnContext, turnState);
                return false;
            }

            // redact query to less than 60 characters
            string query = turnContext.Activity.Text;
            if (query.Length > 60)
            {
                query = turnContext.Activity.Text.Substring(0, 57) + "...";
            }
            this._logger.LogInformation($"PrBreakingChangeReview-Go: PR link:{prLink}. Query: {query}");
            (string org, string repo, int prNum) = GetPrInfoFromPrLink(prLink);
            string reply = "";
            string action = "";

            // Get pull request info
            if (prNum != 0)
            {
                (bool isPrMerged, reply) = await IsPrMerged(org, repo, prNum);
                if (reply == Constants.Message_Error_Null_PR)
                {
                    reply += $" {prLink}";
                }
                else if (!isPrMerged)
                {
                    IReadOnlyList<Label> labels = await GetPrLabels(org, repo, prNum);

                    // check breaking change
                    (bool completeBcReview, reply, action) = CheckBreakingChangeReview(labels);
                    if (completeBcReview || reply == "")
                    {
                        // sdk breaking change review
                        if (!labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_Go))
                        {
                            reply = Constants.Message_SDKBreakingChangeReview_NotNeeded;
                        }
                        else if (labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_Go) && !labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_Go_Approval))
                        {
                            (bool completeArmReview, reply, action) = CheckArmReview(labels);
                            if (!completeArmReview && reply != "")
                            {
                                // arm review isn't finished
                                reply = Constants.Message_ArmReview_NotFinished_BeforeSdkBreakingChangeReview;
                                action = Constants.Action_ArmReview_NotFinished_BeforeSdkBreakingChangeReview;
                            }
                            else
                            {
                                reply = Constants.Message_SDKBreakingChangeGo_NotFinished;
                                action = Constants.Action_SDKBreakingChangeGo_NotFinished;
                            }
                        }
                        else
                        {
                            reply = Constants.Message_GoSdkReview_Finished;
                            action = Constants.Action_BreakingChangeReview_Finished;
                        }
                    }
                }
                // add support message for tool failures
                reply = AddSupportMessageForTools(turnContext, reply);
            }

            // call QA function
            (string additionalAnswer, List<string>? relevancies) = await _questionAnsweringActions.QuestionAnsweringHandler(turnContext, turnState);

            // get the relevance links based on user input or PR labels to differenciate the mgmt plane and data plane
            IList<string> references = await GetRelevanceLinks(turnContext, org, repo, prNum);
            Attachment card;
            if (additionalAnswer.StartsWith("Sorry, I do not know"))
            {
                additionalAnswer = "";
                relevancies = null;
            }
            card = await CardBuilder.NewPRAndQAAttachment(query, reply, action, additionalAnswer, references, relevancies, CancellationToken.None);

            try
            {
                await turnContext.SendActivityAsync(MessageFactory.Attachment(card), CancellationToken.None);
            }
            catch (Exception ex)
            {
                await turnContext.TraceActivityAsync("SendActivityError", ex, "The bot received an error when send message to Teams.");
                Console.WriteLine("Error:" + ex);
                this._logger.LogError($"PrBreakingChangeReview-Go: PR link:{prLink}. Query:{query}. Error: {ex}");
            }

            // End the current chain
            return false;
        }

        [Action("PrBreakingChangeReview-Python")]
        public async Task<bool> PullRequestPythonSdkBreakingChangeReview([ActionTurnContext] ITurnContext turnContext, [ActionTurnState] AppState turnState, [ActionEntities] Dictionary<string, object> entities)
        {
            if (turnContext == null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            if (turnState == null)
            {
                throw new ArgumentNullException(nameof(turnState));
            }

            string prLink = (string)entities["prLink"];
            if (string.IsNullOrEmpty(prLink) || !prLink.Contains("https://"))
            {
                await _questionAnsweringActions.QuestionAnswering(turnContext, turnState);
                return false;
            }

            // redact query to less than 60 characters
            string query = turnContext.Activity.Text;
            if (query.Length > 60)
            {
                query = turnContext.Activity.Text.Substring(0, 57) + "...";
            }

            this._logger.LogInformation($"PrBreakingChangeReview-Python: PR link:{prLink}. Query: {query}");
            (string org, string repo, int prNum) = GetPrInfoFromPrLink(prLink);
            string reply = "";
            string action = "";

            // Get pull request info
            if (prNum != 0)
            {
                (bool isPrMerged, reply) = await IsPrMerged(org, repo, prNum);
                if (reply == Constants.Message_Error_Null_PR)
                {
                    reply += $" {prLink}";
                }
                else if (!isPrMerged)
                {
                    IReadOnlyList<Label> labels = await GetPrLabels(org, repo, prNum);

                    // check breaking change
                    (bool completeBcReview, reply, action) = CheckBreakingChangeReview(labels);
                    if (completeBcReview || reply == "")
                    {
                        // sdk breaking change review
                        if (!labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_Python))
                        {
                            reply = Constants.Message_SDKBreakingChangeReview_NotNeeded;
                        }
                        else if (labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_Python || x.Name == Constants.Label_SDKBreakingChange_PythonTrack2) && !labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_Python_Approval))
                        {
                            (bool completeArmReview, reply, action) = CheckArmReview(labels);
                            if (!completeArmReview && reply != "")
                            {
                                // arm review isn't finished
                                reply = Constants.Message_ArmReview_NotFinished_BeforeSdkBreakingChangeReview;
                                action = Constants.Action_ArmReview_NotFinished_BeforeSdkBreakingChangeReview;
                            }
                            else
                            {
                                reply = Constants.Message_SDKBreakingChangePython_NotFinished;
                                action = Constants.Action_SDKBreakingChangePython_NotFinished;
                            }
                        }
                        else
                        {
                            reply = Constants.Message_PythonSdkReview_Finished;
                            action = Constants.Action_BreakingChangeReview_Finished;
                        }
                    }
                }
                // add support message for tool failures
                reply = AddSupportMessageForTools(turnContext, reply);
            }

            // call QA function
            (string additionalAnswer, List<string>? relevancies) = await _questionAnsweringActions.QuestionAnsweringHandler(turnContext, turnState);

            // get the relevance links based on user input or PR labels to differenciate the mgmt plane and data plane
            IList<string> references = await GetRelevanceLinks(turnContext, org, repo, prNum);
            Attachment card;
            if (additionalAnswer.StartsWith("Sorry, I do not know"))
            {
                additionalAnswer = "";
                relevancies = null;
            }
            card = await CardBuilder.NewPRAndQAAttachment(query, reply, action, additionalAnswer, references, relevancies, CancellationToken.None);

            try
            {
                await turnContext.SendActivityAsync(MessageFactory.Attachment(card), CancellationToken.None);
            }
            catch (Exception ex)
            {
                await turnContext.TraceActivityAsync("SendActivityError", ex, "The bot received an error when send message to Teams.");
                Console.WriteLine("Error:" + ex);
                this._logger.LogError($"PrBreakingChangeReview-Python: PR link:{prLink}. Query:{query}. Error: {ex}");
            }

            // End the current chain
            return false;
        }

        [Action("PrBreakingChangeReview-JS")]
        public async Task<bool> PullRequestJSSdkBreakingChangeReview([ActionTurnContext] ITurnContext turnContext, [ActionTurnState] AppState turnState, [ActionEntities] Dictionary<string, object> entities)
        {
            if (turnContext == null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            if (turnState == null)
            {
                throw new ArgumentNullException(nameof(turnState));
            }

            string prLink = (string)entities["prLink"];
            if (string.IsNullOrEmpty(prLink) || !prLink.Contains("https://"))
            {
                await _questionAnsweringActions.QuestionAnswering(turnContext, turnState);
                return false;
            }

            // redact query to less than 60 characters
            string query = turnContext.Activity.Text;
            if (query.Length > 60)
            {
                query = turnContext.Activity.Text.Substring(0, 57) + "...";
            }

            this._logger.LogInformation($"PrBreakingChangeReview-JS: PR link:{prLink}. Query: {query}");
            (string org, string repo, int prNum) = GetPrInfoFromPrLink(prLink);
            string reply = "";
            string action = "";

            // Get pull request info
            if (prNum != 0)
            {
                (bool isPrMerged, reply) = await IsPrMerged(org, repo, prNum);
                if (reply == Constants.Message_Error_Null_PR)
                {
                    reply += $" {prLink}";
                }
                else if (!isPrMerged)
                {
                    IReadOnlyList<Label> labels = await GetPrLabels(org, repo, prNum);

                    // check breaking change
                    (bool completeBcReview, reply, action) = CheckBreakingChangeReview(labels);
                    if (completeBcReview || reply == "")
                    {
                        // sdk breaking change review
                        if (!labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_JavaScript))
                        {
                            reply = Constants.Message_SDKBreakingChangeReview_NotNeeded;
                        }
                        else if (labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_JavaScript) && !labels.Any(x => x.Name == Constants.Label_SDKBreakingChange_JavaScript_Approval))
                        {
                            (bool completeArmReview, reply, action) = CheckArmReview(labels);
                            if (!completeArmReview && reply != "")
                            {
                                // arm review isn't finished
                                reply = Constants.Message_ArmReview_NotFinished_BeforeSdkBreakingChangeReview;
                                action = Constants.Action_ArmReview_NotFinished_BeforeSdkBreakingChangeReview;
                            }
                            else
                            {
                                reply = Constants.Message_SDKBreakingChangeJavaScript_NotFinished;
                                action = Constants.Action_SDKBreakingChangeJavaScript_NotFinished;
                            }
                        }
                        else
                        {
                            reply = Constants.Message_JavaScriptSdkReview_Finished;
                            action = Constants.Action_BreakingChangeReview_Finished;
                        }
                    }
                }
                // add support message for tool failures
                reply = AddSupportMessageForTools(turnContext, reply);
            }

            // call QA function
            (string additionalAnswer, List<string>? relevancies) = await _questionAnsweringActions.QuestionAnsweringHandler(turnContext, turnState);

            // get the relevance links based on user input or PR labels to differenciate the mgmt plane and data plane
            IList<string> references = await GetRelevanceLinks(turnContext, org, repo, prNum);
            Attachment card;
            if (additionalAnswer.StartsWith("Sorry, I do not know"))
            {
                additionalAnswer = "";
                relevancies = null;
            }
            card = await CardBuilder.NewPRAndQAAttachment(query, reply, action, additionalAnswer, references, relevancies, CancellationToken.None);

            try
            {
                await turnContext.SendActivityAsync(MessageFactory.Attachment(card), CancellationToken.None);
            }
            catch (Exception ex)
            {
                await turnContext.TraceActivityAsync("SendActivityError", ex, "The bot received an error when send message to Teams.");
                Console.WriteLine("Error:" + ex);
                this._logger.LogError($"PrBreakingChangeReview-JS: PR link:{prLink}. Query:{query}. Error: {ex}");
            }

            // End the current chain
            return false;
        }

        private static (string org, string repo, int prNum) GetPrInfoFromQuery(string query)
        {
            string org = "";
            string repo = "";
            int prNum = 0;
            if (!string.IsNullOrEmpty(query))
            {
                string pattern = @"(https?://[\w.-]+)";
                Match match = Regex.Match(query, pattern);
                if (match.Success)
                {
                    string url = match.Groups[1].Value;
                    Console.WriteLine(url);
                    pattern = @"https://github.com/(?<org>[^/]+)/(?<repo>[^/]+)/pull/(?<pr>\d+)";
                    match = Regex.Match(url, pattern);
                    if (match.Success)
                    {
                        org = match.Groups["org"].Value;
                        repo = match.Groups["repo"].Value;
                        prNum = int.TryParse(match.Groups["pr"].Value, out int pr) ? pr : 0;
                    }
                }
            }

            return (org, repo, prNum);
        }

        private static (string org, string repo, int prNum) GetPrInfoFromPrLink(string prLink)
        {
            string org = "";
            string repo = "";
            int prNum = 0;
            if (!string.IsNullOrEmpty(prLink))
            {
                string pattern = @"https://github.com/(?<org>[^/]+)/(?<repo>[^/]+)/pull/(?<pr>\d+)";
                Match match = Regex.Match(prLink, pattern);
                if (match.Success)
                {
                    org = match.Groups["org"].Value;
                    repo = match.Groups["repo"].Value;
                    prNum = int.TryParse(match.Groups["pr"].Value, out int pr) ? pr : 0;
                }
            }

            return (org, repo, prNum);
        }

        private static (bool isComplete, string reply, string action) CheckArmReview(IReadOnlyList<Label> labels)
        {
            string result = "";
            string action = "";
            bool isComplete = false;

            if (labels.Any(x => x.Name == "ARMReview"))
            {
                if (labels.Any(x => x.Name == "WaitForARMFeedback") && !labels.Any(x => x.Name == "ARMSignedOff"))
                {
                    result = "This pull request has been labeled with 'WaitForARMFeedback' and is currently in the ARM review queue. The on-call ARM reviewer will review it automatically.";
                    action = "Wait for the ARM reviewer's feedback.";
                }
                else if (labels.Any(x => x.Name == "ARMChangesRequested"))
                {
                    result = "There is blocking feedback in this pull request. You will need to address these issues before the ARM reviewer can re-evaluate the changes.";
                    action = "Address the review feedback then remove 'ARMChangesRequested' label.";
                }
                else if (labels.Any(x => x.Name == "ARMSignedOff"))
                {
                    isComplete = true;
                }
            }
            return (isComplete, result, action);
        }

        private static (bool isComplete, string reply, string action) CheckBreakingChangeReview(IReadOnlyList<Label> labels)
        {
            string result = "";
            string action = "";
            bool isComplete = false;

            if (labels.Any(x => x.Name == Constants.Label_APIBreakingChange || x.Name == Constants.Label_APINewApiVersionRequired))
            {
                if (!labels.Any(x => x.Name == Constants.Label_APIBreakingChangeApproval))
                {
                    result = "This pull request has been labeled with 'BreakingChangeReviewRequired' and need to get the breaking changes reviewed by the breaking change review board.";
                    action = Constants.Action_BreakingChangeReview;
                }
                else
                {
                    isComplete = true;
                }
            }
            return (isComplete, result, action);
        }

        private static (bool isComplete, string reply, string action) CheckSdkBreakingChangeReview(IReadOnlyList<Label> labels)
        {
            string result = "";
            string action = "";
            bool isComplete = false;

            if (labels.Any(x => x.Name == "CI-BreakingChange-Go" || x.Name == "CI-BreakingChange-Python" || x.Name == "CI-BreakingChange-Python-Track2" || x.Name == "CI-BreakingChange-JavaScript"))
            {
                if ((labels.Any(x => x.Name == "CI-BreakingChange-Go") && !labels.Any(x => x.Name == "Approved-SdkBreakingChange-Go"))
                    || (labels.Any(x => x.Name == "CI-BreakingChange-Python" || x.Name == "CI-BreakingChange-Python-Track2") && !labels.Any(x => x.Name == "Approved-SdkBreakingChange-Python"))
                    || (labels.Any(x => x.Name == "CI-BreakingChange-JavaScript") && !labels.Any(x => x.Name == "Approved-SdkBreakingChange-JavaScript")))
                {
                    result = "This pull request is flagged with at least one label prefixed with 'CI-BreakingChange-', you must get an approval for these breaking changes.";
                    action = "No action needs to take for SDK breaking change review. This PR is in the SDK breaking change review queue and the expected time of review completion is two business days.";
                }
                else
                {
                    isComplete = true;
                }
            }
            return (isComplete, result, action);
        }

        private async Task<IReadOnlyList<Label>> GetPrLabels(string org, string repo, int prNum)
        {
            if (_labels == null)
            {
                // Get the labels of the pull request
                _labels = await _gitHubClient.Issue.Labels.GetAllForIssue(org, repo, prNum);
            }

            return _labels;

        }

        private async Task<IOrderedEnumerable<CheckRun>> GetCheckRuns(string org, string repo, int prNum)
        {
            PullRequest pr = await GetPullRequest(org, repo, prNum);

            // Get check runs and validate the requirement check is success
            var checkRuns = await _gitHubClient.Check.Run.GetAllForReference(org, repo, pr.Head.Sha);

            return checkRuns.CheckRuns.OrderByDescending(cr => cr.CompletedAt);

        }

        private async Task<(bool, string)> IsPrMerged(string org, string repo, int prNum)
        {
            bool isMerged = false;
            string reply = "";
            PullRequest pr = await GetPullRequest(org, repo, prNum);

            if (pr == null)
            {
                reply = Constants.Message_Error_Null_PR;
            }
            else if (pr.Merged || pr.State == "Closed")
            {
                isMerged = true;
                reply = Constants.Message_PrIsMerged;
            }

            return (isMerged, reply);
        }

        private async Task<(string, string)> CheckPrStatus(string org, string repo, int prNum)
        {
            string result = "";
            string action = "";

            // Get the labels of the pull request
            var labels = await GetPrLabels(org, repo, prNum);

            // 1. breaking change review
            (bool completeBcReview, string reply, action) = CheckBreakingChangeReview(labels);
            if (!completeBcReview && reply != "")
            {
                result = reply;
            }
            else
            {
                if (labels.Any(x => x.Name == Constants.Label_ResourceManager))
                {
                    // 2. arm review
                    (bool completeArmReview, reply, action) = CheckArmReview(labels);
                    if (!completeArmReview)
                    {
                        result = reply;
                    }
                    else
                    {
                        // 3. sdk breaking change review
                        (bool completesdkBcReview, reply, action) = CheckSdkBreakingChangeReview(labels);
                        if (!completesdkBcReview && reply != "")
                        {
                            result = reply;
                        }
                    }
                }
            }

            // 4. required CI checks
            (string checkResult, string checkAction) = await CheckRequiredCIChecks(org, repo, prNum);
            if (checkResult != "")
            {
                // append the result and action
                result = result + checkResult;
                action = action + checkAction;
            }

            if (result == "" && action == "")
            {
                result = Constants.Message_Review_Finished;
                if (labels.Any(x => x.Name == Constants.Label_DataPlane))
                {
                    action = Constants.Action_RequestMerge_DataPlane;
                }
                else
                {
                    action = Constants.Action_RequestMerge_MgmtPlane;
                }
            }

            return (result, action);
        }

        private async Task<PullRequest> GetPullRequest(string org, string repo, int prNum)
        {
            if (_pullRequest == null)
            {
                try
                {
                    _pullRequest = await _gitHubClient.PullRequest.Get(org, repo, prNum);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"GetPullRequest with org:{org}, repo:{repo}, pull request number:{prNum}. Errors:{ex}");
                    this._logger.LogError($"GetPullRequest: org:{org}, repo:{repo}, pull request number:{prNum}  Error: {ex}");
                }
            }
            return _pullRequest;
        }

        private async Task<(string, string)> CheckRequiredCIChecks(string org, string repo, int prNum)
        {
            string result = "";
            string action = "";

            // Get check runs
            IOrderedEnumerable<CheckRun> checkRuns = await GetCheckRuns(org, repo, prNum);

            foreach (CheckRun checkRun in checkRuns)
            {
                if (checkRun.Name == Constants.CheckName_MergeRequirement)
                {
                    if (checkRun.Conclusion == "Failure")
                    {
                        result = $"The CI check for '{Constants.CheckName_MergeRequirement}' does not succeed, it must be passed to proceed the merge.";
                        action = $"Please add the comment '/azp run' to the PR to trigger a re-run of the pipeline. If the re-run still doesn't work, please reach out to **Konrad Jamrozik** for assistance.";
                        if (checkRun.Output.Text.Contains("required checks are failing"))
                        {
                            result = "This PR has required CI check failures, it must be fixed to proceed the merge.";
                            action = $"In addition, please see [the check result]({checkRun.HtmlUrl}) to fix the required check failures.";
                        }
                    }
                    else if (checkRun.Conclusion == null)
                    {
                        result = $"The CI check for '{Constants.CheckName_MergeRequirement}' is not completing. You can view [the results page]({checkRun.HtmlUrl}).";
                        action = $"Please add the comment '/azp run' to the PR to trigger a re-run of the pipeline. If the re-run still doesn't work, please reach out to **Konrad Jamrozik** for assistance.";
                    }
                    break;
                }
            }
            return (result, action);
        }

        private async Task<(string, string)> DoPrMerge(string org, string repo, int prNum)
        {
            bool readyForMerge = false;

            // Get check runs and validate the requirement check is success
            IOrderedEnumerable<CheckRun> checkRuns = await GetCheckRuns(org, repo, prNum);

            string result = "Sorry, the pull request cannot be merged due to failing the automated merging requirements.";
            string action = $"Please check the '{Constants.CheckName_MergeRequirement}' check result for details.";

            foreach (CheckRun checkRun in checkRuns)
            {
                if (checkRun.Name == Constants.CheckName_MergeRequirement)
                {
                    if (checkRun.Conclusion == "success")
                    {
                        readyForMerge = true;
                    }
                    else
                    {
                        result = checkRun.Output.Title;
                        action = $"For more information, please see [the check result]({checkRun.HtmlUrl}).";
                    }
                    break;
                }
            }

            // Check the label doesn't have 'DONOTMERGE'
            if (readyForMerge)
            {
                // Do the PR merge - comment out below merge operation in order to ignore real merge when validate with real user queries.
                /*var mergeResult = await _gitHubClient.PullRequest.Merge(org, repo, prNum, new MergePullRequest());

                if (mergeResult.Merged)
                {
                    result = "I'm pleased to inform you that this pull request has been successfully merged.";
                    action = "Cheer!";
                }
                else
                {
                    result = $"I'm sorry to inform you that this pull request cannot be merged at this time. The reason is: {mergeResult.Message}";
                    action = "Retry";
                }*/
                result = "Testing phase - ignore merge while validate with real user queries!";
            }
            return (result, action);
        }

        public static string AddSupportMessageForTools(ITurnContext turnContext, string reply)
        {
            string query = turnContext.Activity.Text;
            if (!string.IsNullOrEmpty(query))
            {
                query = query.ToLower();
                if (query.Contains("avocado"))
                {
                    reply += Constants.Message_FurtherHelp_Avocado;
                }
                else if (query.Contains("lintdiff") || query.Contains("lint diff") || query.Contains("lintrpaas"))
                {
                    reply += Constants.Message_FurtherHelp_LintTool;
                }
                else if (query.Contains("modelvalidation") || query.Contains("model validation") || query.Contains("semanticvalidation") || query.Contains("semantic validation"))
                {
                    reply += Constants.Message_FurtherHelp_Oav;
                }
                else if (query.Contains("breaking change tool") || query.Contains("openapi diff") || query.Contains("breaking change cross version"))
                {
                    reply += Constants.Message_FurtherHelp_Oad;
                }
                else if (query.Contains("typespec validation") || query.Contains("type spec validation"))
                {
                    reply += Constants.Message_FurtherHelp_TypeSpecValidation;
                }
                else if (query.Contains("apidocpreview") || query.Contains("api doc preview"))
                {
                    reply += Constants.Message_FurtherHelp_ApiDocPreview;
                }
                else if (query.Contains("apiview") || query.Contains("api view"))
                {
                    reply += Constants.Message_FurtherHelp_ApiView;
                }
                else if (query.Contains("azure-resource-manager-schemas"))
                {
                    reply += Constants.Message_FurtherHelp_ApiView;
                }
                else if (query.Contains("azure-powershell"))
                {
                    reply += Constants.Message_FurtherHelp_Powershell;
                }
                else if (query.Contains("azure-sdk-for-go") || query.Contains("go sdk"))
                {
                    reply += Constants.Message_FurtherHelp_GoSdk;
                }
                else if (query.Contains("azure-sdk-for-python") || query.Contains("azure-sdk-for-python-track2") || query.Contains("python sdk"))
                {
                    reply += Constants.Message_FurtherHelp_PythonSdk;
                }
                else if (query.Contains("azure-sdk-for-java") || query.Contains("java sdk"))
                {
                    reply += Constants.Message_FurtherHelp_JavaSdk;
                }
                else if (query.Contains("azure-sdk-for-js") || query.Contains("js sdk"))
                {
                    reply += Constants.Message_FurtherHelp_JsSdk;
                }
                else if (query.Contains("azure-sdk-for-net-track2") || query.Contains("azure-sdk-for-net") || query.Contains("dotnet sdk") || query.Contains(".net sdk"))
                {
                    reply += Constants.Message_FurtherHelp_DotnetSdk;
                }
            }
            return reply;
        }

        private async Task<IList<string>> GetRelevanceLinks(ITurnContext turnContext, string org = "", string repo = "", int prNum = 0)
        {
            // default value set to mgmt plane relevant links
            IList<string> relevanceLinks = _referencesMgmtPlane;
            if (!string.IsNullOrEmpty(org) && !string.IsNullOrEmpty(repo) && prNum != 0)
            {
                IReadOnlyList<Label> labels = await GetPrLabels(org, repo, prNum);
                if (labels.Any(x => x.Name == Constants.Label_DataPlane))
                {
                    // check labels in the PR
                    relevanceLinks = _referencesDataPlane;
                }
            }
            else
            {
                // check user input
                string input = GetUserQueryFromContext(turnContext).ToLower();
                if (input.Contains("data plane") || input.Contains("data-plane"))
                {
                    relevanceLinks = _referencesDataPlane;
                }
            }

            return relevanceLinks;
        }

        public static string GetUserQueryFromContext(ITurnContext turnContext)
        {
            string input = turnContext.Activity.Text; ;
            if (turnContext.Activity.Attachments.Count > 0)
            {
                foreach (Attachment? attachment in turnContext.Activity.Attachments)
                {
                    if (attachment.ContentType == "text/html")
                    {
                        input = (string)attachment.Content;
                        break;
                    }
                }
            }
            return input;
        }
    }
}
