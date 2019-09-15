using System;
using System.IO;
using System.Threading.Tasks;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class UploadModel : PageModel
    {
        private readonly BlobAssemblyRepository assemblyRepository;
        private readonly BlobCommentRepository comments;

        public UploadModel(BlobAssemblyRepository assemblyRepository, BlobCommentRepository comments)
        {
            this.assemblyRepository = assemblyRepository;
            this.comments = comments;
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

                    AssemblyModel assemblyModel = new AssemblyModel();
                    assemblyModel.Author = User.GetGitHubLogin();
                    assemblyModel.HasOriginal = KeepOriginal;
                    assemblyModel.OriginalFileName = file.FileName;
                    assemblyModel.TimeStamp = DateTime.UtcNow;
                    assemblyModel.RunAnalysis = RunAnalysis;

                    if (file.FileName.EndsWith(".json"))
                    {
                        await assemblyModel.BuildFromJsonAsync(memoryStream);
                    }
                    else
                    {
                        assemblyModel.BuildFromStream(memoryStream, RunAnalysis);
                    }

                    memoryStream.Position = 0;

                    var originalStream = KeepOriginal ? memoryStream : null;

                    var id = await assemblyRepository.UploadAssemblyAsync(assemblyModel, originalStream);

                    return RedirectToPage("Review", new { id });
                }
            }

            return RedirectToPage("./Index");
        }
    }
}
