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
            ContainerClient = new BlobContainerClient(connectionString, "assemblies");
            OriginalsContainer = new BlobContainerClient(connectionString, "originals");
            this.commentRepository = commentRepository;
        }

        private BlobContainerClient ContainerClient { get; }
        private BlobContainerClient OriginalsContainer { get; }

        /// <summary>
        /// Return the blobs contained in the assemblies blob container.
        /// </summary>
        /// <returns>A collection of the blobs in the container.</returns>
        private List<BlobItem> FetchBlobs()
        {
            var segment = ContainerClient.GetBlobs();

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
        public async Task<string> UploadAssemblyAsync(AssemblyModel assemblyModel, Stream original)
        {
            var guid = Guid.NewGuid().ToString();
            assemblyModel.Id = guid;
            assemblyModel.HasOriginal = original != null;
            var assemblyComments = new AssemblyCommentsModel(guid);
            await commentRepository.UploadCommentsAsync(assemblyComments);
            var blob = ContainerClient.GetBlobClient(guid);
            
            await blob.UploadAsync(new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(assemblyModel)));

            if (original != null)
            {
                var originalBlob = OriginalsContainer.GetBlobClient(guid);
                await originalBlob.UploadAsync(original);
            }

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

        public async Task<Stream> GetOriginalAsync(string id)
        {
            var originalBlob = OriginalsContainer.GetBlobClient(id);
            return (await originalBlob.DownloadAsync()).Value.Content;
        }

        public async Task UpdateAsync(AssemblyModel assemblyModel)
        {
            var blob = ContainerClient.GetBlobClient(assemblyModel.Id);
            await blob.UploadAsync(new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(assemblyModel)));
        }
    }
}
