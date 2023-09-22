// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class CosmosUserProfileRepository : ICosmosUserProfileRepository
    {
        private readonly Container _userProfileContainer;

        public CosmosUserProfileRepository(IConfiguration configuration, CosmosClient cosmosClient)
        {
            _userProfileContainer = cosmosClient.GetContainer("APIView", "Profiles");
        }

        public async Task<UserProfileModel> TryGetUserProfileAsync(string UserName)
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

        public async Task<Result> UpsertUserProfileAsync(ClaimsPrincipal User, UserProfileModel userModel)
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
