using APIViewWeb.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        /// <summary>
        /// Return the blobs contained in the comments blob container.
        /// </summary>
        /// <returns>A collection of the blobs in the container.</returns>
        public async Task<List<BlobItem>> FetchBlobsAsync()
        {
            var segment = ContainerClient.GetBlobsAsync(new GetBlobsOptions() { IncludeMetadata = true });

            var blobs = new List<BlobItem>();
            await foreach (var item in segment)
            {
                blobs.Add(item);
            }
            return blobs;
        }

        /// <summary>
        /// Return all comments written for review of the assembly with the provided ID.
        /// </summary>
        /// <param name="assemblyId">The ID of the assembly to have its comments read.</param>
        /// <returns>The comments existing for the specified assembly if it exists, or null if no assembly has the specified ID.</returns>
        public async Task<AssemblyCommentsModel> FetchCommentsAsync(string assemblyId)
        {
            var blobs = await FetchBlobsAsync();

            foreach (var blob in blobs.OrderBy(blob => blob.Properties.CreationTime))
            {
                foreach (var pair in blob.Metadata)
                {
                    if (pair.Value == assemblyId)
                    {
                        var assemblyComments = await ReadBlobContentAsync(blob.Name);
                        return assemblyComments;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Return the comments contained in the blob with the provided ID.
        /// </summary>
        /// <param name="id">The ID of the blob to have its comments read.</param>
        /// <returns>The comments existing in the specified blob.</returns>
        public async Task<AssemblyCommentsModel> ReadBlobContentAsync(string id)
        {
            var result = await ContainerClient.GetBlobClient(id).DownloadAsync();
            //result.Value.Properties.ETag   Can use this to check if version of assembly comments is up to date

            using (StreamReader reader = new StreamReader(result.Value.Content))
            {
                AssemblyCommentsModel comments = JsonSerializer.Parse<AssemblyCommentsModel>(reader.ReadToEnd());
                return comments;
            }
        }

        /// <summary>
        /// Upload the comments existing for an assembly review.
        /// </summary>
        /// <param name="assemblyComments">The comments in the review.</param>
        /// <returns></returns>
        public async Task UploadAssemblyCommentsAsync(AssemblyCommentsModel assemblyComments)
        {
            var blob = ContainerClient.GetBlobClient(assemblyComments.Id);
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.ToString(assemblyComments))))
            {
                await blob.UploadAsync(stream);
            }
            blob = ContainerClient.GetBlobClient(assemblyComments.Id);
            await blob.SetMetadataAsync(new Dictionary<string, string>() { { "assembly", assemblyComments.AssemblyId } });
        }

        /// <summary>
        /// Upload a single comment added to the review of an assembly with the specified ID.
        /// </summary>
        /// <param name="commentModel">The comment being added to the review.</param>
        /// <param name="assemblyId">The ID of the assembly being commented on.</param>
        /// <returns></returns>
        public async Task UploadCommentAsync(CommentModel commentModel, string assemblyId)
        {
            commentModel.Id = Guid.NewGuid().ToString();

            AssemblyCommentsModel assemblyComments = await FetchCommentsAsync(assemblyId);
            if (assemblyComments == null)
            {
                var assemblyCommentsId = Guid.NewGuid().ToString();
                assemblyComments = new AssemblyCommentsModel(assemblyId, commentModel, assemblyCommentsId);
            } else
            {
                assemblyComments.AddComment(commentModel);
            }
            await UploadAssemblyCommentsAsync(assemblyComments);
        }

        /// <summary>
        /// Delete the blob containing comments for an assembly with the specified ID.
        /// </summary>
        /// <param name="assemblyId">The ID of the assembly having its comments blob deleted.</param>
        /// <returns></returns>
        public async Task DeleteAssemblyCommentsAsync(string assemblyId)
        {
            var blobs = await FetchBlobsAsync();
            foreach (var blob in blobs)
            {
                foreach (var pair in blob.Metadata)
                {
                    if (pair.Value == assemblyId)
                        await ContainerClient.GetBlobClient(blob.Name).DeleteAsync();
                }
            }
        }

        /// <summary>
        /// Delete a single comment from the review of an assembly with the specified ID.
        /// </summary>
        /// <param name="assemblyId">The ID of the assembly from which a comment is being deleted.</param>
        /// <param name="commentId">The ID of the comment being deleted.</param>
        /// <returns></returns>
        public async Task DeleteCommentAsync(string assemblyId, string commentId)
        {
            var assemblyComments = await FetchCommentsAsync(assemblyId);
            assemblyComments.DeleteComment(commentId);
            await UploadAssemblyCommentsAsync(assemblyComments);
        }
    }
}
