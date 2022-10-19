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

namespace APIViewWeb
{
    public class UserProfileManager
    {
        private CosmosUserProfileRepository _UserProfileRepository;

        public UserProfileManager(CosmosUserProfileRepository UserProfileRepository)
        {
            _UserProfileRepository = UserProfileRepository;
        }

        public async Task createUserProfileAsync(ClaimsPrincipal User, string Email, HashSet<string> Langauges = null, UserPreferenceModel Preferences = null)
        {
            await _UserProfileRepository.upsertUserProfileAsync(User, new UserProfileModel(User, Email, Langauges, Preferences));
        }

        public async Task<UserProfileModel> tryGetUserProfileAsync(ClaimsPrincipal User)
        {   
            return await _UserProfileRepository.tryGetUserProfileAsync(User.GetGitHubLogin());
        }

        public async Task<UserProfileModel> tryGetUserProfileByNameAsync(string UserName)
        {
            return await _UserProfileRepository.tryGetUserProfileAsync(UserName);
        }

        public async Task updateUserPreferences(ClaimsPrincipal User, UserPreferenceModel preferences)
        {
            UserProfileModel UserProfile = await tryGetUserProfileAsync(User);

            UserProfile.Preferences = preferences ?? new UserPreferenceModel();
            await _UserProfileRepository.upsertUserProfileAsync(User, UserProfile);
        }

        public async Task updateUserProfile(ClaimsPrincipal User, string email, HashSet<string> languages, UserPreferenceModel preferences)
        {
            UserProfileModel UserProfile = await tryGetUserProfileAsync(User);

            if (languages != null)
            {
                UserProfile.Languages = languages;
            }

            if(preferences != null)
            {
                UserProfile.Preferences = preferences;
            }

            UserProfile.Email = email;

            await _UserProfileRepository.upsertUserProfileAsync(User, UserProfile);
        }
    }
}
