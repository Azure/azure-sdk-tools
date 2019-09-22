using APIViewWeb.Models;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Azure;

namespace APIViewWeb
{
    public class BlobCommentRepository
    {
        public BlobCommentRepository(IConfiguration configuration)
        {
            var connectionString = configuration["Blob:ConnectionString"];
            ContainerClient = new BlobContainerClient(connectionString, "comments");
        }

        private BlobContainerClient ContainerClient { get; }

        /// <summary>
        /// Return all comments written for review of the assembly with the provided ID.
        /// </summary>
        /// <param name="assemblyId">The ID of the assembly to have its comments read.</param>
        /// <returns>The comments existing for the specified assembly if it exists, or null if no assembly has the specified ID.</returns>
        public async Task<AssemblyCommentsModel> FetchCommentsAsync(string assemblyId)
        {
            try
            {
                var result = await ContainerClient.GetBlobClient(assemblyId).DownloadAsync();

                using (StreamReader reader = new StreamReader(result.Value.Content))
                {
                    AssemblyCommentsModel comments = JsonSerializer.Deserialize<AssemblyCommentsModel>(reader.ReadToEnd());
                    return comments;
                }
            }
            catch (RequestFailedException e) when (e.Status == 404)
            {
                return new AssemblyCommentsModel(assemblyId);
            }
        }
    }
}
