using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Mvc;
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

        public Response<BlobsFlatSegment> Segment { get; set; }

        public async Task OnGetAsync()
        {
            string connectionString = Configuration.GetValue<String>("APIVIEW_STORAGE");
            var service = new BlobServiceClient(connectionString);
            var container = service.GetBlobContainerClient("hello");
            Segment = await container.ListBlobsFlatSegmentAsync(options: new Azure.Storage.Blobs.Models.BlobsSegmentOptions() { Details = new Azure.Storage.Blobs.Models.BlobListingDetails() { Metadata = true } });
        }
    }
}