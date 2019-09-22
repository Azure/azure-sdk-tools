using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class DeleteModel : PageModel
    {
        private readonly BlobCodeFileRepository _codeFileRepository;

        private readonly BlobOriginalsRepository _originalsRepository;

        private readonly CosmosReviewRepository _reviewRepository;

        private readonly CosmosCommentsRepository _commentsRepository;

        public DeleteModel(BlobCodeFileRepository codeFileRepository,
            BlobOriginalsRepository originalsRepository,
            CosmosReviewRepository reviewRepository,
            CosmosCommentsRepository commentsRepository
            )
        {
            _codeFileRepository = codeFileRepository;
            _originalsRepository = originalsRepository;
            _reviewRepository = reviewRepository;
            _commentsRepository = commentsRepository;
        }

        public string AssemblyName { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reviewModel = await _reviewRepository.GetReviewAsync(id);
            AssemblyName = reviewModel.Name;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reviewModel = await _reviewRepository.GetReviewAsync(id);
            await _reviewRepository.DeleteReviewAsync(reviewModel);

            foreach (var reviewCodeFileModel in reviewModel.Files)
            {
                if (reviewCodeFileModel.HasOriginal)
                {
                    await _originalsRepository.DeleteOriginalAsync(reviewCodeFileModel.ReviewFileId);
                }
                await _codeFileRepository.DeleteCodeFileAsync(reviewCodeFileModel.ReviewFileId);
            }

            await _commentsRepository.DeleteCommentsAsync(id);

            return RedirectToPage("./Index");
        }
    }
}
