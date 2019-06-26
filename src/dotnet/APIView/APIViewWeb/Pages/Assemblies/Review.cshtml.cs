using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Azure.Storage.Blobs;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb.Pages.Assemblies
{
    public class ReviewModel : PageModel
    {
        public ReviewModel(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public string AssemblyModel { get; set; }

        public async Task OnGetAsync(string id)
        {
            string connectionString = Configuration.GetValue<String>("APIVIEW_STORAGE");
            BlobServiceClient service = new BlobServiceClient(connectionString);
            var container = service.GetBlobContainerClient("hello");
            var client = container.GetBlockBlobClient(id);
            var result = await client.DownloadAsync();
            StreamReader reader = new StreamReader(result.Value.Content);
            AssemblyModel = reader.ReadToEnd();
        }
    }
}
