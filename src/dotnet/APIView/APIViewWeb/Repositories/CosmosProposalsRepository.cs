using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class CosmosProposalsRepository : ICosmosProposalsRepository
    {
        private readonly Container _commentsContainer;

        public CosmosProposalsRepository(IConfiguration configuration, CosmosClient cosmosClient)
        {
            // Reuse the Comments container — proposals are stored alongside comments
            // but distinguished by the Type = "Proposal" discriminator field.
            _commentsContainer = cosmosClient.GetContainer(configuration["CosmosDBName"], "Comments");
        }

        public async Task UpsertProposalAsync(CrossLanguageProposalModel proposal)
        {
            await _commentsContainer.UpsertItemAsync(proposal, new PartitionKey(proposal.ReviewId));
        }

        public async Task<CrossLanguageProposalModel> GetProposalAsync(string reviewId, string proposalId)
        {
            return await _commentsContainer.ReadItemAsync<CrossLanguageProposalModel>(proposalId, new PartitionKey(reviewId));
        }

        public async Task<IEnumerable<CrossLanguageProposalModel>> GetProposalsByCrossLanguageIdAsync(string crossLanguageId)
        {
            var queryDefinition = new QueryDefinition(
                "SELECT * FROM c WHERE c.CrossLanguageId = @crossLanguageId AND c.Type = 'Proposal' AND (NOT IS_DEFINED(c.IsDeleted) OR c.IsDeleted = false)")
                .WithParameter("@crossLanguageId", crossLanguageId);

            var proposals = new List<CrossLanguageProposalModel>();
            var iterator = _commentsContainer.GetItemQueryIterator<CrossLanguageProposalModel>(queryDefinition,
                requestOptions: new QueryRequestOptions { MaxConcurrency = -1 });
            while (iterator.HasMoreResults)
            {
                var result = await iterator.ReadNextAsync();
                proposals.AddRange(result.Resource);
            }
            return proposals;
        }

        public async Task<IEnumerable<CrossLanguageProposalModel>> GetProposalsByReviewIdAsync(string reviewId)
        {
            var queryDefinition = new QueryDefinition(
                "SELECT * FROM c WHERE c.ReviewId = @reviewId AND c.Type = 'Proposal' AND (NOT IS_DEFINED(c.IsDeleted) OR c.IsDeleted = false)")
                .WithParameter("@reviewId", reviewId);

            var proposals = new List<CrossLanguageProposalModel>();
            var iterator = _commentsContainer.GetItemQueryIterator<CrossLanguageProposalModel>(queryDefinition,
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(reviewId) });
            while (iterator.HasMoreResults)
            {
                var result = await iterator.ReadNextAsync();
                proposals.AddRange(result.Resource);
            }
            return proposals;
        }
    }
}
