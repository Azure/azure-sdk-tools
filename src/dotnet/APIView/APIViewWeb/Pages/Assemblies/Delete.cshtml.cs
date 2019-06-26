using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Models;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb.Pages.Assemblies
{
    public class DeleteModel : PageModel
    {
        public DeleteModel(IConfiguration configuration)
        {
            Configuration = configuration;
            string connectionString = Configuration.GetValue<String>("APIVIEW_STORAGE");
            BlobServiceClient service = new BlobServiceClient(connectionString);
            Container = service.GetBlobContainerClient("hello");
        }

        public IConfiguration Configuration { get; }

        public BlobContainerClient Container { get; set; }

        public string AssemblyModel { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            
            var client = Container.GetBlockBlobClient(id);
            var result = await client.DownloadAsync();
            StreamReader reader = new StreamReader(result.Value.Content);
            AssemblyModel = reader.ReadToEnd();

            if (AssemblyModel == null)
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

            var client = Container.GetBlockBlobClient(id);
            await client.DeleteAsync();

            return RedirectToPage("./Index");
        }
    }
}