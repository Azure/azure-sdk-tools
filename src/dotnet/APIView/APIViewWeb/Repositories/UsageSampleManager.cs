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
            return await _samplesRepository.GetUsageSampleAsync(sampleId);
        }
        
        public async Task<UsageSampleModel> AddReviewUsageSampleAsync(string sampleId, string fileString)
        {
            UsageSampleModel newSample = new UsageSampleModel(sampleId, fileString);
            
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
