// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.Services.Graph.Client;

namespace APIViewWeb
{
    public class CosmosUserProfileRepository
    {
        private readonly Container _userProfileContainer;

        public CosmosUserProfileRepository(IConfiguration configuration, CosmosClient cosmosClient = null)
        {
            var client = cosmosClient ?? new CosmosClient(configuration["Cosmos:ConnectionString"]);
            _userProfileContainer = client.GetContainer("APIView", "Profiles");
        }

        public async Task<UserProfileModel> tryGetUserProfileAsync(string UserName)
        {
            try
            {
                return await _userProfileContainer.ReadItemAsync<UserProfileModel>(UserName, new PartitionKey(UserName));
            }
            catch
            {
                return new UserProfileModel(UserName);
            }
        }

        public async Task<Result> upsertUserProfileAsync(ClaimsPrincipal User, UserProfileModel userModel)
        {
            if(User.GetGitHubLogin().Equals(userModel.UserName))
            {
                await _userProfileContainer.UpsertItemAsync(userModel, new PartitionKey(User.GetGitHubLogin()));
                return Result.Success;
            }
            else
            {
                return Result.Failure;
            }
        }
    }
}
