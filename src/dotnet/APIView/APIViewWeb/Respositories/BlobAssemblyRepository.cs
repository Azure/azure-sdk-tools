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
using Azure;

namespace APIViewWeb
{
    public class BlobAssemblyRepository
    {
        private readonly BlobCommentRepository commentRepository;

        public BlobAssemblyRepository(IConfiguration configuration, BlobCommentRepository commentRepository)
        {
            var connectionString = configuration["Blob:ConnectionString"];
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

            return await AssemblyModel.DeserializeAsync(result.Value.Content);
        }
    }
}
