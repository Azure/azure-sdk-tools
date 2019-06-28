using APIViewWeb.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
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
            var connectionString = configuration.GetValue<string>("APIVIEW_STORAGE");
            var container = configuration.GetValue<string>("APIVIEW_STORAGE_CONTAINER");
            ContainerClient = new BlobContainerClient(connectionString, container);
        }

        private BlobContainerClient ContainerClient { get; }

        public async Task<string> ReadAssemblyContentAsync(string id)
        {
            var result = await ContainerClient.GetBlockBlobClient(id).DownloadAsync();
            using (StreamReader reader = new StreamReader(result.Value.Content))
            {
                return reader.ReadToEnd();
            }
        }

        public async Task<List<(string id, string name)>> FetchAssembliesAsync()
        {
            var segment = await ContainerClient.ListBlobsFlatSegmentAsync(options: new BlobsSegmentOptions() { Details = new BlobListingDetails() { Metadata = true } });

            var assemblies = new List<(string id, string name)>();
            foreach (var item in segment.Value.BlobItems)
            {
                foreach (var pair in item.Metadata)
                {
                    assemblies.Add((id: item.Name, name: pair.Value));
                }
            }
            return assemblies;
        }

        public async Task UploadAssemblyAsync(AssemblyModel assemblyModel, string fileName)
        {
            var guid = Guid.NewGuid().ToString();
            var blob = ContainerClient.GetBlockBlobClient(guid);
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(assemblyModel.DisplayString))) {
                await blob.UploadAsync(stream);
            }
            blob = ContainerClient.GetBlockBlobClient(guid);
            await blob.SetMetadataAsync(new Dictionary<string, string>() { { "name", fileName } });
        }

        public async Task DeleteAssemblyAsync(string id)
        {
            await ContainerClient.GetBlockBlobClient(id).DeleteAsync();
        }
    }
}
