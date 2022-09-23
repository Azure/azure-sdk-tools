// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class CosmosUserRepository
    {
        private readonly Container _userContainer;

        public CosmosUserRepository(IConfiguration configuration)
        {
            var client = new CosmosClient(configuration["Cosmos:ConnectionString"]);
            _userContainer = client.GetContainer("APIView", "Users");
        }

        public async Task<UserModel> getUserAsync(ClaimsPrincipal User)
        {
            return await _userContainer.ReadItemAsync<UserModel>(User.GetGitHubLogin(), new PartitionKey("users"));
        }

        public async Task upsertUserAsync(ClaimsPrincipal User, UserModel userModel)
        {
            if(User.GetGitHubLogin().Equals(userModel.UserName))
            {
                await _userContainer.UpsertItemAsync(userModel, new PartitionKey("users"));
            }
        }
    }
}
