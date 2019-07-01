using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb.Pages.Assemblies
{
    public class DeleteModel : PageModel
    {
        private readonly BlobAssemblyRepository assemblyRepository;

        public DeleteModel(BlobAssemblyRepository assemblyRepository)
        {
            this.assemblyRepository = assemblyRepository;
        }

        public string AssemblyContent { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            AssemblyContent = await assemblyRepository.ReadAssemblyContentAsync(id);

            if (AssemblyContent == null)
            {
                return NotFound();
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            await assemblyRepository.DeleteAssemblyAsync(id);

            return RedirectToPage("./Index");
        }
    }
}
