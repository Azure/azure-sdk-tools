using System.Security.Policy;
using Microsoft.Extensions.Caching.Memory;
using APIViewWeb.Models;
using System;
using System.Collections.Generic;

namespace APIViewWeb.Repositories
{
    public class UserPreferenceCache
    {
        private readonly IMemoryCache _cache;

        public UserPreferenceCache(IMemoryCache cache)
        {
            _cache = cache;
        }

        public void UpdateUserPreference(UserPreferenceModel preference)
        {
            _cache.Set(preference.UserName, preference);
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
