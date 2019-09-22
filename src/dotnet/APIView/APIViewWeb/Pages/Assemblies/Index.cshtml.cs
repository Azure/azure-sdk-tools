using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class IndexPageModel : PageModel
    {
        private readonly BlobAssemblyRepository assemblyRepository;
        private readonly BlobCodeFileRepository _codeFileRepository;
        private readonly CosmosReviewRepository _cosmosReviewRepository;
        private readonly BlobCommentRepository _blobCommentRepository;
        private readonly CosmosCommentsRepository _cosmosCommentsRepository;

        public IndexPageModel(
            BlobAssemblyRepository assemblyRepository,
            BlobCodeFileRepository codeFileRepository,
            CosmosReviewRepository cosmosReviewRepository,
            BlobCommentRepository blobCommentRepository,
            CosmosCommentsRepository cosmosCommentsRepository)
        {
            this.assemblyRepository = assemblyRepository;
            _codeFileRepository = codeFileRepository;
            _cosmosReviewRepository = cosmosReviewRepository;
            _blobCommentRepository = blobCommentRepository;
            _cosmosCommentsRepository = cosmosCommentsRepository;
        }

        public IEnumerable<ReviewModel> Assemblies { get; set; }

        public async Task OnGetAsync()
        {
            Assemblies = await _cosmosReviewRepository.GetReviewsAsync();
        }

        public async Task<IActionResult> OnPostMigrateAsync()
        {
            var assemblies = await assemblyRepository.FetchAssembliesAsync();
            foreach (var assemblyModel in assemblies)
            {
                ReviewCodeFileModel[] files;
                if (assemblyModel.AssemblyNode != null)
                {
                    files = new[]
                    {
                        new ReviewCodeFileModel()
                        {
                            ReviewFileId = assemblyModel.Id,
                            RunAnalysis = assemblyModel.RunAnalysis,
                            HasOriginal = assemblyModel.HasOriginal,
                            Name = assemblyModel.OriginalFileName
                        }
                    };
                }
                else
                {
                    files = new ReviewCodeFileModel[0];
                }
                var review = new ReviewModel()
                {
                    Author = assemblyModel.Author,
                    ReviewId = assemblyModel.Id,
                    CreationDate = assemblyModel.TimeStamp,
                    Name = assemblyModel.Name,
                    Files = files
                };

                await _cosmosReviewRepository.UpsertReviewAsync(review);

                if (assemblyModel.AssemblyNode != null)
                {
                    await _codeFileRepository.UpsertCodeFileAsync(assemblyModel.Id, assemblyModel.AssemblyNode);
                }

                var assemblyCommentsModel = await _blobCommentRepository.FetchCommentsAsync(assemblyModel.Id);
                foreach (var commentModel in assemblyCommentsModel.Comments)
                {
                    commentModel.ReviewId = assemblyCommentsModel.AssemblyId;
                    await _cosmosCommentsRepository.UpsertCommentAsync(commentModel);
                }
            }

            return RedirectToPage();
        }
    }
}
