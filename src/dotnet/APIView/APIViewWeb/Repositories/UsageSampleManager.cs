// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb.Repositories
{
    public class UsageSampleManager
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly CosmosUsageSampleRepository _samplesRepository;
        private readonly BlobUsageSampleRepository _sampleFilesRepository;

        public UsageSampleManager(
            IAuthorizationService authorizationService, 
            CosmosUsageSampleRepository samplesRepository,
            BlobUsageSampleRepository sampleFilesRepository)
        {
            _authorizationService = authorizationService;
            _samplesRepository = samplesRepository;
            _sampleFilesRepository = sampleFilesRepository;
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

        public async Task<UsageSampleModel> UpsertReviewUsageSampleAsync(ClaimsPrincipal user, string reviewId, string sample)
        {
            if (sample == null)
            {
                await DeleteUsageSampleAsync(user, reviewId);
                return new UsageSampleModel(null, reviewId, null);
            }
            // remove the old file (if present)
            UsageSampleModel SampleModel = await _samplesRepository.GetUsageSampleAsync(reviewId);
            if (SampleModel.UsageSampleFileId != null)
            {
                await _sampleFilesRepository.DeleteUsageSampleAsync(SampleModel.UsageSampleFileId);
            }

            Markdig.MarkdownPipeline pipeline = new Markdig.MarkdownPipelineBuilder()
                //.UseAdvancedExtensions()
                //.UseColorCode()
                .Build();

            string htmlSample = Markdig.Markdown.ToHtml(sample, pipeline);

            // Create new file and upsert the updated model
            UsageSampleFileModel SampleFile = new UsageSampleFileModel();

            SampleModel.Author = user.GetGitHubLogin();
            SampleModel.UsageSampleFileId = SampleFile.UsageSampleFileId;

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(htmlSample));

            await _samplesRepository.UpsertUsageSampleAsync(SampleModel);
            await _sampleFilesRepository.UploadUsageSampleAsync(SampleFile.UsageSampleFileId, stream);

            return SampleModel;
        }

        public async Task<UsageSampleModel> UpsertReviewUsageSampleAsync(ClaimsPrincipal user, string reviewId, Stream fileStream)
        {
            StreamReader reader = new StreamReader(fileStream);
            var sample = reader.ReadToEnd();
            return await UpsertReviewUsageSampleAsync(user, reviewId, sample);
        }

        public async Task DeleteUsageSampleAsync(ClaimsPrincipal user, string reviewId) 
        {
            var sampleModel = await _samplesRepository.GetUsageSampleAsync(reviewId);

            await AssertUsageSampleOwnerAsync(user, sampleModel);

            await _samplesRepository.DeleteUsageSampleAsync(sampleModel);
            await _sampleFilesRepository.DeleteUsageSampleAsync(sampleModel.UsageSampleFileId);
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
