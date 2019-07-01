using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json.Serialization;
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
            AssemblyAPIV assembly = JsonSerializer.Parse<AssemblyAPIV>(json);
            AssemblyModel = assembly.ToString();
        }
    }
}
