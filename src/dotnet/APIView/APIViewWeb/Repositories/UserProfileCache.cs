using Microsoft.Extensions.Caching.Memory;
using APIViewWeb.Models;
using System;
using AutoMapper;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Primitives;
using APIViewWeb.Managers;

namespace APIViewWeb.Repositories
{
    public class UserProfileCache
    {
        private readonly IMemoryCache _cache;
        private readonly IMapper _mapper;
        private readonly IUserProfileManager _userProfileManager;

        public UserProfileCache(IMemoryCache cache, IMapper mapper, IUserProfileManager profileManager)
        {
            _cache = cache;
            _mapper = mapper;
            _userProfileManager = profileManager;
        }

        public async Task UpdateUserProfileAsync(string userName, string email = null, UserPreferenceModel userPreferenceModel = null)
        {
            UserProfileModel existingUserProfile = await _userProfileManager.TryGetUserProfileByNameAsync(userName);
            if (email != null)
            {
                existingUserProfile.Email = email;
            }

            if (userPreferenceModel != null)
            {
                _mapper.Map<UserPreferenceModel, UserPreferenceModel>(userPreferenceModel, existingUserProfile.Preferences);
            }
            
            UpdateCache(existingUserProfile, userName);
        }

        public async Task<UserProfileModel> GetUserProfileAsync(string userName)
        {
            if (_cache.TryGetValue(userName, out UserProfileModel _profile))
            {
                return _profile;
            }
            else
            {
                var profile = (await _userProfileManager.TryGetUserProfileByNameAsync(userName));
                UpdateCache(profile, userName);
                return profile;
            }
        }

        private void UpdateCache(UserProfileModel profile, string userName)
        {
            MemoryCacheEntryOptions memoryCacheEntryOptions = new MemoryCacheEntryOptions()
                .AddExpirationToken(new CancellationChangeToken(new CancellationTokenSource(TimeSpan.FromHours(24)).Token))
                .SetSlidingExpiration(TimeSpan.FromHours(2))
                .RegisterPostEvictionCallback(async (key, value, reason, state) => {
                    if (reason == EvictionReason.TokenExpired || reason == EvictionReason.Expired || reason == EvictionReason.Capacity)
                    {
                        UserProfileModel newProfile = (UserProfileModel)value;
                        await _userProfileManager.UpdateUserProfile(userName, newProfile);
                    }
                });
            _cache.Set(userName, profile, memoryCacheEntryOptions);
        }
    }
}
