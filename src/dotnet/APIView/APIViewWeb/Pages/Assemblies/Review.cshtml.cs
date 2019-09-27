using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Models;
using APIViewWeb.Respositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace APIViewWeb.Pages.Assemblies
{
    public class ReviewPageModel : PageModel
    {
        private readonly ReviewManager _manager;

        private readonly BlobCodeFileRepository _codeFileRepository;

        private readonly CosmosCommentsRepository _commentRepository;

        public ReviewPageModel(
            ReviewManager manager,
            BlobCodeFileRepository codeFileRepository,
            CosmosCommentsRepository commentRepository)
        {
            _manager = manager;
            _codeFileRepository = codeFileRepository;
            _commentRepository = commentRepository;
        }

        public ReviewModel Review { get; set; }
        public CodeFile CodeFile { get; set; }
        public LineApiView[] Lines { get; set; }
        public Dictionary<string, List<CommentModel>> Comments { get; set; }

        [BindProperty]
        public CommentModel Comment { get; set; }

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
            Review = await _manager.GetReviewAsync(User, id);

            var codeFile = Review.Files.SingleOrDefault();
            if (codeFile != null)
            {
                CodeFile = await _codeFileRepository.GetCodeFileAsync(codeFile.ReviewFileId);
            }
            else
            {
                return RedirectToPage("LegacyReview", new { id = id });
            }

            Lines = new CodeFileHtmlRenderer().Render(CodeFile).ToArray();
            Comments = new Dictionary<string, List<CommentModel>>();

            var assemblyComments = await _commentRepository.GetCommentsAsync(id);

            foreach (var comment in assemblyComments)
            {
                if (!Comments.TryGetValue(comment.ElementId, out _))
                    Comments[comment.ElementId] = new List<CommentModel>() { comment };
                else
                    Comments[comment.ElementId].Add(comment);
            }

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
            await _manager.UpdateReviewAsync(User, id);

            return RedirectToPage(new { id = id });
        }
    }
}
