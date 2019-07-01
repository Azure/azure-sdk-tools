using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using APIView;

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
            string json = await assemblyRepository.ReadAssemblyContentAsync(id);
            AssemblyModel = AssemblyAPIV.JsonDeserialize(json).ToString();
        }
    }
}
