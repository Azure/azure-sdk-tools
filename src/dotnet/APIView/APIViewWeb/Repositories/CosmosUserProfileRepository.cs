// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.Services.Graph.Client;

namespace APIViewWeb
{
    public class CosmosUserProfileRepository
    {
        private readonly Container _userProfileContainer;

        public CosmosUserProfileRepository(IConfiguration configuration)
        {
            var client = new CosmosClient(configuration["Cosmos:ConnectionString"]);
            _userProfileContainer = client.GetContainer("APIView", "Profiles");
        }

        public async Task<UserProfileModel> tryGetUserProfileAsync(ClaimsPrincipal User)
        {
            try
            {
                return await _userProfileContainer.ReadItemAsync<UserProfileModel>(User.GetGitHubLogin(), new PartitionKey(User.GetGitHubLogin()));
            }
            catch
            {
                return new UserProfileModel(User, null, null, null);
            }
        }

        public async Task<UserProfileModel> tryGetUserProfileByNameAsync(string UserName)
        {
            try
            {
                return await _userProfileContainer.ReadItemAsync<UserProfileModel>(UserName, new PartitionKey(UserName));
            }
            catch
            {
                return null;
            }
        }

        public async Task upsertUserProfileAsync(ClaimsPrincipal User, UserProfileModel userModel)
        {
            if(User.GetGitHubLogin().Equals(userModel.UserName))
            {
                await _userProfileContainer.UpsertItemAsync(userModel, new PartitionKey(User.GetGitHubLogin()));
            }
        }
    }
}
