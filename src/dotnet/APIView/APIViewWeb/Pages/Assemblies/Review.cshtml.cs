using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace APIViewWeb.Pages.Assemblies
{
    public class ReviewPageModel : PageModel
    {
        private readonly BlobCodeFileRepository _codeFileRepository;

        private readonly CosmosReviewRepository _cosmosReviewRepository;

        private readonly BlobOriginalsRepository _originalsRepository;

        private readonly CosmosCommentsRepository _commentRepository;

        public ReviewPageModel(BlobCodeFileRepository codeFileRepository,
            CosmosReviewRepository cosmosReviewRepository,
            BlobOriginalsRepository originalsRepository,
            CosmosCommentsRepository commentRepository)
        {
            _codeFileRepository = codeFileRepository;
            _cosmosReviewRepository = cosmosReviewRepository;
            _originalsRepository = originalsRepository;
            this._commentRepository = commentRepository;
        }

        public string Id { get; set; }
        public LineApiView[] Lines { get; set; }

        public ReviewModel Review { get; set; }
        public ReviewCodeFileModel ReviewCodeFile { get; set; }
        public CodeFile CodeFile { get; set; }

        public bool UpdateAvailable => CodeFile != null &&
                                       ReviewCodeFile.HasOriginal &&
                                       CodeFile.Version != CodeFile.CurrentVersion &&
                                       Review.Author == User.GetGitHubLogin();
        [BindProperty]
        public CommentModel Comment { get; set; }
        public Dictionary<string, List<CommentModel>> Comments { get; set; }
        public string Username { get; set; }

        public async Task<ActionResult> OnPostDeleteAsync(string id, string commentId, string elementId)
        {
            var comment = await _commentRepository.GetCommentAsync(id, commentId);
            await _commentRepository.DeleteCommentAsync(comment);

            return await CommentPartialAsync(id, elementId);
        }

        private async Task<ActionResult> CommentPartialAsync(string id, string elementId)
        {
            var commentArray = await _commentRepository.GetCommentsAsync(id);
            List<CommentModel> comments = commentArray.Where(c => c.ElementId == elementId).ToList();

            CommentThreadModel partialModel = new CommentThreadModel()
            {
                AssemblyId = id,
                Comments = comments,
                LineId = Comment.ElementId
            };

            return new PartialViewResult
            {
                ViewName = "_CommentThreadPartial",
                ViewData = new ViewDataDictionary<CommentThreadModel>(ViewData, partialModel)
            };
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            Id = id;
            Review = await _cosmosReviewRepository.GetReviewAsync(id);
            ReviewCodeFile = Review.Files.SingleOrDefault();
            if (ReviewCodeFile != null)
            {
                CodeFile = await _codeFileRepository.GetCodeFileAsync(ReviewCodeFile.ReviewFileId);
            }
            else
            {
                return RedirectToPage("LegacyReview", new { id = id });
            }

            Lines = new CodeFileHtmlRenderer().Render(CodeFile).ToArray();
            Comments = new Dictionary<string, List<CommentModel>>();

            var assemblyComments = await _commentRepository.GetCommentsAsync(id);
            var comments = assemblyComments;

            foreach (var comment in comments)
            {
                if (!Comments.TryGetValue(comment.ElementId, out _))
                    Comments[comment.ElementId] = new List<CommentModel>() { comment };
                else
                    Comments[comment.ElementId].Add(comment);
            }

            Username = User.GetGitHubLogin();
            return Page();
        }

        public async Task<ActionResult> OnPostAsync(string id)
        {
            Comment.TimeStamp = DateTime.UtcNow;
            Comment.Username = User.GetGitHubLogin();
            Comment.ReviewId = id;

            await _commentRepository.UpsertCommentAsync(Comment);

            return await CommentPartialAsync(id, Comment.ElementId);
        }

        public async Task<ActionResult> OnPostRefreshModelAsync(string id)
        {
            var review = await _cosmosReviewRepository.GetReviewAsync(id);
            foreach (var file in review.Files)
            {
                if (!file.HasOriginal)
                {
                    continue;
                }

                var fileOriginal = await _originalsRepository.GetOriginalAsync(file.ReviewFileId);

                var codeFile = ApiView.CodeFileBuilder.Build(fileOriginal, file.RunAnalysis);
                await _codeFileRepository.UpsertCodeFileAsync(file.ReviewFileId, codeFile);
            }
            return RedirectToPage(new { id = id });
        }
    }
}
