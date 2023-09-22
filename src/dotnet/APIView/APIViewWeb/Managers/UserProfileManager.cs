// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Authorization;
using Octokit;

namespace APIViewWeb.Managers
{
    public class UserProfileManager : IUserProfileManager
    {
        private ICosmosUserProfileRepository _UserProfileRepository;

        public UserProfileManager(ICosmosUserProfileRepository UserProfileRepository)
        {
            _UserProfileRepository = UserProfileRepository;
        }

        public async Task CreateUserProfileAsync(ClaimsPrincipal User, string Email, HashSet<string> Langauges = null, UserPreferenceModel Preferences = null)
        {
            await _UserProfileRepository.UpsertUserProfileAsync(User, new UserProfileModel(User, Email, Langauges, Preferences));
        }

        public async Task<UserProfileModel> TryGetUserProfileAsync(ClaimsPrincipal User)
        {
            return await _UserProfileRepository.TryGetUserProfileAsync(User.GetGitHubLogin());
        }

        public async Task<UserProfileModel> TryGetUserProfileByNameAsync(string UserName)
        {
            return await _UserProfileRepository.TryGetUserProfileAsync(UserName);
        }

        public async Task UpdateUserPreferences(ClaimsPrincipal User, UserPreferenceModel preferences)
        {
            var UserProfile = await TryGetUserProfileAsync(User);

            UserProfile.Preferences = preferences ?? new UserPreferenceModel();
            await _UserProfileRepository.UpsertUserProfileAsync(User, UserProfile);
        }

        public async Task UpdateUserProfile(ClaimsPrincipal User, string email, HashSet<string> languages, UserPreferenceModel preferences)
        {
            var UserProfile = await TryGetUserProfileAsync(User);

            if (languages != null)
            {
                UserProfile.Languages = languages;
            }

            if (preferences != null)
            {
                UserProfile.Preferences = preferences;
            }

            UserProfile.Email = email;

            await _UserProfileRepository.UpsertUserProfileAsync(User, UserProfile);
        }
    }
}
