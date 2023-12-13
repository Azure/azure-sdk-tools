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
using APIViewWeb.Repositories;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Managers
{
    public class SamplesRevisionsManager : ISamplesRevisionsManager
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly ICosmosSamplesRevisionsRepository _samplesRevisionsRepository;
        private readonly IBlobUsageSampleRepository _sampleFilesRepository;
        private readonly ICosmosCommentsRepository _commentsRepository;
        private readonly ICommentsManager _commentsManager;

        public SamplesRevisionsManager(
            IAuthorizationService authorizationService,
            ICosmosSamplesRevisionsRepository samplesRepository,
            IBlobUsageSampleRepository sampleFilesRepository,
            ICosmosCommentsRepository commentsRepository,
            ICommentsManager commentManager)
        {
            _authorizationService = authorizationService;
            _samplesRevisionsRepository = samplesRepository;
            _sampleFilesRepository = sampleFilesRepository;
            _commentsRepository = commentsRepository;
            _commentsManager = commentManager;
        }

        public async Task<IEnumerable<SamplesRevisionModel>> GetSamplesRevisionsAsync(string reviewId)
        {
            return await _samplesRevisionsRepository.GetSamplesRevisionsAsync(reviewId);
        }

        public async Task<string> GetSamplesRevisionContentAsync(string fileId)
        {
            var file = await _sampleFilesRepository.GetUsageSampleAsync(fileId);

            if (file == null) return null;

            var reader = new StreamReader(file);
            var htmlString = reader.ReadToEnd();

            return htmlString;
        }

        public async Task<SamplesRevisionModel> UpsertSamplesRevisionsAsync(ClaimsPrincipal user, string reviewId, string sample, string revisionTitle, string FileName = null)
        {
            // markdig parser with syntax highlighting
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSyntaxHighlighting()
                .Build();

            var htmlSample = Markdown.ToHtml(sample, pipeline);

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(htmlSample));
            var originalStream = new MemoryStream(Encoding.UTF8.GetBytes(sample));

            // Create new file and upsert the updated model
            var sampleRevision = new SamplesRevisionModel();
            sampleRevision.ReviewId = reviewId;
            sampleRevision.Title = revisionTitle ?? FileName;
            sampleRevision.CreatedOn = System.DateTime.UtcNow;
            sampleRevision.CreatedBy = user.GetGitHubLogin();

            await _samplesRevisionsRepository.UpsertSamplesRevisionAsync(sampleRevision);
            await _sampleFilesRepository.UploadUsageSampleAsync(sampleRevision.FileId, stream);
            await _sampleFilesRepository.UploadUsageSampleAsync(sampleRevision.OriginalFileId, originalStream);
            return sampleRevision;
        }

        public async Task<SamplesRevisionModel> UpsertSamplesRevisionsAsync(ClaimsPrincipal user, string reviewId, Stream fileStream, string revisionTitle, string FileName)
        {
            // For file upload. Read stream then continue.
            var reader = new StreamReader(fileStream);
            var sample = reader.ReadToEnd();
            return await UpsertSamplesRevisionsAsync(user, reviewId, sample, revisionTitle, FileName);
        }

        public async Task DeleteSamplesRevisionAsync(ClaimsPrincipal user, string reviewId, string sampleId)
        {
            var samplesRevision = await _samplesRevisionsRepository.GetSamplesRevisionAsync(reviewId, sampleId);
            await AssertUsageSampleOwnerAsync(user, samplesRevision);

            samplesRevision.IsDeleted = true;

            var comments = await _commentsRepository.GetCommentsAsync(reviewId);
            foreach (var comment in comments)
            {
                var commentSampleId = comment.ElementId.Split("-")[0]; // sample id is stored as first part of ElementId
                if (comment.CommentType == CommentType.SampleRevision && commentSampleId == samplesRevision.FileId)  // remove all comments from server 
                {
                    await _commentsManager.SoftDeleteCommentAsync(user, comment);
                }
            }
            await _samplesRevisionsRepository.UpsertSamplesRevisionAsync(samplesRevision);
        }

        private async Task AssertUsageSampleOwnerAsync(ClaimsPrincipal user, SamplesRevisionModel samplesRevision)
        {
            var result = await _authorizationService.AuthorizeAsync(user, samplesRevision, new[] { UsageSampleOwnerRequirement.Instance });
            if (!result.Succeeded)
            {
                throw new AuthorizationFailedException();
            }
        }
    }
}
