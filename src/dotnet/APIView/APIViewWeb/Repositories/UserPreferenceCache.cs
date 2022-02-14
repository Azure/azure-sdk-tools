using System.Security.Policy;
using Microsoft.Extensions.Caching.Memory;
using APIViewWeb.Models;
using System;

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
            if (_cache.TryGetValue(preference.UserName, out var _preference))
            {
                _cache.Set(preference.UserName, preference);
            }
            else
            {
                _cache.CreateEntry(preference.UserName)
                .SetSlidingExpiration(TimeSpan.FromHours(2))
                .SetValue(preference);
            }
        }

        public string GetLangauge(string userName)
        {
            if (_cache.TryGetValue(userName, out UserPreferenceModel _preference))
            {
                return _preference.Language;
            }
            return "All";
        }

        public ReviewType GetFilterType(string userName)
        {
            if (_cache.TryGetValue(userName, out UserPreferenceModel _preference))
            {
                return _preference.FilterType;
            }

            return ReviewType.Automatic;
        }
    }
}
