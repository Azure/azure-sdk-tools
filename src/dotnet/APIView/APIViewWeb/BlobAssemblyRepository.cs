using APIViewWeb.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace APIViewWeb
{

    public class BlobAssemblyRepository
    {
        public BlobAssemblyRepository(IConfiguration configuration)
        {
            Configuration = configuration;
            ConnectionString = Configuration.GetValue<string>("APIVIEW_STORAGE");
            Container = Configuration.GetValue<string>("APIVIEW_STORAGE_CONTAINER");
            Client = new BlobContainerClient(ConnectionString, Container);
        }

        public IConfiguration Configuration { get; }

        public BlobContainerClient Client { get; }
        public string ConnectionString { get; }
        public string Container { get; }

        public async Task<string> ReadAssemblyContentAsync(string id)
        {
            var client = new BlockBlobClient(ConnectionString, Container, id);
            var result = await client.DownloadAsync();
            string content;
            using (StreamReader reader = new StreamReader(result.Value.Content))
            {
                content = reader.ReadToEnd();
            }
            return content;
        }

        public async Task<List<List<string>>> FetchAssembliesAsync()
        {
            var client = new BlobContainerClient(ConnectionString, Container);
            var segment = await client.ListBlobsFlatSegmentAsync(options: new BlobsSegmentOptions() { Details = new BlobListingDetails() { Metadata = true } });

            var assemblies = new List<List<string>>();
            foreach (var item in segment.Value.BlobItems)
            {
                var assemblyView = new List<string> { item.Name };  // assemblyView stores an assembly's ID and display name as [ID, displayName]
                foreach (var pair in item.Metadata)
                {
                    assemblyView.Add(pair.Value);
                }
                assemblies.Add(assemblyView);
            }
            return assemblies;
        }

        public async Task UploadAssemblyAsync(IFormFile file)
        {
            var client = new BlobContainerClient(ConnectionString, Container);
            AssemblyModel assemblyModel = new AssemblyModel(file.OpenReadStream(), file.FileName);

            var guid = Guid.NewGuid().ToString();
            var blob = client.GetBlockBlobClient(guid);
            await blob.UploadAsync(new MemoryStream(Encoding.UTF8.GetBytes(assemblyModel.DisplayString)));
            blob = client.GetBlockBlobClient(guid);
            await blob.SetMetadataAsync(new Dictionary<string, string>() { { "name", file.FileName } });
        }

        public async Task DeleteAssemblyAsync(string id)
        {
            var client = new BlockBlobClient(ConnectionString, Container, id);
            await client.DeleteAsync();
        }
    }
}
