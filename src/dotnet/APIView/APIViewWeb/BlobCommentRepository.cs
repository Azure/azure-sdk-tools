using APIViewWeb.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace APIViewWeb
{
    public class BlobCommentRepository
    {
        public BlobCommentRepository(IConfiguration configuration)
        {
            var connectionString = configuration.GetValue<string>("APIVIEW_STORAGE");
            var container = configuration.GetValue<string>("APIVIEW_COMMENT_CONTAINER");
            ContainerClient = new BlobContainerClient(connectionString, container);
        }

        private BlobContainerClient ContainerClient { get; }

        public async Task<CommentModel> ReadCommentContentAsync(string id)
        {
            var result = await ContainerClient.GetBlockBlobClient(id).DownloadAsync();

            // Return a rendering of the AssemblyAPIV object deserialized from JSON.
            using (StreamReader reader = new StreamReader(result.Value.Content))
            {
                CommentModel comment = JsonSerializer.Parse<CommentModel>(reader.ReadToEnd());
                return comment;
            }
        }

        public async Task<CommentModel[]> FetchCommentsAsync(string assemblyID)
        {
            var segment = await ContainerClient.ListBlobsFlatSegmentAsync(options: new BlobsSegmentOptions() { Details = new BlobListingDetails() { Metadata = true } });

            var comments = new List<CommentModel>();
            foreach (var item in segment.Value.BlobItems)
            {
                foreach (var pair in item.Metadata)
                {
                    if (pair.Value == assemblyID)
                    {
                        var comment = await ReadCommentContentAsync(item.Name);
                        comments.Add(comment);
                    }
                }
            }
            return comments.ToArray();
        }

        public async Task UploadCommentAsync(CommentModel commentModel, string assemblyID)
        {
            var guid = Guid.NewGuid().ToString();
            commentModel.Id = guid;
            var blob = ContainerClient.GetBlockBlobClient(guid);

            // Store the JSON serialization of the assembly.
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.ToString(commentModel))))
            {
                await blob.UploadAsync(stream);
            }
            blob = ContainerClient.GetBlockBlobClient(guid);
            await blob.SetMetadataAsync(new Dictionary<string, string>() { { "assembly", assemblyID } });
        }

        public async Task DeleteCommentAsync(string id)
        {
            await ContainerClient.GetBlockBlobClient(id).DeleteAsync();
        }
    }
}
