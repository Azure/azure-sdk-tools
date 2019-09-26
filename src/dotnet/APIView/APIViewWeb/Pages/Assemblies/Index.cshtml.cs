using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class IndexPageModel : PageModel
    {
        private readonly CosmosReviewRepository _cosmosReviewRepository;

        private readonly CosmosReviewRepository _reviewsRepository;

        private readonly BlobCodeFileRepository _codeFileRepository;

        private readonly BlobOriginalsRepository _originalsRepository;

        public IndexPageModel(
            CosmosReviewRepository cosmosReviewRepository,
            CosmosReviewRepository reviewsRepository,
            BlobCodeFileRepository codeFileRepository,
            BlobOriginalsRepository originalsRepository)
        {
            _cosmosReviewRepository = cosmosReviewRepository;
            _reviewsRepository = reviewsRepository;
            _codeFileRepository = codeFileRepository;
            _originalsRepository = originalsRepository;
        }

        [FromForm]
        public UploadModel Upload { get; set; }

        public IEnumerable<ReviewModel> Assemblies { get; set; }

        public async Task OnGetAsync()
        {
            Assemblies = await _cosmosReviewRepository.GetReviewsAsync();
        }

        public async Task<IActionResult> OnPostUploadAsync()
        {
            if (!ModelState.IsValid)
            {
                return RedirectToPage();
            }

            var file = Upload.Files.SingleOrDefault();

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
                    reviewCodeFileModel.HasOriginal = true;
                    reviewCodeFileModel.Name = file.Name;
                    var uploadRunAnalysis = Upload.RunAnalysis;
                    reviewCodeFileModel.RunAnalysis = uploadRunAnalysis;

                    reviewModel.Files = new [] { reviewCodeFileModel };

                    CodeFile codeFile;
                    if (file.FileName.EndsWith(".json"))
                    {
                        codeFile = await CodeFile.DeserializeAsync(memoryStream);
                    }
                    else
                    {
                        codeFile = CodeFileBuilder.Build(memoryStream, uploadRunAnalysis);
                    }

                    memoryStream.Position = 0;
                    reviewModel.Name = codeFile.Name;

                    if (true)
                    {
                        await _originalsRepository.UploadOriginalAsync(reviewCodeFileModel.ReviewFileId, memoryStream);
                    }

                    await _codeFileRepository.UpsertCodeFileAsync(reviewCodeFileModel.ReviewFileId, codeFile);
                    await _reviewsRepository.UpsertReviewAsync(reviewModel);

                    return RedirectToPage("Review", new { id = reviewModel.ReviewId });
                }
            }

            return RedirectToPage();
        }
    }
}
