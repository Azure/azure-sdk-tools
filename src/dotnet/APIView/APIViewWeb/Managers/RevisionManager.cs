using APIViewWeb.LeanModels;
using APIViewWeb.Repositories;
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb.Managers
{
    public class RevisionManager : IRevisionManager
    {
        private readonly ICosmosReviewRepository _reviewsRepository;

        public RevisionManager(ICosmosReviewRepository reviewsRepository)
        {
            _reviewsRepository = reviewsRepository;
        }

        public async Task<IEnumerable<RevisionListItemModel>> GetRevisionsAsync(string reviewId)
        {
            return await _reviewsRepository.GetRevisionsAsync(reviewId);
        }

        public async Task<RevisionListItemModel> GetRevisionsAsync(string reviewId, string revisionId) 
        {
            var revisions = await _reviewsRepository.GetRevisionsAsync(reviewId);
            return revisions.FirstOrDefault(r => r.Id == revisionId);
        }

        public async Task<RevisionListItemModel> GetLatestRevisionsAsync(string reviewId)
        {
            var revisions = await _reviewsRepository.GetRevisionsAsync(reviewId);
            return revisions.OrderByDescending(r => DateTime.Parse(r.CreationDate)).FirstOrDefault();
        }
    }
}
