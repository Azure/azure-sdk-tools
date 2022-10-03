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

namespace APIViewWeb.Repositories
{
    public class UserPreferenceCache
    {
        private readonly IMemoryCache _cache;
        private readonly IMapper _mapper;
        private readonly Container _userPreferenceContainer;

        public UserPreferenceCache(IConfiguration configuration, IMemoryCache cache, IMapper mapper)
        {
            _cache = cache;
            _mapper = mapper;
            var client = new CosmosClient(configuration["Cosmos:ConnectionString"]);
            _userPreferenceContainer = client.GetContainer("APIView", "UserPreference");
        }

        public async void UpdateUserPreference(UserPreferenceModel preference, string userName)
        {
            UserPreferenceModel existingPreference = await GetUserPreferences(userName);
            _mapper.Map<UserPreferenceModel, UserPreferenceModel>(preference, existingPreference);
            UpdateCache(existingPreference, userName);
        }

        public async Task<UserPreferenceModel> GetUserPreferences(string userName)
        {
            if (_cache.TryGetValue(userName, out UserPreferenceModel _preference))
            {
                return _preference;
            }
            else
            {
                var preference = await GetUserPreferenceFromDBAsync(userName);
                UpdateCache(preference, userName);
                return preference;
            }
        }

        public IEnumerable<ReviewType> GetFilterType(string userName, ReviewType defaultType = ReviewType.Automatic)
        {
            if (_cache.TryGetValue(userName, out UserPreferenceModel _preference))
            {
                return _preference.FilterType;
            }
            return new List<ReviewType> { defaultType };
        }

        private void UpdateCache(UserPreferenceModel preference, string userName) 
        {
            MemoryCacheEntryOptions memoryCacheEntryOptions = new MemoryCacheEntryOptions()
                .AddExpirationToken(new CancellationChangeToken(new CancellationTokenSource(TimeSpan.FromHours(24)).Token))
                .SetSlidingExpiration(TimeSpan.FromHours(2))
                .RegisterPostEvictionCallback((key, value, reason, state) => {
                    if (reason == EvictionReason.TokenExpired || reason == EvictionReason.Expired || reason == EvictionReason.Capacity)
                    {
                        UserPreferenceModel newPreference = (UserPreferenceModel)value;
                        UserPreferenceModel existingPreference = GetUserPreferenceFromDBAsync(userName).Result;
                        newPreference.PreferenceId = existingPreference.PreferenceId;
                        newPreference.UserName = userName;
                        _userPreferenceContainer.UpsertItemAsync(newPreference, new PartitionKey(userName));
                    }
                });
            _cache.Set(userName, preference, memoryCacheEntryOptions);
        }

        private async Task<UserPreferenceModel> GetUserPreferenceFromDBAsync(string userName)
        {
            UserPreferenceModel userPreference = new UserPreferenceModel();
            using FeedIterator<UserPreferenceModel> itemQueryIterator = _userPreferenceContainer.GetItemQueryIterator<UserPreferenceModel>($"SELECT * FROM UserPreference u WHERE u.UserName = '{userName}'");
            while (itemQueryIterator.HasMoreResults)
            {
                userPreference = (await itemQueryIterator.ReadNextAsync()).SingleOrDefault() ?? userPreference;
            }
            return userPreference;
        }
    }
}
