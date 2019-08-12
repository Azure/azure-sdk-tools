using APIViewWeb.Models;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using System;
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

        /// <summary>
        /// Return all comments written for review of the assembly with the provided ID.
        /// </summary>
        /// <param name="assemblyId">The ID of the assembly to have its comments read.</param>
        /// <returns>The comments existing for the specified assembly if it exists, or null if no assembly has the specified ID.</returns>
        public async Task<AssemblyCommentsModel> FetchCommentsAsync(string assemblyId)
        {
            var result = await ContainerClient.GetBlobClient(assemblyId).DownloadAsync();

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
            var blob = ContainerClient.GetBlobClient(assemblyComments.AssemblyId);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.ToString(assemblyComments))))
            {
                await blob.UploadAsync(stream);
            }
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
            assemblyComments.AddComment(commentModel);
            await UploadAssemblyCommentsAsync(assemblyComments);
        }

        /// <summary>
        /// Delete the blob containing comments for an assembly with the specified ID.
        /// </summary>
        /// <param name="assemblyId">The ID of the assembly having its comments blob deleted.</param>
        /// <returns></returns>
        public async Task DeleteAssemblyCommentsAsync(string assemblyId)
        {
            await ContainerClient.GetBlobClient(assemblyId).DeleteAsync();
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
