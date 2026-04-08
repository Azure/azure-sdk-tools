using APIViewWeb.LeanModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIViewWeb.Repositories
{
    public interface ICosmosProposalsRepository
    {
        public Task UpsertProposalAsync(CrossLanguageProposalModel proposal);
        public Task<CrossLanguageProposalModel> GetProposalAsync(string reviewId, string proposalId);
        public Task<IEnumerable<CrossLanguageProposalModel>> GetProposalsByCrossLanguageIdAsync(string crossLanguageId);
        public Task<IEnumerable<CrossLanguageProposalModel>> GetProposalsByReviewIdAsync(string reviewId);
    }
}
