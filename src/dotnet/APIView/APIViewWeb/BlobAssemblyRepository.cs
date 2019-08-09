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
using System.Linq;

namespace APIViewWeb
{

    public class BlobAssemblyRepository
    {
        private readonly BlobCommentRepository commentRepository;

        public BlobAssemblyRepository(IConfiguration configuration, BlobCommentRepository commentRepository)
        {
            var connectionString = configuration.GetValue<string>("APIVIEW_STORAGE");
            var container = configuration.GetValue<string>("APIVIEW_STORAGE_CONTAINER");
            ContainerClient = new BlobContainerClient(connectionString, container);
            this.commentRepository = commentRepository;
        }

        private BlobContainerClient ContainerClient { get; }

        public async Task<AssemblyModel> ReadAssemblyContentAsync(string id)
        {
            var result = await ContainerClient.GetBlobClient(id).DownloadAsync();

            using (StreamReader reader = new StreamReader(result.Value.Content))
            {
                return JsonSerializer.Parse<AssemblyModel>(reader.ReadToEnd());
            }
        }

        public async Task<List<AssemblyModel>> FetchAssembliesAsync()
        {
            var segment = ContainerClient.GetBlobsAsync(new GetBlobsOptions() { IncludeMetadata = true });

            var blobs = new List<BlobItem>();
            await foreach (var item in segment)
            {
                blobs.Add(item);
            }
            
            var assemblies = new List<AssemblyModel>();
            foreach (var item in blobs.OrderByDescending(blob => blob.Properties.CreationTime))
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
            var blob = ContainerClient.GetBlobClient(guid);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.ToString(assemblyModel)))) {
                await blob.UploadAsync(stream);
            }
            blob = ContainerClient.GetBlobClient(guid);
            await blob.SetMetadataAsync(new Dictionary<string, string>() { { "id", guid } });
            return guid;
        }

        public async Task DeleteAssemblyAsync(string id)
        {
            await ContainerClient.GetBlobClient(id).DeleteAsync();
            await commentRepository.DeleteAssemblyCommentsAsync(id);
        }
    }
}
