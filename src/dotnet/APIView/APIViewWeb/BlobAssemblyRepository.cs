using APIViewWeb.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using APIView;
using System.Linq;

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

        public async Task<AssemblyModel> ReadAssemblyContentAsync(string id)
        {
            var result = await ContainerClient.GetBlockBlobClient(id).DownloadAsync();

            using (StreamReader reader = new StreamReader(result.Value.Content))
            {
                return JsonSerializer.Parse<AssemblyModel>(reader.ReadToEnd());
            }
        }

        public async Task<List<AssemblyModel>> FetchAssembliesAsync()
        {
            var segment = await ContainerClient.ListBlobsFlatSegmentAsync(options: new BlobsSegmentOptions() { Details = new BlobListingDetails() { Metadata = true } });

            var assemblies = new List<AssemblyModel>();
            foreach (var item in segment.Value.BlobItems.OrderByDescending(blob => blob.Properties.CreationTime))
            {
                foreach (var pair in item.Metadata)
                {
                    AssemblyModel assembly = await ReadAssemblyContentAsync(pair.Value);
                    assemblies.Add(assembly);
                }
            }
            return assemblies;
        }

        public async Task<string> UploadAssemblyAsync(AssemblyModel assemblyModel)
        {
            var guid = Guid.NewGuid().ToString();
            assemblyModel.Id = guid;
            var blob = ContainerClient.GetBlockBlobClient(guid);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.ToString(assemblyModel)))) {
                await blob.UploadAsync(stream);
            }
            blob = ContainerClient.GetBlockBlobClient(guid);
            await blob.SetMetadataAsync(new Dictionary<string, string>() { { "id", guid } });
            return guid;
        }

        public async Task DeleteAssemblyAsync(string id)
        {
            await ContainerClient.GetBlockBlobClient(id).DeleteAsync();
        }
    }
}
