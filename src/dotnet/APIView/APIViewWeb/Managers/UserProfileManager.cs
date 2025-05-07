// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using APIView.Identity;
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

        public async Task<UserProfileModel> TryGetUserProfileByNameAsync(string UserName)
        {
            return await _UserProfileRepository.TryGetUserProfileAsync(UserName);
        }

        public async Task UpdateUserProfile(string userName, UserProfileModel profile)
        {
            await _UserProfileRepository.UpsertUserProfileAsync(userName, profile);
        }

        public async Task SetUserEmailIfNullOrEmpty(ClaimsPrincipal User)
        {
            var userProfile = await TryGetUserProfileAsync(User);

            if (string.IsNullOrWhiteSpace(userProfile.Email))
            {
                var microsoftEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimConstants.Email)?.Value;

                if (!string.IsNullOrWhiteSpace(microsoftEmail))
                {
                    userProfile.Email = microsoftEmail;
                    await UpdateUserProfile(User.GetGitHubLogin(), userProfile);
                }
            }
        }
    }
}
