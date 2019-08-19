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
    public class ReviewModel : PageModel
    {
        private readonly BlobAssemblyRepository assemblyRepository;
        private readonly BlobCommentRepository commentRepository;

        public ReviewModel(BlobAssemblyRepository assemblyRepository, BlobCommentRepository commentRepository)
        {
            this.assemblyRepository = assemblyRepository;
            this.commentRepository = commentRepository;
        }

        public string Id { get; set; }
        public LineApiView[] Lines { get; set; }
        public AssemblyModel AssemblyModel { get; set; }

        public bool UpdateAvailable => AssemblyModel?.AssemblyNode != null &&
                                       AssemblyModel.HasOriginal &&
                                       AssemblyModel.AssemblyNode.Version != CodeFile.CurrentVersion &&
                                       AssemblyModel.Author == User.GetGitHubLogin();
        [BindProperty]
        public CommentModel Comment { get; set; }
        public Dictionary<string, List<CommentModel>> Comments { get; set; }
        public string Username { get; set; }

        public async Task<ActionResult> OnPostDeleteAsync(string id, string commentId, string elementId)
        {
            await commentRepository.DeleteCommentAsync(id, commentId);
            var commentArray = await commentRepository.FetchCommentsAsync(id);
            List<CommentModel> comments = commentArray.Comments.Where(comment => comment.ElementId == elementId).ToList();

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

        public async Task OnGetAsync(string id)
        {
            Id = id;
            AssemblyModel = await assemblyRepository.ReadAssemblyContentAsync(id);
            if (AssemblyModel.AssemblyNode != null)
            {
                Lines = new CodeFileHtmlRenderer().Render(AssemblyModel.AssemblyNode).ToArray();
            }
            else
            {
            	var renderer = new HTMLRendererApiView();
                Lines = renderer.Render(AssemblyModel.Assembly).ToArray();
            }
            Comments = new Dictionary<string, List<CommentModel>>();

            var assemblyComments = await commentRepository.FetchCommentsAsync(id);
            var comments = assemblyComments.Comments;

            foreach (var comment in comments)
            {
                if (!Comments.TryGetValue(comment.ElementId, out List<CommentModel> list))
                    Comments[comment.ElementId] = new List<CommentModel>() { comment };
                else
                    Comments[comment.ElementId].Add(comment);
            }

            Username = User.GetGitHubLogin();
        }

        public async Task<ActionResult> OnPostAsync(string id)
        {
            Comment.TimeStamp = DateTime.UtcNow;
            Comment.Username = User.GetGitHubLogin();
            await commentRepository.UploadCommentAsync(Comment, id);
            var assemblyComments = await commentRepository.FetchCommentsAsync(id);
            var commentArray = assemblyComments.Comments;
            List<CommentModel> comments = commentArray.Where(comment => comment.ElementId == Comment.ElementId).ToList();

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

        public async Task<ActionResult> OnPostRefreshModelAsync(string id)
        {
            var assemblyModel = await assemblyRepository.ReadAssemblyContentAsync(id);
            if (assemblyModel.HasOriginal)
            {
                assemblyModel.BuildFromStream(await assemblyRepository.GetOriginalAsync(id));
                await assemblyRepository.UpdateAsync(assemblyModel);
            }
            return RedirectToPage(new { id = id });
        }
    }
}
