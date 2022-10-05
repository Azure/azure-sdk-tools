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
using System.Linq;
using Microsoft.AspNetCore.Mvc.Formatters;

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

        public async Task<UsageSampleModel> UpsertReviewUsageSampleAsync(ClaimsPrincipal user, string reviewId, string sample, int revisionNum, string revisionTitle, string FileName = null)
        {
            // markdig parser with syntax highlighting
            MarkdownPipeline pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSyntaxHighlighting()
                .Build();

            string htmlSample = Markdown.ToHtml(sample, pipeline);

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(htmlSample));
            var originalStream = new MemoryStream(Encoding.UTF8.GetBytes(sample));
            UsageSampleModel SampleModel = (await _samplesRepository.GetUsageSampleAsync(reviewId)).FirstOrDefault() ?? new UsageSampleModel(reviewId);

            // Create new file and upsert the updated model
            UsageSampleRevisionModel SampleRevision = new UsageSampleRevisionModel(user, revisionNum);
            if (revisionTitle == null && FileName != null)
            {
                SampleRevision.RevisionTitle = FileName;
            }
            else
            {
                SampleRevision.RevisionTitle = revisionTitle;
            }
            if(SampleModel.Revisions == null)
            {
                SampleModel.Revisions = new List<UsageSampleRevisionModel>();
            }
            SampleModel.Revisions.Add(SampleRevision);

            await _samplesRepository.UpsertUsageSampleAsync(SampleModel);
            await _sampleFilesRepository.UploadUsageSampleAsync(SampleRevision.FileId, stream);
            await _sampleFilesRepository.UploadUsageSampleAsync(SampleRevision.OriginalFileId, originalStream);
            return SampleModel;
        }

        public async Task<UsageSampleModel> UpsertReviewUsageSampleAsync(ClaimsPrincipal user, string reviewId, Stream fileStream, int revisionNum, string revisionTitle, string FileName)
        {
            // For file upload. Read stream then continue.
            StreamReader reader = new StreamReader(fileStream);
            var sample = reader.ReadToEnd();
            return await UpsertReviewUsageSampleAsync(user, reviewId, sample, revisionNum, revisionTitle, FileName);
        }

        public async Task DeleteUsageSampleAsync(ClaimsPrincipal user, string reviewId, string FileId, string sampleId) 
        {
            var sampleModels = (await _samplesRepository.GetUsageSampleAsync(reviewId)).Find(e => e.SampleId == sampleId);
            var sampleModel = sampleModels.Revisions.Find(e => e.FileId == FileId);

            await AssertUsageSampleOwnerAsync(user, sampleModel);

            sampleModels.Revisions.Remove(sampleModel);

            int i = 0;
            foreach (var revision in sampleModels.Revisions)
            {
                if (revision.RevisionIsDeleted)
                {
                    continue; 
                }
                i++;
                revision.RevisionNumber = i;
            }

            var comments = await _commentsRepository.GetCommentsAsync(reviewId);
            foreach (var comment in comments)
            {
                string commentSampleId = comment.ElementId.Split("-")[0]; // sample id is stored as first part of ElementId
                if (comment.IsUsageSampleComment && commentSampleId == FileId)  // remove all comments from server 
                {
                    await _commentsRepository.DeleteCommentAsync(comment);
                }
            }

            sampleModel.RevisionIsDeleted = true;

            await _samplesRepository.UpsertUsageSampleAsync(sampleModels);

        }

        private async Task AssertUsageSampleOwnerAsync(ClaimsPrincipal user, UsageSampleRevisionModel sampleModel)
        {
            var result = await _authorizationService.AuthorizeAsync(user, sampleModel, new[] { UsageSampleOwnerRequirement.Instance });
            if (!result.Succeeded)
            {
                throw new AuthorizationFailedException();
            }
        }
    }
}
