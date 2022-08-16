// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Markdig;
using Markdig.SyntaxHighlighting;
using Microsoft.AspNetCore.Authorization;
using Octokit;

namespace APIViewWeb.Repositories
{
    public class UsageSampleManager
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly CosmosUsageSampleRepository _samplesRepository;
        private readonly BlobUsageSampleRepository _sampleFilesRepository;
        private readonly CosmosCommentsRepository _commentsRepository;

        public UsageSampleManager(
            IAuthorizationService authorizationService, 
            CosmosUsageSampleRepository samplesRepository,
            BlobUsageSampleRepository sampleFilesRepository,
            CosmosCommentsRepository commentsRepository)
        {
            _authorizationService = authorizationService;
            _samplesRepository = samplesRepository;
            _sampleFilesRepository = sampleFilesRepository;
            _commentsRepository = commentsRepository;
        }

        public async Task<UsageSampleModel> GetReviewUsageSampleAsync(string reviewId)
        {
            var sample = await _samplesRepository.GetUsageSampleAsync(reviewId);
            return sample;
        }

        public async Task<string> GetUsageSampleContentAsync(string fileId)
        {
            var file = await _sampleFilesRepository.GetUsageSampleAsync(fileId);

            if(file == null) return null;

            StreamReader reader = new StreamReader(file);
            string htmlString = reader.ReadToEnd();

            return htmlString;
        }

        public async Task<UsageSampleModel> UpsertReviewUsageSampleAsync(ClaimsPrincipal user, string reviewId, string sample, bool updating)
        {
            UsageSampleModel SampleModel = await _samplesRepository.GetUsageSampleAsync(reviewId);
            MarkdownPipeline pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSyntaxHighlighting()
                .Build();

            string htmlSample = Markdown.ToHtml(sample, pipeline);

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(htmlSample));
            var originalStream = new MemoryStream(Encoding.UTF8.GetBytes(sample));


            if (!updating)
            {
                if (SampleModel.UsageSampleFileId != null)
                {
                    await _sampleFilesRepository.DeleteUsageSampleAsync(SampleModel.UsageSampleFileId);
                    await _sampleFilesRepository.DeleteUsageSampleAsync(SampleModel.UsageSampleOriginalFileId);
                    await _samplesRepository.DeleteUsageSampleAsync(SampleModel);
                }
                SampleModel = new UsageSampleModel(user, reviewId);

                // Create new file and upsert the updated model
                UsageSampleFileModel SampleFile = new UsageSampleFileModel();
                UsageSampleFileModel SampleOriginal = new UsageSampleFileModel();

                SampleModel.Author = user.GetGitHubLogin();
                SampleModel.UsageSampleFileId = SampleFile.UsageSampleFileId;
                SampleModel.UsageSampleOriginalFileId = SampleOriginal.UsageSampleFileId;

                await _samplesRepository.UpsertUsageSampleAsync(SampleModel);
            }

            await _sampleFilesRepository.UploadUsageSampleAsync(SampleModel.UsageSampleFileId, stream);
            await _sampleFilesRepository.UploadUsageSampleAsync(SampleModel.UsageSampleOriginalFileId, originalStream);
            return SampleModel;
        }

        public async Task<UsageSampleModel> UpsertReviewUsageSampleAsync(ClaimsPrincipal user, string reviewId, Stream fileStream, bool updating)
        {
            StreamReader reader = new StreamReader(fileStream);
            var sample = reader.ReadToEnd();
            return await UpsertReviewUsageSampleAsync(user, reviewId, sample, updating);
        }

        public async Task DeleteUsageSampleAsync(ClaimsPrincipal user, string reviewId) 
        {
            var sampleModel = await _samplesRepository.GetUsageSampleAsync(reviewId);

            await AssertUsageSampleOwnerAsync(user, sampleModel);

            var comments = await _commentsRepository.GetCommentsAsync(reviewId);
            foreach (var comment in comments)
            {
                if (comment.IsSampleComment)
                {
                    await _commentsRepository.DeleteCommentAsync(comment);
                }
            }
            await _samplesRepository.DeleteUsageSampleAsync(sampleModel);
            await _sampleFilesRepository.DeleteUsageSampleAsync(sampleModel.UsageSampleFileId);
            await _sampleFilesRepository.DeleteUsageSampleAsync(sampleModel.UsageSampleOriginalFileId);
        }

        private async Task AssertUsageSampleOwnerAsync(ClaimsPrincipal user, UsageSampleModel sampleModel)
        {
            var result = await _authorizationService.AuthorizeAsync(user, sampleModel, new[] { UsageSampleOwnerRequirement.Instance });
            if (!result.Succeeded)
            {
                throw new AuthorizationFailedException();
            }
        }
    }
}
