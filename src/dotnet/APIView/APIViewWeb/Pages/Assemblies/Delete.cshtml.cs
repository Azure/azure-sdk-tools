using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class DeleteModel : PageModel
    {
        private readonly BlobAssemblyRepository assemblyRepository;

        public DeleteModel(BlobAssemblyRepository assemblyRepository)
        {
            this.assemblyRepository = assemblyRepository;
        }

        public string AssemblyName { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var assemblyModel = await assemblyRepository.ReadAssemblyContentAsync(id);
            AssemblyName = assemblyModel.Name;

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
