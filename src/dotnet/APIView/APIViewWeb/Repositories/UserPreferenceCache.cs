using System.Security.Policy;
using Microsoft.Extensions.Caching.Memory;
using APIViewWeb.Models;
using System;
using System.Collections.Generic;
using AutoMapper;

namespace APIViewWeb.Repositories
{
    public class UserPreferenceCache
    {
        private readonly IMemoryCache _cache;
        private readonly IMapper _mapper;

        public UserPreferenceCache(IMemoryCache cache, IMapper mapper)
        {
            _cache = cache;
            _mapper = mapper;
        }

        public void UpdateUserPreference(UserPreferenceModel preference)
        {
            UserPreferenceModel existingPreference = GetUserPreferences(preference.UserName);
            if (existingPreference == null)
            {
                _cache.Set(preference.UserName, preference);
            }
            else
            {
                _mapper.Map<UserPreferenceModel, UserPreferenceModel>(preference, existingPreference);
                _cache.Set(preference.UserName, existingPreference);
            }
        }

        public UserPreferenceModel GetUserPreferences(string userName)
        {
            if (_cache.TryGetValue(userName, out UserPreferenceModel _preference))
            {
                return _preference;
            }
            return null;
        }

        public IEnumerable<string> GetLangauge(string userName)
        {
            if (_cache.TryGetValue(userName, out UserPreferenceModel _preference))
            {
                return _preference.Language;
            }

            return null;
        }

        public IEnumerable<ReviewType> GetFilterType(string userName, ReviewType defaultType = ReviewType.Automatic)
        {
            if (_cache.TryGetValue(userName, out UserPreferenceModel _preference))
            {
                return _preference.FilterType;
            }

            return new List<ReviewType> { defaultType };
        }
    }
}
