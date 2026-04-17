using Microsoft.Extensions.Caching.Memory;
using APIViewWeb.Models;
using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Primitives;
using APIViewWeb.Managers;
using APIViewWeb.DTOs;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Linq;
using APIView.Identity;

namespace APIViewWeb.Repositories
{
    public class UserProfileCache
    {
        private readonly ILogger<UserProfileCache> _logger;
        private readonly IMemoryCache _cache;
        private readonly IUserProfileManager _userProfileManager;


        public UserProfileCache(IMemoryCache cache, IUserProfileManager profileManager, ILogger<UserProfileCache> logger)
        {
            _cache = cache;
            _logger = logger;
            _userProfileManager = profileManager;
        }

        public async Task UpdateUserProfileAsync(string userName, string email = null, UserPreferenceDto userPreferenceDto = null)
        {
            try 
            {
                UserProfileModel existingUserProfile = await GetUserProfileAsync(userName, createIfNotExist: false);
                if (email != null)
                {
                    existingUserProfile.Email = email;
                }

                if (userPreferenceDto != null)
                {
                    existingUserProfile.Preferences.Language = (userPreferenceDto.Language != null) ? userPreferenceDto.Language : existingUserProfile.Preferences.Language;
                    existingUserProfile.Preferences.APIRevisionType = (userPreferenceDto.APIRevisionType != null) ? userPreferenceDto.APIRevisionType : existingUserProfile.Preferences.APIRevisionType;
                    existingUserProfile.Preferences.State = (userPreferenceDto.State != null) ? userPreferenceDto.State : existingUserProfile.Preferences.State;
                    existingUserProfile.Preferences.Status = (userPreferenceDto.Status != null) ? userPreferenceDto.Status : existingUserProfile.Preferences.Status;
                    existingUserProfile.Preferences.HideLineNumbers = (userPreferenceDto.HideLineNumbers != null) ? (bool)userPreferenceDto.HideLineNumbers : existingUserProfile.Preferences.HideLineNumbers;
                    existingUserProfile.Preferences.HideLeftNavigation = (userPreferenceDto.HideLeftNavigation != null) ? (bool)userPreferenceDto.HideLeftNavigation : existingUserProfile.Preferences.HideLeftNavigation;
                    existingUserProfile.Preferences.ShowHiddenApis = (userPreferenceDto.ShowHiddenApis != null) ? (bool)userPreferenceDto.ShowHiddenApis : existingUserProfile.Preferences.ShowHiddenApis;
                    existingUserProfile.Preferences.ShowDocumentation = (userPreferenceDto.ShowDocumentation != null) ? (bool)userPreferenceDto.ShowDocumentation : existingUserProfile.Preferences.ShowDocumentation;
                    existingUserProfile.Preferences.HideReviewPageOptions = (userPreferenceDto.HideReviewPageOptions != null) ? (bool)userPreferenceDto.HideReviewPageOptions : existingUserProfile.Preferences.HideReviewPageOptions;
                    existingUserProfile.Preferences.HideIndexPageOptions = (userPreferenceDto.HideIndexPageOptions != null) ? (bool)userPreferenceDto.HideIndexPageOptions : existingUserProfile.Preferences.HideIndexPageOptions;
                    existingUserProfile.Preferences.HideSamplesPageOptions = (userPreferenceDto.HideSamplesPageOptions != null) ? (bool)userPreferenceDto.HideSamplesPageOptions : existingUserProfile.Preferences.HideSamplesPageOptions;
                    existingUserProfile.Preferences.HideRevisionsPageOptions = (userPreferenceDto.HideRevisionsPageOptions != null) ? (bool)userPreferenceDto.HideRevisionsPageOptions : existingUserProfile.Preferences.HideRevisionsPageOptions;
                    existingUserProfile.Preferences.ShowComments = (userPreferenceDto.ShowComments != null) ? (bool)userPreferenceDto.ShowComments : existingUserProfile.Preferences.ShowComments;
                    existingUserProfile.Preferences.ShowSystemComments = (userPreferenceDto.ShowSystemComments != null) ? (bool)userPreferenceDto.ShowSystemComments : existingUserProfile.Preferences.ShowSystemComments;
                    existingUserProfile.Preferences.DisableCodeLinesLazyLoading = (userPreferenceDto.DisableCodeLinesLazyLoading != null) ? (bool)userPreferenceDto.DisableCodeLinesLazyLoading : existingUserProfile.Preferences.DisableCodeLinesLazyLoading;
                    existingUserProfile.Preferences.Theme = (userPreferenceDto.Theme != null) ? userPreferenceDto.Theme : existingUserProfile.Preferences.Theme;
                }
                UpdateCache(existingUserProfile, userName);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating UserProfile: {ex.Message}");
            }
        }

        public async Task UpdateUserProfileAsync(string userName, string email = null, UserPreferenceModel userPreferenceModel = null)
        {
            try 
            {
                UserProfileModel existingUserProfile = await GetUserProfileAsync(userName, createIfNotExist: false);
                if (email != null)
                {
                    existingUserProfile.Email = email;
                }

                if (userPreferenceModel != null)
                {
                    existingUserProfile.Preferences = userPreferenceModel;
                }
                UpdateCache(existingUserProfile, userName);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating UserProfile: {ex.Message}");
            }
        }

        public async Task<UserProfileModel> GetUserProfileAsync(string userName, bool createIfNotExist = true)
        {
            if (_cache.TryGetValue(userName, out UserProfileModel _profile))
            {
                return _profile;
            }
            else
            {
                var profile = await _userProfileManager.TryGetUserProfileByNameAsync(userName, createIfNotExist: createIfNotExist);
                UpdateCache(profile, userName);
                return profile;
            }
        }

        public async Task SetUserEmailIfNullOrEmpty(ClaimsPrincipal User)
        {
            var userProfile = await GetUserProfileAsync(userName: User.GetGitHubLogin());

            if (string.IsNullOrWhiteSpace(userProfile.Email))
            {
                var microsoftEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimConstants.Email)?.Value;

                if (!string.IsNullOrWhiteSpace(microsoftEmail))
                {
                    userProfile.Email = microsoftEmail;
                    await UpdateUserProfileAsync(userProfile.UserName, userProfile.Email, userProfile.Preferences);
                }
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
