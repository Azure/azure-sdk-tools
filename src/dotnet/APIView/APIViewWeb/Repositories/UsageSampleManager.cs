// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Markdig;
using Markdig.SyntaxHighlighting;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;

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

        public async Task<List<UsageSampleModel>> GetReviewUsageSampleAsync(string reviewId)
        {
            return await _samplesRepository.GetUsageSampleAsync(reviewId);
        }

        public async Task<string> GetUsageSampleContentAsync(string fileId)
        {
            var file = await _sampleFilesRepository.GetUsageSampleAsync(fileId);

            if(file == null) return null;

            StreamReader reader = new StreamReader(file);
            string htmlString = reader.ReadToEnd();

            return htmlString;
        }

        public async Task<UsageSampleModel> UpsertReviewUsageSampleAsync(ClaimsPrincipal user, string reviewId, string sample, int revisionNum, string revisionTitle)
        {
            MarkdownPipeline pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSyntaxHighlighting()
                .Build();

            string htmlSample = Markdown.ToHtml(sample, pipeline);

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(htmlSample));
            var originalStream = new MemoryStream(Encoding.UTF8.GetBytes(sample));
            UsageSampleModel SampleModel = new UsageSampleModel(user, reviewId, revisionNum);

            SampleModel.RevisionTitle = revisionTitle;

            // Create new file and upsert the updated model
            UsageSampleFileModel SampleFile = new UsageSampleFileModel();
            UsageSampleFileModel SampleOriginal = new UsageSampleFileModel();

            SampleModel.Author = user.GetGitHubLogin();
            SampleModel.UsageSampleFileId = SampleFile.UsageSampleFileId;
            SampleModel.UsageSampleOriginalFileId = SampleOriginal.UsageSampleFileId;

            await _samplesRepository.UpsertUsageSampleAsync(SampleModel);

            await _sampleFilesRepository.UploadUsageSampleAsync(SampleModel.UsageSampleFileId, stream);
            await _sampleFilesRepository.UploadUsageSampleAsync(SampleModel.UsageSampleOriginalFileId, originalStream);
            return SampleModel;
        }

        public async Task<UsageSampleModel> UpsertReviewUsageSampleAsync(ClaimsPrincipal user, string reviewId, Stream fileStream, int revisionNum, string revisionTitle)
        {
            StreamReader reader = new StreamReader(fileStream);
            var sample = reader.ReadToEnd();
            return await UpsertReviewUsageSampleAsync(user, reviewId, sample, revisionNum, revisionTitle);
        }

        public async Task DeleteUsageSampleAsync(ClaimsPrincipal user, string reviewId, string sampleId) 
        {
            var sampleModels = await _samplesRepository.GetUsageSampleAsync(reviewId);
            var sampleModel = sampleModels.Find(e => e.SampleId == sampleId);

            await AssertUsageSampleOwnerAsync(user, sampleModel);

            var comments = await _commentsRepository.GetCommentsAsync(reviewId);
            foreach (var comment in comments)
            {
                string commentSampleId = comment.ElementId.Split("-")[0];
                if (comment.IsSampleComment && commentSampleId == sampleId) 
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
