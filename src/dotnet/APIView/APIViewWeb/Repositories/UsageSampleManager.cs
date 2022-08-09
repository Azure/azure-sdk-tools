// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ApiView;
using APIView.DIff;
using APIViewWeb.Models;
using Microsoft.ApplicationInsights;

namespace APIViewWeb.Repositories
{
    public class UsageSampleManager
    {
        //private readonly IAuthorizationService _authorizationService;
        private readonly CosmosUsageSampleRepository _samplesRepository;
        private readonly BlobUsageSampleRepository _sampleFilesRepository;

        public UsageSampleManager(
            //IAuthorizationService authorizationService, 
            CosmosUsageSampleRepository samplesRepository,
            BlobUsageSampleRepository sampleFilesRepository)
        {
            //_authorizationService = authorizationService;
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

        public async Task<UsageSampleModel> UpsertReviewUsageSampleAsync(string reviewId, string sample)
        {
            if (sample == "No Sample.")
            {
                await DeleteUsageSampleAsync(reviewId);
                return new UsageSampleModel(reviewId, null);
            }
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(sample));

            return await UpsertReviewUsageSampleAsync(reviewId, stream);
        }

        public async Task<UsageSampleModel> UpsertReviewUsageSampleAsync(string reviewId, Stream fileStream)
        {
            // remove the old file (if present)
            UsageSampleModel Sample = await _samplesRepository.GetUsageSampleAsync(reviewId);
            if(Sample.UsageSampleFileId != null)
            {
                await _sampleFilesRepository.DeleteUsageSampleAsync(Sample.UsageSampleFileId);
            }

            // Create new file and upsert the updated model
            UsageSampleFileModel SampleFile = new UsageSampleFileModel();

            Sample.UsageSampleFileId = SampleFile.UsageSampleFileId;

            await _samplesRepository.UpsertUsageSampleAsync(Sample);
            await _sampleFilesRepository.UploadUsageSampleAsync(SampleFile.UsageSampleFileId, fileStream);

            return Sample;
        }

        public async Task DeleteUsageSampleAsync(string reviewId) 
        {
            var sampleModel = await _samplesRepository.GetUsageSampleAsync(reviewId);

            await _samplesRepository.DeleteUsageSampleAsync(sampleModel);
            await _sampleFilesRepository.DeleteUsageSampleAsync(sampleModel.UsageSampleFileId);
        }

    }
}
