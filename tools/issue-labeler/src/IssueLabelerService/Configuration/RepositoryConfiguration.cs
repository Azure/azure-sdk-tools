using Microsoft.Extensions.Configuration;

namespace IssueLabelerService
{
    public class RepositoryConfiguration
    {
        IConfiguration _config;
        string _repository;

        internal RepositoryConfiguration(IConfiguration config, string repository) =>
            (_config, _repository) = (config, repository);

        // Will always fall back on default
        internal RepositoryConfiguration(IConfiguration config) =>
            (_config, _repository) = (config, null);

        /// <summary>
        /// Exposes the underlying IConfiguration for use with components that need it.
        /// </summary>
        public IConfiguration Configuration => _config;

        public string BlobAccountUri => GetItem("BlobAccountUri");
        public string BlobContainerName => GetItem("BlobContainerName");
        public string CommonModelRepositoryName => GetItem("CommonModelRepositoryName");
        public string ConfidenceThreshold => GetItem("ConfidenceThreshold");
        public string EnableAnswers => GetItem("EnableAnswers");
        public string EnableLabels => GetItem("EnableLabels");
        public string IndexName => GetItem("IndexName");
        public string SemanticName => GetItem("SemanticName");
        public string Instructions => GetItem("Instructions");
        public string Prompt => GetItem("Prompt");
        public string LabelPredictor => GetItem("LabelPredictor");
        public string OpenAIEndpoint => GetItem("OpenAIEndpoint");
        public string RepoNames => GetItem("RepoNames");
        public string RepoOwner => GetItem("RepoOwner");
        public string ReposUsingCommonModel => GetItem("ReposUsingCommonModel");
        public string ScoreThreshold => GetItem("ScoreThreshold");
        public string SearchEndpoint => GetItem("SearchEndpoint");
        public string SolutionThreshold => GetItem("SolutionThreshold");
        public string SourceCount => GetItem("SourceCount");
        public string IssueModelForCategoryLabels => GetItem("IssueModelForCategoryLabels");
        public string IssueModelForServiceLabels => GetItem("IssueModelForServiceLabels");
        public string AnswerService => GetItem("AnswerService");
        public string IssueIndexFieldName => GetItem("IssueIndexFieldName");
        public string DocumentIndexFieldName => GetItem("DocumentIndexFieldName");
        public string SuggestionResponseIntroduction => GetItem("SuggestionResponseIntroduction");
        public string SuggestionResponseConclusion => GetItem("SuggestionResponseConclusion");
        public string SolutionResponseIntroduction => GetItem("SolutionResponseIntroduction");
        public string SolutionResponseConclusion => GetItem("SolutionResponseConclusion");
        public string AnswerModelName => GetItem("AnswerModelName");
        public string LabelModelName => GetItem("LabelModelName");
        public string SolutionInstructions => GetItem("SolutionInstructions");
        public string SuggestionInstructions => GetItem("SuggestionInstructions");
        public string SolutionUserPrompt => GetItem("SolutionUserPrompt");
        public string SuggestionUserPrompt => GetItem("SuggestionUserPrompt");
        public string LabelPrompt => GetItem("LabelPrompt");
        public string LabelInstructions => GetItem("LabelInstructions");
        public string LabelNames => GetItem("LabelNames");
        public string KnowledgeAgentName => GetItem("KnowledgeAgentName");
        public string KnowledgeAgentInstruction => GetItem("KnowledgeAgentInstruction");
        public string KnowledgeAgentMessage => GetItem("KnowledgeAgentMessage");
        public string KnowledgeAgentModelName => GetItem("KnowledgeAgentModelName");
        public string IssueGeneratorInstruction => GetItem("IssueGeneratorInstruction");
        public string IssueGeneratorMessage => GetItem("IssueGeneratorMessage");


        public string GetItem(string name)
        {
            if (string.IsNullOrEmpty(_repository))
            {
                return _config[$"defaults:{name}"];
            }

            return _config[$"{_repository}:{name}"] ??
                   _config[$"defaults:{name}"];
        }
    }
}
