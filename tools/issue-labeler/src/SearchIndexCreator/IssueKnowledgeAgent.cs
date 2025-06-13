// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Configuration;
using Azure.Search.Documents.Indexes.Models;
using Azure;

namespace SearchIndexCreator
{
    public class IssueKnowledgeAgent
    {
        private readonly SearchIndexClient _indexClient;
        private readonly IConfiguration _config;

        public IssueKnowledgeAgent(SearchIndexClient indexClient, IConfiguration config)
        {
            _indexClient = indexClient;
            _config = config;
        }

        public async Task CreateOrUpdateAsync()
        {
            //create the knowledge agent
            Console.WriteLine("Creating/Updating the knowledge agent...");
            var IndexName = _config["IndexName"];
            var openAiParameters = new AzureOpenAIVectorizerParameters
            {
                ResourceUri = new Uri(_config["OpenAIEndpoint"]),
                DeploymentName = _config["KnowledgeAgentModelName"],
                ModelName = _config["KnowledgeAgentModelName"]
            };
            var agentModel = new KnowledgeAgentAzureOpenAIModel(azureOpenAIParameters: openAiParameters);
            var targetIndex = new KnowledgeAgentTargetIndex(IndexName)
            {
                DefaultRerankerThreshold = 1.0f
            };

            var agent = new KnowledgeAgent(
                name: _config["KnowledgeAgentName"],
                models: new[] { agentModel },
                targetIndexes: new[] { targetIndex }
            );
            await _indexClient.CreateOrUpdateKnowledgeAgentAsync(agent);
            Console.WriteLine($"Search agent '{_config["KnowledgeAgentName"]}' created or updated successfully");
        }

        public async Task<bool> KnowledgeAgentExistsAsync()
        {
            var agentName = _config["KnowledgeAgentName"];
            try
            {
                var agent = await _indexClient.GetKnowledgeAgentAsync(agentName);
                return agent != null;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
