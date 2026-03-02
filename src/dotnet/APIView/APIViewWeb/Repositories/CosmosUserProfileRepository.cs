// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
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

        public async Task<UserProfileModel> TryGetUserProfileAsync(string UserName, bool createIfNotExist = true)
        {
            try
            {
                // When a profile is read from the database, any deprecated properties (like ScrollBarSize) 
                // will be automatically ignored during deserialization since they no longer exist in the model.
                // The property will be removed from the database the next time the profile is updated.
                return await _userProfileContainer.ReadItemAsync<UserProfileModel>(UserName, new PartitionKey(UserName));
            }
            catch
            {
                if (createIfNotExist)
                {
                    var profile = new UserProfileModel(UserName);
                    profile.Preferences.UserName = UserName;
                    return profile;
                }
                throw;
            }
        }

        public async Task<Result> UpsertUserProfileAsync(ClaimsPrincipal User, UserProfileModel userModel)
        {
            return await UpsertUserProfileAsync(User.GetGitHubLogin(), userModel);
        }

        public async Task<Result> UpsertUserProfileAsync(string userName, UserProfileModel userModel)
        {
            if (userName.Equals(userModel.UserName))
            {
                await _userProfileContainer.UpsertItemAsync(userModel, new PartitionKey(userName));
                return Result.Success;
            }
            else
            {
                return Result.Failure;
            }
        }

        public async Task<IEnumerable<string>> GetAllUsernamesAsync()
        {
            var queryable = _userProfileContainer.GetItemLinqQueryable<UserProfileModel>();
            var query = queryable.Select(u => u.UserName);

            using var iterator = query.ToFeedIterator();
            var users = new List<string>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                users.AddRange(response);
            }

            return users;
        }

        public async Task<IEnumerable<string>> GetExistingUsersAsync(IEnumerable<string> userNames)
        {
            var existingUsers = new List<string>();
            var userNamesList = userNames.ToList();
            
            if (userNamesList.Count == 0)
            {
                return existingUsers;
            }

            var queryable = _userProfileContainer.GetItemLinqQueryable<UserProfileModel>();
            IQueryable<string> query = queryable
                .Where(u => userNamesList.Contains(u.UserName))
                .Select(u => u.UserName);

            using var iterator = query.ToFeedIterator();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                existingUsers.AddRange(response);
            }

            return existingUsers;
        }
    }
}
