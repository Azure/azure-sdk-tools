using System.Threading.Tasks;
using APIView;
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

        public string AssemblyModel { get; set; }

        [BindProperty]
        public CommentModel Comment { get; set; }

        public CommentModel[] Comments { get; set; }

        public async Task OnGetAsync(string id)
        {
            var assemblyModel = await assemblyRepository.ReadAssemblyContentAsync(id);
            var renderer = new HTMLRendererAPIV();
            AssemblyModel = renderer.Render(assemblyModel.Assembly);

            this.Comments = await commentRepository.FetchCommentsAsync(id);
        }

        public async Task<ActionResult> OnPostAsync(string id)
        {
            await commentRepository.UploadCommentAsync(Comment, id);

            return RedirectToPage(new { id });
        }
    }
}
