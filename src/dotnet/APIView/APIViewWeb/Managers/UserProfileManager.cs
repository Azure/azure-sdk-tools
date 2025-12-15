// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.Models;
using APIViewWeb.Repositories;

namespace APIViewWeb.Managers
{
    public class UserProfileManager : IUserProfileManager
    {
        private ICosmosUserProfileRepository _UserProfileRepository;

        public UserProfileManager(ICosmosUserProfileRepository UserProfileRepository)
        {
            _UserProfileRepository = UserProfileRepository;
        }

        public async Task<UserProfileModel> TryGetUserProfileAsync(ClaimsPrincipal User)
        {
            return await _UserProfileRepository.TryGetUserProfileAsync(User.GetGitHubLogin());
        }

        public async Task<UserProfileModel> TryGetUserProfileByNameAsync(string userName, bool createIfNotExist = true)
        {
            return await _UserProfileRepository.TryGetUserProfileAsync(userName, createIfNotExist);
        }

        public async Task UpdateUserProfile(string userName, UserProfileModel profile)
        {
            await _UserProfileRepository.UpsertUserProfileAsync(userName, profile);
        }
    }
}
