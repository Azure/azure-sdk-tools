using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb.Pages.Assemblies
{
    public class IndexModel : PageModel
    {
        public IndexModel(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public List<List<string>> Assemblies { get; set; }

        public async Task OnGetAsync()
        {
            var config = new BlobAssemblyRepository(Configuration);
            Assemblies = await config.FetchAssembliesAsync();
        }
    }
}