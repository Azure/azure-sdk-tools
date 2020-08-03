using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Models;
using APIViewWeb.Respositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb.Pages.Assemblies
{
    public class SummaryModel : PageModel
    {
        private readonly CommentsManager _commentsManager;
        private readonly ReviewManager _reviewManager;
        private const string ENDPOINT_SETTING = "Endpoint";

        public string Endpoint { get; }
        public ReviewModel Review { get; private set; }
        public ReviewCommentsModel Comments { get; private set; }

        public IOrderedEnumerable<KeyValuePair<ReviewRevisionModel, List<CommentThreadModel>>> UnresolvedThreads { get; set; }
        public IOrderedEnumerable<KeyValuePair<ReviewRevisionModel, List<CommentThreadModel>>> ResolvedThreads { get; set; }

        public SummaryModel(
            IConfiguration configuration,
            CommentsManager commentsManager,
            ReviewManager reviewManager)
        {
            _commentsManager = commentsManager;
            _reviewManager = reviewManager;
            Endpoint = configuration.GetValue<string>(ENDPOINT_SETTING);
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            TempData["Page"] = "summary";
            Review = await _reviewManager.GetReviewAsync(User, id);
            Comments = await _commentsManager.GetReviewCommentsAsync(id);
            UnresolvedThreads = ParseThreads(Comments.Threads.Where(t => !t.IsResolved));
            ResolvedThreads = ParseThreads(Comments.Threads.Where(t => t.IsResolved));
            return Page();
        }

        private IOrderedEnumerable<KeyValuePair<ReviewRevisionModel, List<CommentThreadModel>>> ParseThreads(IEnumerable<CommentThreadModel> threads)
        {
            var threadDict = new Dictionary<ReviewRevisionModel, List<CommentThreadModel>>();

            foreach (var thread in threads)
            {
                ReviewRevisionModel lastRevisionForThread = null;
                int lastRevision = 0;
                foreach (var comment in thread.Comments)
                {
                    ReviewRevisionModel commentRevision = Review.Revisions.Single(r => r.RevisionId == comment.RevisionId);
                    var commentRevisionIndex = commentRevision.RevisionNumber;
                    // Group each thread under the last revision where a comment was added for it. 
                    if (commentRevisionIndex >= lastRevision)
                    {
                        lastRevision = commentRevisionIndex;
                        lastRevisionForThread = commentRevision;
                    }
                }
                if (!threadDict.ContainsKey(lastRevisionForThread))
                {
                    threadDict.Add(lastRevisionForThread, new List<CommentThreadModel>());
                }
                threadDict[lastRevisionForThread].Add(thread);
            }
            return threadDict.OrderBy(kvp => Review.Revisions.IndexOf(kvp.Key));
        }
    }
}