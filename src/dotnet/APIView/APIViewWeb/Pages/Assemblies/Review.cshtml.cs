using System.Collections.Generic;
using System.Threading.Tasks;
using APIView;
using APIViewWeb.ExtensionMethods;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

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
        public LineAPIV[] AssemblyModel { get; set; }
        [BindProperty]
        public CommentModel Comment { get; set; }
        public Dictionary<string, List<CommentModel>> Comments { get; set; }
        public string Username { get; set; }

        public async Task<ActionResult> OnPostDeleteAsync(string id, string commentId)
        {
            await commentRepository.DeleteCommentAsync(commentId);
            return RedirectToPage(new { id });
        }

        public async Task OnGetAsync(string id)
        {
            Id = id;
            var assemblyModel = await assemblyRepository.ReadAssemblyContentAsync(id);
            var renderer = new HTMLRendererAPIV();
            AssemblyModel = renderer.Render(assemblyModel.Assembly).ToArray();
            var comments = await commentRepository.FetchCommentsAsync(id);

            Comments = new Dictionary<string, List<CommentModel>>();
            foreach (var comment in comments)
            {
                if (!Comments.TryGetValue(comment.ElementId, out List<CommentModel> list))
                    Comments[comment.ElementId] = new List<CommentModel>() { comment };
                else
                    Comments[comment.ElementId].Add(comment);
            }

            Username = User.GetGitHubLogin();
        }

        public async Task<ActionResult> OnPostAsync(string id, string cancel)
        {
            if (cancel == null)
                await commentRepository.UploadCommentAsync(Comment, id);

            return RedirectToPage(new { id });
        }
    }
}
