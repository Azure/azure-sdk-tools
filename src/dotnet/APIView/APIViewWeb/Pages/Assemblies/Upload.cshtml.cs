using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb.Pages.Assemblies
{
    public class UploadModel : PageModel
    {
        public UploadModel(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public IActionResult OnGet()
        {
            return Page();
        }

        public AssemblyModel AssemblyModel { get; set; }

        public async Task<IActionResult> OnPostAsync(IFormFile file)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            string connectionString = Configuration.GetValue<String>("APIVIEW_STORAGE");
            BlobServiceClient service = new BlobServiceClient(connectionString);
            var container = service.GetBlobContainerClient("hello");

            if (file.Length > 0)
            {
                using (var localFile = System.IO.File.Create("generated.txt"))
                {
                    file.CopyTo(localFile);
                }
            }

            AssemblyModel = new AssemblyModel("generated.txt", file.FileName);
            System.IO.File.WriteAllText("generated.txt", AssemblyModel.DisplayString);

            using (var newFile = System.IO.File.Open("generated.txt", FileMode.Open))
            {
                var guid = Guid.NewGuid().ToString();
                var blob = container.GetBlockBlobClient(guid);
                await blob.UploadAsync(newFile);
                blob = container.GetBlockBlobClient(guid);
                await blob.SetMetadataAsync(new Dictionary<String, String>() { { "name", file.FileName } });
            }

            return RedirectToPage("./Index");
        }
    }
}