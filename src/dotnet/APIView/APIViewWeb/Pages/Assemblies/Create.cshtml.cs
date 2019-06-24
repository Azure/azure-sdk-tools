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

namespace APIViewWeb.Pages.Assemblies
{
    public class CreateModel : PageModel
    {
        public IActionResult OnGet()
        {
            return Page();
        }

        public AssemblyModel AssemblyModel { get; set; }

        public IActionResult OnPost(IFormFile file)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            BlobServiceClient service = new BlobServiceClient("DefaultEndpointsProtocol=https;AccountName=mcpatdemo;AccountKey=9UDTZ0uNAnEufkV09rhOvN0VrJyvDN+pxrdMEDTJ5FefYzNEAL0avRQ7Qro4zwHtOECMMfryUMxKeFI/Wx/jaQ==;EndpointSuffix=core.windows.net");
            var container = service.GetBlobContainerClient("hello");

            if (file.Length > 0)
            {
                using (var localFile = System.IO.File.Create("generated.txt"))
                {
                    file.CopyTo(localFile);
                }
            }

            AssemblyModel = new AssemblyModel("generated.txt");

            return RedirectToPage("./Index");
        }
    }
}