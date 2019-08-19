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

        public UploadModel(BlobAssemblyRepository assemblyRepository)
        {
            this.assemblyRepository = assemblyRepository;
        }

        [BindProperty]
        public bool KeepOriginal { get; set; } 

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
                    assemblyModel.HasOriginal = true;
                    assemblyModel.OriginalFileName = file.FileName;
                    assemblyModel.TimeStamp = DateTime.UtcNow;
                    assemblyModel.BuildFromStream(memoryStream);

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
