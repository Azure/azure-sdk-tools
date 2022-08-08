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

        public UsageSampleManager(
            //IAuthorizationService authorizationService, 
            CosmosUsageSampleRepository samplesRepository)
        {
            //_authorizationService = authorizationService;
            _samplesRepository = samplesRepository;
        }

        public async Task<UsageSampleModel> GetReviewUsageSampleAsync(string sampleId)
        {
            var sample = await _samplesRepository.GetUsageSampleAsync(sampleId);
            // await _sampleFilesRepository.GetUsageSampleAsync(sample.UsageSampleFileId);
            return sample;
        }
        
        public async Task<UsageSampleModel> CreateReviewUsageSampleAsync(string sampleId, string sample)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);

            writer.Write(sample);
            writer.Flush();
            stream.Position = 0;

            return await CreateReviewUsageSampleAsync(sampleId, stream);
        }

        public async Task<UsageSampleModel> CreateReviewUsageSampleAsync(string sampleId, Stream fileStream)
        {
            UsageSampleModel newSample = new UsageSampleModel(sampleId, fileStream);

            await _samplesRepository.UpsertUsageSampleAsync(newSample);

            return newSample;
        }

        public async Task DeleteUsageSampleAsync(string sampleId) 
        {
            var sampleModel = await _samplesRepository.GetUsageSampleAsync(sampleId);

            await _samplesRepository.DeleteUsageSampleAsync(sampleModel);
        }

    }
}
