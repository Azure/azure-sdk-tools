using System;
using System.IO;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class UploadModel : PageModel
    {
        private readonly CosmosReviewRepository _reviewsRepository;

        private readonly BlobCodeFileRepository _codeFileRepository;

        private readonly BlobOriginalsRepository _originalsRepository;

        public UploadModel(CosmosReviewRepository reviewsRepository,
            BlobCodeFileRepository codeFileRepository,
            BlobOriginalsRepository originalsRepository)
        {
            this._reviewsRepository = reviewsRepository;
            _codeFileRepository = codeFileRepository;
            _originalsRepository = originalsRepository;
        }

        [BindProperty]
        public bool KeepOriginal { get; set; }

        [BindProperty]
        public bool RunAnalysis { get; set; }

        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(IFormFile file)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (file != null)
            {
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);

                    memoryStream.Position = 0;

                    ReviewModel reviewModel = new ReviewModel();
                    reviewModel.Author = User.GetGitHubLogin();
                    reviewModel.CreationDate = DateTime.UtcNow;

                    var reviewCodeFileModel = new ReviewCodeFileModel();
                    reviewCodeFileModel.HasOriginal = KeepOriginal;
                    reviewCodeFileModel.Name = file.Name;
                    reviewCodeFileModel.RunAnalysis = RunAnalysis;

                    reviewModel.Files = new [] { reviewCodeFileModel };

                    CodeFile codeFile;
                    if (file.FileName.EndsWith(".json"))
                    {
                        codeFile = await CodeFile.DeserializeAsync(memoryStream);
                    }
                    else
                    {
                        codeFile = CodeFileBuilder.Build(memoryStream, RunAnalysis);
                    }

                    memoryStream.Position = 0;
                    reviewModel.Name = codeFile.Name;

                    if (KeepOriginal)
                    {
                        await _originalsRepository.UploadOriginalAsync(reviewCodeFileModel.ReviewFileId, memoryStream);
                    }

                    await _codeFileRepository.UpsertCodeFileAsync(reviewCodeFileModel.ReviewFileId, codeFile);
                    await _reviewsRepository.UpsertReviewAsync(reviewModel);

                    return RedirectToPage("Review", new { id = reviewModel.ReviewId });
                }
            }

            return RedirectToPage("./Index");
        }
    }
}
