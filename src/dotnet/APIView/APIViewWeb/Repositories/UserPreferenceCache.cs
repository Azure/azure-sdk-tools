using System.Security.Policy;
using Microsoft.Extensions.Caching.Memory;
using APIViewWeb.Models;
using System;
using System.Collections.Generic;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Cosmos;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using System.Linq;
using Octokit;
using System.Threading;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;
using APIViewWeb.Managers;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Repositories
{
    public class UserPreferenceCache
    {
        private readonly IMemoryCache _cache;
        private readonly IMapper _mapper;
        private readonly IUserProfileManager _userProfileManager;

        public UserPreferenceCache(IMemoryCache cache, IMapper mapper, IUserProfileManager profileManager)
        {
            _cache = cache;
            _mapper = mapper;
            _userProfileManager = profileManager;
        }

        public async void UpdateUserPreference(UserPreferenceModel preference, ClaimsPrincipal User)
        {
            UserPreferenceModel existingPreference = await GetUserPreferences(User);
            _mapper.Map<UserPreferenceModel, UserPreferenceModel>(preference, existingPreference);
            UpdateCache(existingPreference, User);
        }

        public async Task<UserPreferenceModel> GetUserPreferences(ClaimsPrincipal User)
        {
            string userName = User.GetGitHubLogin();
            if (_cache.TryGetValue(userName, out UserPreferenceModel _preference))
            {
                return _preference;
            }
            else
            {
                var preference = (await _userProfileManager.TryGetUserProfileAsync(User)).Preferences;
                UpdateCache(preference, User);
                return preference;
            }
        }

        public IEnumerable<APIRevisionType> GetFilterType(string userName, APIRevisionType defaultType = APIRevisionType.Automatic)
        {
            if (_cache.TryGetValue(userName, out UserPreferenceModel _preference))
            {
                return _preference.APIRevisionType;
            }
            return new List<APIRevisionType> { defaultType };
        }

        private void UpdateCache(UserPreferenceModel preference, ClaimsPrincipal User) 
        {
            string userName = User.GetGitHubLogin();
            MemoryCacheEntryOptions memoryCacheEntryOptions = new MemoryCacheEntryOptions()
                .AddExpirationToken(new CancellationChangeToken(new CancellationTokenSource(TimeSpan.FromHours(24)).Token))
                .SetSlidingExpiration(TimeSpan.FromHours(2))
                .RegisterPostEvictionCallback(async (key, value, reason, state) => {
                    if (reason == EvictionReason.TokenExpired || reason == EvictionReason.Expired || reason == EvictionReason.Capacity)
                    {
                        UserPreferenceModel newPreference = (UserPreferenceModel)value;
                        newPreference.UserName = User.GetGitHubLogin();
                        await _userProfileManager.UpdateUserPreferences(User, newPreference);
                    }
                });
            _cache.Set(userName, preference, memoryCacheEntryOptions);
        }
    }
}
