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
    public class ConversationModel : PageModel
    {
        private readonly CommentsManager _commentsManager;
        private readonly ReviewManager _reviewManager;
        private const string ENDPOINT_SETTING = "Endpoint";
        private readonly BlobCodeFileRepository _codeFileRepository;

        public string Endpoint { get; }
        public ReviewModel Review { get; private set; }
        public ReviewCommentsModel Comments { get; private set; }

        public IOrderedEnumerable<KeyValuePair<ReviewRevisionModel, List<CommentThreadModel>>> Threads { get; set; }

        public ConversationModel(
            IConfiguration configuration,
            BlobCodeFileRepository codeFileRepository,
            CommentsManager commentsManager,
            ReviewManager reviewManager)
        {
            _codeFileRepository = codeFileRepository;
            _commentsManager = commentsManager;
            _reviewManager = reviewManager;
            Endpoint = configuration.GetValue<string>(ENDPOINT_SETTING);
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            TempData["Page"] = "conversation";
            Review = await _reviewManager.GetReviewAsync(User, id);
            Comments = await _commentsManager.GetReviewCommentsAsync(id);
            Threads = ParseThreads(Comments.Threads);
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
                    if (comment.RevisionId == null)
                    {
                        continue;
                    }
                    ReviewRevisionModel commentRevision = Review.Revisions.SingleOrDefault(r => r.RevisionId == comment.RevisionId);
                    if (commentRevision == null)
                    {
                        // if revision that comment was added in has been deleted
                        continue;
                    }
                    var commentRevisionIndex = commentRevision.RevisionNumber;
                    // Group each thread under the last revision where a comment was added for it. 
                    if (commentRevisionIndex >= lastRevision)
                    {
                        lastRevision = commentRevisionIndex;
                        lastRevisionForThread = commentRevision;
                    }
                }
                if (lastRevisionForThread == null)
                {
                    continue;
                }
                if (!threadDict.ContainsKey(lastRevisionForThread))
                {
                    threadDict.Add(lastRevisionForThread, new List<CommentThreadModel>());
                }
                threadDict[lastRevisionForThread].Add(thread);
            }
            return threadDict.OrderByDescending(kvp => Review.Revisions.IndexOf(kvp.Key));
        }
    }
}