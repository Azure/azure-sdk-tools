using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
<<<<<<< HEAD
using System.Text.Json.Serialization;
using APIView;
=======
>>>>>>> json2

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
            AssemblyModel = assemblyModel.Assembly.ToString();
        }
    }
}
