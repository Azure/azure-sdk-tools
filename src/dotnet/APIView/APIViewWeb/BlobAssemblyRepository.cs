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
using System.Text.Json;

namespace APIViewWeb
{

    public class BlobAssemblyRepository
    {
        private readonly BlobCommentRepository commentRepository;

        public BlobAssemblyRepository(IConfiguration configuration, BlobCommentRepository commentRepository)
        {
            var connectionString = configuration["APIVIEW_STORAGE"] ?? configuration["Reviews:ConnectionString"];
            var container = configuration["APIVIEW_STORAGE_CONTAINER"] ?? configuration["Reviews:Container"];
            ContainerClient = new BlobContainerClient(connectionString, container);
            this.commentRepository = commentRepository;
        }

        private BlobContainerClient ContainerClient { get; }

        /// <summary>
        /// Return the blobs contained in the assemblies blob container.
        /// </summary>
        /// <returns>A collection of the blobs in the container.</returns>
        public List<BlobItem> FetchBlobs()
        {
            var segment = ContainerClient.GetBlobs(new GetBlobsOptions() { IncludeMetadata = true });

            var blobs = new List<BlobItem>();
            foreach (var item in segment)
            {
                blobs.Add(item);
            }
            return blobs;
        }

        /// <summary>
        /// Return all assemblies available for review.
        /// </summary>
        /// <returns>A collection of the assemblies available for review.</returns>
        public async Task<List<AssemblyModel>> FetchAssembliesAsync()
        {
            var blobs = FetchBlobs();

            var assemblies = new List<AssemblyModel>();
            foreach (var item in blobs.OrderByDescending(blob => blob.Properties.CreationTime))
            {
                AssemblyModel assembly = await ReadAssemblyContentAsync(item.Name);
                assemblies.Add(assembly);
            }
            return assemblies;
        }

        /// <summary>
        /// Return the contents contained in the assembly blob with the provided ID.
        /// </summary>
        /// <param name="id">The ID of the assembly blob to have its comments read.</param>
        /// <returns>The contents of the specified assembly blob.</returns>
        public async Task<AssemblyModel> ReadAssemblyContentAsync(string id)
        {
            var result = await ContainerClient.GetBlobClient(id).DownloadAsync();

            using (StreamReader reader = new StreamReader(result.Value.Content))
            {
                return JsonSerializer.Deserialize<AssemblyModel>(reader.ReadToEnd());
            }
        }

        /// <summary>
        /// Upload a single assembly for review.
        /// </summary>
        /// <param name="assemblyModel">The assembly being uploaded.</param>
        /// <returns>The ID assigned to the assembly in the database.</returns>
        public async Task<string> UploadAssemblyAsync(AssemblyModel assemblyModel)
        {
            var guid = Guid.NewGuid().ToString();
            assemblyModel.Id = guid;
            var assemblyComments = new AssemblyCommentsModel(guid);
            await commentRepository.UploadAssemblyCommentsAsync(assemblyComments);
            var blob = ContainerClient.GetBlobClient(guid);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(assemblyModel)))) {
                await blob.UploadAsync(stream);
            }
            blob = ContainerClient.GetBlobClient(guid);
            await blob.SetMetadataAsync(new Dictionary<string, string>() { { "id", guid } });
            return guid;
        }

        /// <summary>
        /// Delete a single assembly from the database.
        /// </summary>
        /// <param name="id">The ID of the assembly being deleted.</param>
        /// <returns></returns>
        public async Task DeleteAssemblyAsync(string id)
        {
            await ContainerClient.GetBlobClient(id).DeleteAsync();
            await commentRepository.DeleteAssemblyCommentsAsync(id);
        }
    }
}
