using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class IndexModel : PageModel
    {
        private readonly BlobAssemblyRepository assemblyRepository;

        public IndexModel(BlobAssemblyRepository assemblyRepository)
        {
            this.assemblyRepository = assemblyRepository;
        }

        public List<(string id, string name)> Assemblies { get; set; }

        public async Task OnGetAsync()
        {
            Assemblies = await assemblyRepository.FetchAssembliesAsync();
        }
    }
}
