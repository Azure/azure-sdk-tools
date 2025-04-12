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

        public string BlobAccountUri => GetItem("BlobAccountUri");
        public string BlobContainerName => GetItem("BlobContainerName");
        public string CommonModelRepositoryName => GetItem("CommonModelRepositoryName");
        public string ConfidenceThreshold => GetItem("ConfidenceThreshold");
        public string DocumentIndexName => GetItem("DocumentIndexName");
        public string DocumentSemanticName => GetItem("DocumentSemanticName");
        public string EnableAnswers => GetItem("EnableAnswers");
        public string Instructions => GetItem("Instructions");
        public string IssueIndexName => GetItem("IssueIndexName");
        public string IssueSemanticName => GetItem("IssueSemanticName");
        public string LabelPredictor => GetItem("labelPredictor");
        public string OpenAIEndpoint => GetItem("OpenAIEndpoint");
        public string OpenAIModelName => GetItem("OpenAIModelName");
        public string RepoNames => GetItem("RepoNames");
        public string RepoOwner => GetItem("RepoOwner");
        public string ReposUsingCommonModel => GetItem("ReposUsingCommonModel");
        public string ScoreThreshold => GetItem("ScoreThreshold");
        public string SearchEndpoint => GetItem("SearchEndpoint");
        public string SolutionThreshold => GetItem("SolutionThreshold");
        public string SourceCount => GetItem("SourceCount");
        public string IssueModelAzureSdkForJavaBlobConfigNames => GetItem("IssueModel.azure_sdk_for_java.BlobConfigNames");
        public string IssueModelAzureSdkForJavaCategoryLabels => GetItem("IssueModel.azure_sdk_for_java.CategoryLabels");
        public string IssueModelAzureSdkForJavaServiceLabels => GetItem("IssueModel.azure_sdk_for_java.ServiceLabels");
        public string IssueModelAzureSdkForNetBlobConfigNames => GetItem("IssueModel.azure_sdk_for_net.BlobConfigNames");
        public string IssueModelAzureSdkForNetCategoryLabels => GetItem("IssueModel.azure_sdk_for_net.CategoryLabels");
        public string IssueModelAzureSdkForNetServiceLabels => GetItem("IssueModel.azure_sdk_for_net.ServiceLabels");
        public string IssueModelAzureSdkBlobConfigNames => GetItem("IssueModel.azure_sdk.BlobConfigNames");
        public string IssueModelAzureSdkCategoryLabels => GetItem("IssueModel.azure_sdk.CategoryLabels");
        public string IssueModelAzureSdkServiceLabels => GetItem("IssueModel.azure_sdk.ServiceLabels");
        public string AnswerService => GetItem("AnswerService");
        public string IssueIndexFieldName => GetItem("IssueIndexFieldName");
        public string DocumentIndexFieldName => GetItem("DocumentIndexFieldName");
        public string SuggestionResponseIntroduction => GetItem("SuggestionResponseIntroduction");
        public string SuggestionResponseConclusion => GetItem("SuggestionResponseConclusion");
        public string SolutionResponseIntroduction => GetItem("SolutionResponseIntroduction");
        public string SolutionResponseConclusion => GetItem("SolutionResponseConclusion");

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
