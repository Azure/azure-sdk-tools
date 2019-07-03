using System.Threading.Tasks;
using APIView;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class ReviewModel : PageModel
    {
        private readonly BlobAssemblyRepository assemblyRepository;

        public ReviewModel(BlobAssemblyRepository assemblyRepository)
        {
            this.assemblyRepository = assemblyRepository;
        }

        public string AssemblyModel { get; set; }

        public async Task OnGetAsync(string id)
        {
            var assemblyModel = await assemblyRepository.ReadAssemblyContentAsync(id);
            var renderer = new HTMLRendererAPIV();
            AssemblyModel = renderer.Render(assemblyModel.Assembly);
        }
    }
}
